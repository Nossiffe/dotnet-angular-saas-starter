using Saas.Domain.Common;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void A_negative_amount_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.From(-1m));

    [Fact]
    public void An_empty_currency_throws() =>
        Should.Throw<ArgumentException>(() => Money.From(1m, " "));

    [Fact]
    public void From_upper_cases_the_currency()
    {
        var money = Money.From(10m, "usd");

        money.Amount.ShouldBe(10m);
        money.Currency.ShouldBe("USD");
    }

    [Fact]
    public void From_defaults_to_EUR() =>
        Money.From(5m).Currency.ShouldBe("EUR");

    [Fact]
    public void Equality_is_by_value()
    {
        Money.From(5m, "EUR").ShouldBe(Money.From(5m, "EUR"));
        Money.From(5m, "EUR").ShouldNotBe(Money.From(5m, "USD"));
    }
}
