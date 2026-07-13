using BatoBuzz.Domain.Common;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Represents a financial/fiscal year for a company.
/// Supports both Gregorian (AD) and Bikram Sambat (BS) dates for Nepal.
/// </summary>
public class FinancialYear : AuditableEntity
{
    public string Name { get; internal set; } = null!; // e.g., "2081-2082" for Nepal
    public DateTime StartDate { get; internal set; }
    public DateTime EndDate { get; internal set; }
    public string? StartDateBs { get; private set; } // Bikram Sambat start
    public string? EndDateBs { get; private set; } // Bikram Sambat end
    public bool IsClosed { get; private set; } = false;
    public bool IsCurrent { get; internal set; } = true;

    internal static FinancialYear Create(Guid companyId, DateTime startDate, DateTime endDate, bool isCurrent = true)
    {
        return new FinancialYear
        {
            CompanyId = companyId,
            Name = $"{startDate:yyyy}-{endDate:yyyy}",
            StartDate = startDate,
            EndDate = endDate,
            StartDateBs = BikramSambatConverter.IsSupported(startDate) ? BikramSambatConverter.ToBsDateString(startDate) : null,
            EndDateBs = BikramSambatConverter.IsSupported(endDate) ? BikramSambatConverter.ToBsDateString(endDate) : null,
            IsCurrent = isCurrent
        };
    }

    public void Close()
    {
        if (IsClosed)
            throw new InvalidOperationException("Financial year is already closed.");
        IsClosed = true;
        IsCurrent = false;
    }

    public bool ContainsDate(DateTime date) =>
        date.Date >= StartDate.Date && date.Date <= EndDate.Date;
}
