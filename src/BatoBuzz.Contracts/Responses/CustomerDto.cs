namespace BatoBuzz.Contracts.Responses;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public string? Code { get; set; }
    public string? PanNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NameNepali { get; set; }
    public string? Code { get; set; }
    public string? PanNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
}
