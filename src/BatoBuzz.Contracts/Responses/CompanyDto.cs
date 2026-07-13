namespace BatoBuzz.Contracts.Responses;

public class CompanyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PanNumber { get; set; }
    public string? CompanyRegNumber { get; set; }
    public string? VatNumber { get; set; }
    public string BaseCurrency { get; set; } = "NPR";
    public DateTime FinancialYearStart { get; set; }
    public DateTime FinancialYearEnd { get; set; }
    public bool IsActive { get; set; }
    public DateTime? PeriodLockDate { get; set; }
    public List<BranchDto> Branches { get; set; } = new();
    public List<FinancialYearDto> FinancialYears { get; set; } = new();
}

public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public class FinancialYearDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? StartDateBs { get; set; }
    public string? EndDateBs { get; set; }
    public bool IsClosed { get; set; }
    public bool IsCurrent { get; set; }
}
