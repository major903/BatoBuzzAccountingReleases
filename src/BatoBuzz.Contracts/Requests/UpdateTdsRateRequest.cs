namespace BatoBuzz.Contracts.Requests;

public sealed class UpdateTdsRateRequest
{
    public string Name { get; set; } = null!;
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
