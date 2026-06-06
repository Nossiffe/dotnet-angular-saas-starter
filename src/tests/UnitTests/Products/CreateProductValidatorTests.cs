using FluentValidation.TestHelper;
using Saas.Application.Products.CreateProduct;
using Xunit;

namespace Saas.UnitTests.Products;

public sealed class CreateProductValidatorTests
{
    private readonly CreateProductValidator _validator = new();

    [Fact]
    public void Name_vide_est_invalide()
    {
        var result = _validator.TestValidate(new CreateProductCommand("", 10m));
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Prix_negatif_est_invalide()
    {
        var result = _validator.TestValidate(new CreateProductCommand("Stylo", -1m));
        result.ShouldHaveValidationErrorFor(c => c.Price);
    }

    [Fact]
    public void Commande_valide_passe()
    {
        var result = _validator.TestValidate(new CreateProductCommand("Stylo", 2.5m));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
