using Saas.Domain.Common;
using Saas.Domain.Products;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_with_an_empty_name_throws() =>
        Should.Throw<ArgumentException>(() => Product.Create("", Money.From(1m)));

    [Fact]
    public void Create_with_a_name_of_spaces_throws() =>
        Should.Throw<ArgumentException>(() => Product.Create("   ", Money.From(1m)));

    [Fact]
    public void Create_trims_the_name() =>
        Product.Create("  Stylo bleu  ", Money.From(1m)).Name.ShouldBe("Stylo bleu");

    [Fact]
    public void Create_raises_ProductCreatedDomainEvent()
    {
        var product = Product.Create("Stylo", Money.From(2.5m));

        product.Name.ShouldBe("Stylo");
        product.Price.Amount.ShouldBe(2.5m);
        product.DomainEvents.ShouldContain(e => e is ProductCreatedDomainEvent);
    }

    [Fact]
    public void Rename_updates_the_name()
    {
        var product = Product.Create("Stylo", Money.From(1m));

        product.Rename("  Stylo bleu  ");

        product.Name.ShouldBe("Stylo bleu");
    }

    [Fact]
    public void ChangePrice_replaces_the_price()
    {
        var product = Product.Create("Stylo", Money.From(1m));

        product.ChangePrice(Money.From(9.99m, "USD"));

        product.Price.Amount.ShouldBe(9.99m);
        product.Price.Currency.ShouldBe("USD");
    }
}
