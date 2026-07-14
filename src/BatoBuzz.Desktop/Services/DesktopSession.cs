namespace BatoBuzz.Desktop.Services;

public sealed class DesktopSession
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public Guid? CompanyId { get; set; }
    public string CompanyName { get; set; } = "";

    public bool HasCompany => CompanyId.HasValue && CompanyId.Value != Guid.Empty;
}
