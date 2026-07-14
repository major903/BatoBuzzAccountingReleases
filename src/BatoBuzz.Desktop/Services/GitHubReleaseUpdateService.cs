using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BatoBuzz.Desktop.Services;

/// <summary>
/// Retrieves signed-off release metadata from the official BatoBuzz GitHub
/// repository and verifies the downloaded installer against its SHA-256 manifest.
/// </summary>
public sealed class GitHubReleaseUpdateService
{
    private const string LatestReleaseEndpoint =
        "https://api.github.com/repos/major903/BatoBuzzAccountingReleases/releases/latest";
    private const string ChecksumAssetName = "SHA256SUMS.txt";
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly Regex ChecksumLine = new(
        "^(?<hash>[A-Fa-f0-9]{64})\\s+\\*?(?<file>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    public async Task<AvailableUpdate?> GetAvailableUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        var release = await HttpClient.GetFromJsonAsync<GitHubRelease>(
            LatestReleaseEndpoint,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The update service returned an empty release response.");

        var version = ParseVersion(release.TagName);
        if (version <= currentVersion)
            return null;

        var installer = release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("BatoBuzzAccounting_Setup_v", StringComparison.OrdinalIgnoreCase)
            && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Release {release.TagName} does not include a BatoBuzz installer.");
        var checksum = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Release {release.TagName} does not include {ChecksumAssetName}.");

        EnsureHttpsUrl(installer.BrowserDownloadUrl);
        EnsureHttpsUrl(checksum.BrowserDownloadUrl);

        return new AvailableUpdate(
            version,
            release.TagName,
            string.IsNullOrWhiteSpace(release.Body) ? "No release notes were provided." : release.Body.Trim(),
            installer.Name,
            installer.BrowserDownloadUrl,
            checksum.BrowserDownloadUrl);
    }

    public async Task<string> DownloadAndVerifyInstallerAsync(
        AvailableUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var checksumManifest = await HttpClient.GetStringAsync(
            update.ChecksumUrl,
            cancellationToken).ConfigureAwait(false);
        var expectedHash = GetExpectedHash(checksumManifest, update.InstallerFileName);

        var updatesDirectory = Path.Combine(DesktopStoragePaths.DataDirectory, "updates");
        Directory.CreateDirectory(updatesDirectory);

        var installerPath = Path.Combine(updatesDirectory, Path.GetFileName(update.InstallerFileName));
        var temporaryPath = installerPath + ".download";

        try
        {
            using var response = await HttpClient.GetAsync(
                update.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            string actualHash;
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                using var sha256 = SHA256.Create();
                await using (var hashingStream = new CryptoStream(
                                 destination,
                                 sha256,
                                 CryptoStreamMode.Write,
                                 leaveOpen: true))
                {
                    await source.CopyToAsync(hashingStream, cancellationToken).ConfigureAwait(false);
                    hashingStream.FlushFinalBlock();
                }

                actualHash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            }

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The downloaded installer did not match the release checksum. The update was cancelled.");
            }

            File.Move(temporaryPath, installerPath, overwrite: true);
            return installerPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("BatoBuzzAccounting", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static Version ParseVersion(string tagName)
    {
        var versionText = tagName.Trim().TrimStart('v', 'V');
        var prereleaseSeparator = versionText.IndexOf('-');
        if (prereleaseSeparator >= 0)
            versionText = versionText[..prereleaseSeparator];

        if (!Version.TryParse(versionText, out var version))
            throw new InvalidOperationException($"Release tag '{tagName}' is not a valid version.");

        return version;
    }

    private static string GetExpectedHash(string manifest, string installerFileName)
    {
        foreach (Match match in ChecksumLine.Matches(manifest))
        {
            var manifestFileName = Path.GetFileName(match.Groups["file"].Value.Trim());
            if (string.Equals(manifestFileName, installerFileName, StringComparison.OrdinalIgnoreCase))
                return match.Groups["hash"].Value.ToLowerInvariant();
        }

        throw new InvalidOperationException(
            $"{ChecksumAssetName} does not contain a checksum for {installerFileName}.");
    }

    private static void EnsureHttpsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The release contains an unsafe download URL.");
        }
    }

    public sealed record AvailableUpdate(
        Version Version,
        string TagName,
        string ReleaseNotes,
        string InstallerFileName,
        string InstallerUrl,
        string ChecksumUrl);

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = "";
    }
}
