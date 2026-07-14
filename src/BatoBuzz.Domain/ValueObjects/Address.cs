namespace BatoBuzz.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a physical address.
/// </summary>
public readonly record struct Address
{
    public string? Street1 { get; }
    public string? Street2 { get; }
    public string? City { get; }
    public string? State { get; }
    public string? PostalCode { get; }
    public string Country { get; }

    public Address(string? street1, string? street2, string? city, string? state, string? postalCode, string country = "Nepal")
    {
        Street1 = street1;
        Street2 = street2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public override string ToString()
    {
        var parts = new List<string?>();
        if (!string.IsNullOrWhiteSpace(Street1)) parts.Add(Street1);
        if (!string.IsNullOrWhiteSpace(Street2)) parts.Add(Street2);
        if (!string.IsNullOrWhiteSpace(City)) parts.Add(City);
        if (!string.IsNullOrWhiteSpace(State)) parts.Add(State);
        if (!string.IsNullOrWhiteSpace(PostalCode)) parts.Add(PostalCode);
        if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
        return string.Join(", ", parts);
    }
}
