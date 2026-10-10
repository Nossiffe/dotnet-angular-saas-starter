using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Common;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Api;

public sealed class ErrorOrProblemTests
{
    // Une réponse d'erreur porte toutes les erreurs du résultat, pas seulement la première.
    [Fact]
    public void Every_error_is_listed_under_its_code()
    {
        ErrorOr<Success> result = new List<Error>
        {
            Error.Validation("Product.NameTaken", "Ce nom est déjà pris."),
            Error.Validation("Product.PriceTooHigh", "Prix trop élevé."),
            Error.Validation("Product.PriceTooHigh", "Prix au-dessus du plafond."),
        };

        var problem = Problem(result.ToHttpResult());

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        var errors = Errors(problem);
        errors.Keys.ShouldBe(["Product.NameTaken", "Product.PriceTooHigh"]);
        errors["Product.NameTaken"].ShouldBe(["Ce nom est déjà pris."]);
        errors["Product.PriceTooHigh"].ShouldBe(["Prix trop élevé.", "Prix au-dessus du plafond."]);
    }

    // Le statut, le titre et le code restent ceux de la première erreur.
    [Fact]
    public void The_first_error_gives_the_status_title_and_code()
    {
        ErrorOr<Success> result = new List<Error>
        {
            Error.NotFound("Product.NotFound", "Produit introuvable."),
            Error.Conflict("Product.Locked", "Produit verrouillé."),
        };

        var problem = Problem(result.ToNoContentHttpResult());

        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("Produit introuvable.");
        problem.Extensions["code"].ShouldBe("Product.NotFound");
        Errors(problem).Keys.ShouldBe(["Product.NotFound", "Product.Locked"]);
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Problem(IResult result)
        => result.ShouldBeOfType<ProblemHttpResult>().ProblemDetails;

    private static Dictionary<string, string[]> Errors(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
        => problem.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>();
}
