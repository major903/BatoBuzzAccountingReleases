namespace BatoBuzz.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount with currency.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "NPR")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "NPR") => new(0, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies.");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies.");
        var result = left.Amount - right.Amount;
        if (result < 0)
            throw new InvalidOperationException("Money subtraction would result in negative amount.");
        return new Money(result, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier, money.Currency);

    public static Money operator /(Money money, decimal divisor) =>
        divisor == 0
            ? throw new DivideByZeroException("Cannot divide money by zero.")
            : new Money(money.Amount / divisor, money.Currency);

    public override string ToString() => $"{Amount:N2} {Currency}";
}
