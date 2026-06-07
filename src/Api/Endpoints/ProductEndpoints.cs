using Mediator;
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

        group.MapPost("/", async (CreateProductCommand command, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/products/{id}", new { id });
        });

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListProductsQuery(), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            ProductDto? product = await sender.Send(new GetProductByIdQuery(id), ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductBody body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateProductCommand(id, body.Name, body.Price), ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteProductCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization("TenantAdmin");

        return app;
    }

    public sealed record UpdateProductBody(string Name, decimal Price);
}
