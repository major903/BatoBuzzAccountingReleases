namespace BatoBuzz.Domain.Exceptions;

/// <summary>
/// Base exception for all domain-level errors.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}
