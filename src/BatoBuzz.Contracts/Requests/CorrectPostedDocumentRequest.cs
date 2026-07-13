namespace BatoBuzz.Contracts.Requests;

public sealed class CorrectPostedDocumentRequest
{
    public DateTime CorrectionDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}
