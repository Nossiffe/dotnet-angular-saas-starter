using Saas.Domain.Common;
using Saas.Domain.Products;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_nom_vide_lance() =>
        Should.Throw<ArgumentException>(() => Product.Create("", Money.From(1m)));

    [Fact]
    public void Create_valide_leve_domain_event()
    {
        var product = Product.Create("Stylo", Money.From(2.5m));

        product.Name.ShouldBe("Stylo");
        product.Price.Amount.ShouldBe(2.5m);
        product.DomainEvents.ShouldContain(e => e is ProductCreatedDomainEvent);
    }

    [Fact]
    public void Rename_met_a_jour_le_nom()
    {
        var product = Product.Create("Stylo", Money.From(1m));

        product.Rename("  Stylo bleu  ");

        product.Name.ShouldBe("Stylo bleu");
    }

    [Fact]
    public void ChangePrice_remplace_le_prix()
    {
        var product = Product.Create("Stylo", Money.From(1m));

        product.ChangePrice(Money.From(9.99m, "USD"));

        product.Price.Amount.ShouldBe(9.99m);
        product.Price.Currency.ShouldBe("USD");
    }

    [Fact]
    public void ClearDomainEvents_vide_la_collection()
    {
        var product = Product.Create("Stylo", Money.From(1m));

        product.ClearDomainEvents();

        product.DomainEvents.ShouldBeEmpty();
    }
}
