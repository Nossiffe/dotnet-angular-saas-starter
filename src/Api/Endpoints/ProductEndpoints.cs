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
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/products").WithTags("Products")
            .RequireAuthorization(AuthorizationPolicies.HasTenant);

        group.MapPost("/", CreateProduct)
            .WithCreatedResponses<CreateProductResponse>();

        group.MapGet("/", ListProducts)
            .Produces<PagedResult<ProductDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetProduct)
            .WithStandardResponses<ProductDto>();

        group.MapPut("/{id:guid}", UpdateProduct)
            .WithNoContentResponses();

        group.MapDelete("/{id:guid}", DeleteProduct)
            .WithNoContentResponses()
            .RequireAuthorization(AuthorizationPolicies.TenantAdmin);
    }

    private static async Task<IResult> CreateProduct(
        CreateProductRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.Send(new CreateProductCommand(request.Name, request.Price), ct);
        // enveloppe dans le contract existant sur le chemin succès
        return result.Match(
            id => Results.Created($"/api/v1/products/{id}", new CreateProductResponse(id)),
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> ListProducts(
        ISender sender, CancellationToken ct, int page = 1, int pageSize = 20)
        => (await sender.Send(new ListProductsQuery(page, pageSize), ct)).ToHttpResult();

    private static async Task<IResult> GetProduct(Guid id, ISender sender, CancellationToken ct)
        => (await sender.Send(new GetProductByIdQuery(id), ct)).ToHttpResult();

    private static async Task<IResult> UpdateProduct(
        Guid id, UpdateProductRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (await sender.Send(new UpdateProductCommand(id, request.Name, request.Price), ct))
            .ToNoContentHttpResult();
    }

    private static async Task<IResult> DeleteProduct(Guid id, ISender sender, CancellationToken ct)
        => (await sender.Send(new DeleteProductCommand(id), ct)).ToNoContentHttpResult();
}
