using BatoBuzz.Domain.Common;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a supplier (creditor) in the accounting system.
/// Each supplier has a linked ledger account for tracking payables.
/// </summary>
public class Supplier : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? NameNepali { get; private set; }
    public string? Code { get; private set; }
    public string? PanNumber { get; private set; } // Nepal PAN
    public string? VatNumber { get; private set; } // Supplier VAT registration
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public decimal CreditLimit { get; private set; } = 0;
    public int PaymentTermsDays { get; private set; } = 0;
    public decimal CurrentBalance { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;

    // Navigation - linked ledger for accounting
    public Guid LedgerId { get; private set; }
    public Ledger Ledger { get; private set; } = null!;
    public ICollection<PurchaseBill> Bills { get; private set; } = new List<PurchaseBill>();
    public ICollection<Payment> Payments { get; private set; } = new List<Payment>();

    private Supplier() { }

    public static Supplier Create(
        Guid companyId,
        string name,
        Guid ledgerId,
        string? code = null,
        string? nameNepali = null,
        string? panNumber = null,
        string? vatNumber = null,
        string? address = null,
        string? city = null,
        string? phone = null,
        string? email = null,
        decimal creditLimit = 0,
        int paymentTermsDays = 0)
    {
        return new Supplier
        {
            CompanyId = companyId,
            Name = name,
            LedgerId = ledgerId,
            Code = code,
            NameNepali = nameNepali,
            PanNumber = panNumber,
            VatNumber = vatNumber,
            Address = address,
            City = city,
            Phone = phone,
            Email = email,
            CreditLimit = creditLimit,
            PaymentTermsDays = paymentTermsDays
        };
    }

    public void UpdateBalance(decimal amount)
    {
        CurrentBalance = Money.Round(CurrentBalance + amount);
    }

    public void Update(string? name = null, string? phone = null, string? email = null, string? address = null, string? city = null, decimal? creditLimit = null, string? panNumber = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (phone != null) Phone = phone;
        if (email != null) Email = email;
        if (address != null) Address = address;
        if (city != null) City = city;
        if (creditLimit.HasValue) CreditLimit = creditLimit.Value;
        if (panNumber != null) PanNumber = string.IsNullOrWhiteSpace(panNumber) ? null : panNumber.Trim();
    }
}
