namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Stores the last issued number for a company and document series. Values are
/// allocated atomically by the persistence layer so concurrent requests cannot
/// receive the same business document number.
/// </summary>
public class DocumentSequence
{
    public Guid CompanyId { get; private set; }
    public string SequenceKey { get; private set; } = null!;
    public long LastValue { get; private set; }

    private DocumentSequence() { }
}
