namespace BatoBuzz.Contracts.Requests;

public class DateRangeRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? BranchId { get; set; }
}

public class ReportFilterRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SupplierId { get; set; }
}
