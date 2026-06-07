using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Contracts;
using Saas.Application.Products;
using Saas.Application.Products.CreateProduct;
using Saas.Application.Products.DeleteProduct;
using Saas.Application.Products.GetProductById;
using Saas.Application.Products.ListProducts;
using Saas.Application.Products.UpdateProduct;

namespace Saas.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/products").WithTags("Products")
            .RequireAuthorization("HasTenant");

        group.MapPost("/", async Task<Results<Created<CreateProductResponse>, ValidationProblem>> (
            CreateProductCommand command, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return TypedResults.Created($"/products/{id}", new CreateProductResponse(id));
        }).Produces<CreateProductResponse>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", async Task<Ok<IReadOnlyList<ProductDto>>> (ISender sender, CancellationToken ct) =>
            TypedResults.Ok(await sender.Send(new ListProductsQuery(), ct)))
          .Produces<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async Task<Results<Ok<ProductDto>, NotFound>> (
            Guid id, ISender sender, CancellationToken ct) =>
        {
            ProductDto? product = await sender.Send(new GetProductByIdQuery(id), ct);
            return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
        }).Produces<ProductDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id, UpdateProductBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateProductCommand(id, body.Name, body.Price), ct);
            return TypedResults.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteProductCommand(id), ct);
            return TypedResults.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound)
          .RequireAuthorization("TenantAdmin");

        return app;
    }
}
