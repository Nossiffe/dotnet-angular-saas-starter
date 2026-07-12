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

namespace Saas.Api.Endpoints;

public sealed class ProductEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/products").WithTags("Products")
            .RequireAuthorization("HasTenant");

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
            .RequireAuthorization("TenantAdmin");
    }

    private static async Task<IResult> CreateProduct(
        CreateProductCommand command, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await sender.Send(command, ct);
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
        Guid id, UpdateProductBody body, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        return (await sender.Send(new UpdateProductCommand(id, body.Name, body.Price), ct))
            .ToNoContentHttpResult();
    }

    private static async Task<IResult> DeleteProduct(Guid id, ISender sender, CancellationToken ct)
        => (await sender.Send(new DeleteProductCommand(id), ct)).ToNoContentHttpResult();
}
