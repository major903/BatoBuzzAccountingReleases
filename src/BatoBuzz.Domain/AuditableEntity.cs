namespace BatoBuzz.Domain;

/// <summary>
/// Base entity with audit trail support.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; protected set; }
    public DateTime? ModifiedAt { get; protected set; }
    public Guid? ModifiedByUserId { get; protected set; }
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();
    public Guid CompanyId { get; internal set; }

    public void SetCreatedBy(Guid userId)
    {
        CreatedByUserId = userId;
    }

    public void SetModifiedBy(Guid userId)
    {
        ModifiedByUserId = userId;
        ModifiedAt = DateTime.UtcNow;
    }
}
