namespace BatoBuzz.Desktop.Services;

/// <summary>
/// The Windows 7 edition has no in-app updater. Legacy installers are released
/// separately so it cannot download a Windows 10/11-only package by mistake.
/// </summary>
public sealed class GitHubReleaseUpdateService
{
    public Task<AvailableUpdate?> GetAvailableUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AvailableUpdate?>(null);

    public Task<string> DownloadAndVerifyInstallerAsync(
        AvailableUpdate update,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("In-app updates are not available in the Windows 7 legacy edition.");

    public sealed record AvailableUpdate(Version Version, string TagName, string ReleaseNotes, string InstallerFileName, string InstallerUrl, string ChecksumUrl);

    public sealed record UpdateDownloadProgress(string Status, long DownloadedBytes, long? TotalBytes, int? Percent);
}
