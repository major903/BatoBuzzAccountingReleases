namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Immutable audit trail record for all significant actions in the system.
/// </summary>
public class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? CompanyId { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserName { get; private set; } = null!;
    public string Action { get; private set; } = null!; // e.g., "Invoice.Created", "Journal.Posted"
    public string EntityType { get; private set; } = null!; // e.g., "SalesInvoice", "JournalEntry"
    public Guid? EntityId { get; private set; }
    public string? OldValues { get; private set; } // JSON snapshot
    public string? NewValues { get; private set; } // JSON snapshot
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(
        string action,
        string entityType,
        Guid? entityId = null,
        Guid? companyId = null,
        Guid? userId = null,
        string userName = "System",
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            CompanyId = companyId,
            UserId = userId,
            UserName = userName,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}
