using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Common;
using Saas.Api.Contracts;
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
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListProducts)
            .Produces<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetProduct)
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateProduct)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteProduct)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("TenantAdmin");
    }

    private static async Task<Created<CreateProductResponse>> CreateProduct(
        CreateProductCommand command, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var id = await sender.Send(command, ct);
        return TypedResults.Created($"/products/{id}", new CreateProductResponse(id));
    }

    private static async Task<Ok<IReadOnlyList<ProductDto>>> ListProducts(
        ISender sender, CancellationToken ct) =>
        TypedResults.Ok(await sender.Send(new ListProductsQuery(), ct));

    private static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
        Guid id, ISender sender, CancellationToken ct)
    {
        ProductDto? product = await sender.Send(new GetProductByIdQuery(id), ct);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<NoContent> UpdateProduct(
        Guid id, UpdateProductBody body, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateProductCommand(id, body.Name, body.Price), ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteProduct(
        Guid id, ISender sender, CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        return TypedResults.NoContent();
    }
}
