using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BatoBuzz.Desktop.Services;

public sealed class RememberedLoginService
{
    private static readonly string DataDirectory = DesktopStoragePaths.DataDirectory;

    private static readonly string SessionPath = Path.Combine(DataDirectory, "session.dat");

    public RememberedLoginSession? TryLoad()
    {
        try
        {
            if (!File.Exists(SessionPath))
                return null;

            var protectedBytes = File.ReadAllBytes(SessionPath);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            var session = JsonSerializer.Deserialize<RememberedLoginSession>(json);

            if (session == null || session.UserId == Guid.Empty || session.ExpiresAtUtc <= DateTime.UtcNow)
            {
                Clear();
                return null;
            }

            return session;
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public void Save(Guid userId, string userName, TimeSpan lifetime, Guid? lastCompanyId = null)
    {
        Directory.CreateDirectory(DataDirectory);
        var session = new RememberedLoginSession(userId, userName, DateTime.UtcNow.Add(lifetime), lastCompanyId);
        WriteSession(session);
    }

    /// <summary>Updates which company to resume into next launch, without disturbing the remembered credentials or their expiry.</summary>
    public void UpdateLastCompany(Guid userId, Guid companyId)
    {
        var existing = TryLoad();
        if (existing == null || existing.UserId != userId)
            return;

        WriteSession(existing with { LastCompanyId = companyId });
    }

    private void WriteSession(RememberedLoginSession session)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(session);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SessionPath, protectedBytes);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(SessionPath))
                File.Delete(SessionPath);
        }
        catch
        {
            // A failed cleanup should not block login or logout.
        }
    }
}

public sealed record RememberedLoginSession(Guid UserId, string UserName, DateTime ExpiresAtUtc, Guid? LastCompanyId = null);
