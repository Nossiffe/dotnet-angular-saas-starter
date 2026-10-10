using Mediator;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.Application.Products.CreateProduct;
using Saas.Application.Products.DeleteProduct;
using Saas.Application.Products.GetProductById;
using Saas.Application.Products.ListProducts;
using Saas.Application.Products.UpdateProduct;
using Saas.Infrastructure.Identity;

namespace Saas.Api.Endpoints;

public sealed class ProductEndpoints : IEndpoint
{
    private const string GetProductRoute = "GetProduct";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products")
            .RequireAuthorization(AuthorizationPolicies.HasTenant)
            .WithAuthorizationResponses();

        group.MapPost("/", CreateProduct)
            .WithCreatedResponses<CreateProductResponse>();

        group.MapGet("/", ListProducts)
            .Produces<PagedResult<ProductDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetProduct)
            .WithMetadata(new RouteNameMetadata(GetProductRoute))
            .WithStandardResponses<ProductDto>();

        // 409 : le produit a été modifié par une autre requête entre la lecture et l'écriture.
        group.MapPut("/{id:guid}", UpdateProduct)
            .WithNoContentResponses()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteProduct)
            .WithNoContentResponses()
            .RequireAuthorization(AuthorizationPolicies.TenantAdmin);
    }

    private static async Task<IResult> CreateProduct(
        CreateProductRequest request, ISender sender, HttpContext http, CancellationToken ct)
    {
        var result = await sender.Send(new CreateProductCommand(request.Name, request.Price), ct);
        return result
            .Then(id => new CreateProductResponse(id))
            .ToCreatedHttpResult(created => http.PathTo(GetProductRoute, new { id = created.Id }));
    }

    private static async Task<IResult> ListProducts(
        ISender sender, CancellationToken ct, int page = 1, int pageSize = 20)
        => (await sender.Send(new ListProductsQuery(page, pageSize), ct)).ToHttpResult();

    private static async Task<IResult> GetProduct(Guid id, ISender sender, CancellationToken ct)
        => (await sender.Send(new GetProductByIdQuery(id), ct)).ToHttpResult();

    private static async Task<IResult> UpdateProduct(
        Guid id, UpdateProductRequest request, ISender sender, CancellationToken ct)
    {
        return (await sender.Send(new UpdateProductCommand(id, request.Name, request.Price), ct))
            .ToNoContentHttpResult();
    }

    private static async Task<IResult> DeleteProduct(Guid id, ISender sender, CancellationToken ct)
        => (await sender.Send(new DeleteProductCommand(id), ct)).ToNoContentHttpResult();
}
