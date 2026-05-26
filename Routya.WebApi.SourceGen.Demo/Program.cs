using Routya.Generated;
using Routya.Core.Abstractions;
using Routya.WebApi.SourceGen.Demo;
using Routya.WebApi.SourceGen.Demo.Behaviors;
using Routya.WebApi.SourceGen.Demo.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ProductStore>();

// Pipeline behavior — registered before AddGeneratedRoutya so it is resolved first
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Notification handlers
builder.Services.AddTransient<INotificationHandler<OrderShippedNotification>, EmailNotificationHandler>();
builder.Services.AddTransient<INotificationHandler<OrderShippedNotification>, WarehouseNotificationHandler>();

// Source-generated dispatcher — wires all handlers at compile time, no reflection
builder.Services.AddGeneratedRoutya();

var app = builder.Build();

// GET /products — list all products
app.MapGet("/products", async (IGeneratedRoutya routya, CancellationToken ct) =>
{
    var products = await routya.SendAsync(new GetAllProductsQuery(), ct);
    return Results.Ok(products);
});

// GET /products/{id} — get a single product
app.MapGet("/products/{id:int}", async (int id, IGeneratedRoutya routya, CancellationToken ct) =>
{
    var product = await routya.SendAsync(new GetProductQuery(id), ct);
    return product is null ? Results.NotFound() : Results.Ok(product);
});

// POST /products — create a product
app.MapPost("/products", async (CreateProductRequest body, IGeneratedRoutya routya, CancellationToken ct) =>
{
    var product = await routya.SendAsync(new CreateProductCommand(body.Name, body.Price, body.Stock), ct);
    return Results.Created($"/products/{product.Id}", product);
});

// GET /products/stream — stream all products one by one (streaming workaround via IRequest<IAsyncEnumerable<T>>)
// ASP.NET Core natively streams IAsyncEnumerable<T> returned from a route handler.
app.MapGet("/products/stream", async (IGeneratedRoutya routya, CancellationToken ct) =>
{
    // Await the task to get the IAsyncEnumerable, then return it — ASP.NET Core streams the rest.
    var stream = await routya.SendAsync(new ExportProductsQuery(), ct);
    return stream;
});

// POST /orders/{id}/shipped — fan-out notification to multiple handlers
app.MapPost("/orders/{id:int}/shipped", async (int id, ShipOrderRequest body, IGeneratedRoutya routya, CancellationToken ct) =>
{
    await routya.PublishAsync(new OrderShippedNotification(id, body.CustomerId), ct);
    return Results.NoContent();
});

app.Run();

record CreateProductRequest(string Name, decimal Price, int Stock);
record ShipOrderRequest(int CustomerId);
