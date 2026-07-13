namespace BatoBuzz.Domain.Common;

/// <summary>
/// Applies the storage precision used by all persisted monetary amounts.
/// Rounding at the domain boundary prevents a balanced in-memory document
/// becoming unbalanced when written to numeric(18,2) columns.
/// </summary>
public static class Money
{
    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
