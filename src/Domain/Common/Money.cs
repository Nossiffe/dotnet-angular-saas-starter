using System.Globalization;

namespace Saas.Domain.Common;

/// <summary>Value object monétaire (montant + devise). Mappé en owned type EF.</summary>
public sealed record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money From(decimal amount, string currency = "EUR")
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Le montant ne peut pas être négatif.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Devise requise.", nameof(currency));
        }

        return new Money(amount, currency.Trim().ToUpperInvariant());
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");
}
