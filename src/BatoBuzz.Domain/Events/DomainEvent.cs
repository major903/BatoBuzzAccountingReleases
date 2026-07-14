namespace BatoBuzz.Domain.Events;

/// <summary>
/// Base class for all domain events.
/// </summary>
public abstract class DomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
