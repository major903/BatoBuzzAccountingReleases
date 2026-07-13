using System.IO;

namespace BatoBuzz.Desktop.Services;

internal static class DesktopStoragePaths
{
    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string DatabasePath => Path.Combine(DataDirectory, "BatoBuzz.db");
    public static string PendingRestorePath => Path.Combine(DataDirectory, "restore.pending");

    private static string ResolveDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable("BATOBUZZ_DATA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(overridePath.Trim()));

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatoBuzz",
            "Accounting");
    }
}
