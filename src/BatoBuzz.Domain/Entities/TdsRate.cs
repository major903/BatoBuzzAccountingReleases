namespace BatoBuzz.Domain.Entities;

/// <summary>
/// A configurable Tax Deducted at Source (TDS) withholding rate.
/// Rates are accountant-configured, not hardcoded: official Nepal TDS
/// percentages vary by payment type and require verification against
/// current IRD rules before use (see docs/nepal/NEPAL_ACCOUNTING_RULES.md).
/// </summary>
public class TdsRate : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public decimal RatePercent { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private TdsRate() { }

    public static TdsRate Create(Guid companyId, string name, decimal ratePercent, string? description = null)
    {
        return new TdsRate
        {
            CompanyId = companyId,
            Name = name,
            RatePercent = ratePercent,
            Description = description
        };
    }

    public void Update(string? name = null, decimal? ratePercent = null, string? description = null, bool? isActive = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (ratePercent.HasValue) RatePercent = ratePercent.Value;
        if (description != null) Description = description;
        if (isActive.HasValue) IsActive = isActive.Value;
    }
}
