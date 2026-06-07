using Saas.Domain.Common;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Montant_negatif_lance() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.From(-1m));

    [Fact]
    public void Devise_vide_lance() =>
        Should.Throw<ArgumentException>(() => Money.From(1m, " "));

    [Fact]
    public void From_normalise_la_devise_en_majuscules()
    {
        var money = Money.From(10m, "usd");

        money.Amount.ShouldBe(10m);
        money.Currency.ShouldBe("USD");
    }

    [Fact]
    public void From_devise_par_defaut_est_EUR() =>
        Money.From(5m).Currency.ShouldBe("EUR");

    [Fact]
    public void Egalite_par_valeur()
    {
        Money.From(5m, "EUR").ShouldBe(Money.From(5m, "EUR"));
        Money.From(5m, "EUR").ShouldNotBe(Money.From(5m, "USD"));
    }
}
