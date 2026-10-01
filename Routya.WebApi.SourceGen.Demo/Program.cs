using Routya.Generated;
using Routya.Core.Abstractions;
using Routya.WebApi.SourceGen.Demo;
using Routya.WebApi.SourceGen.Demo.Behaviors;
using Routya.WebApi.SourceGen.Demo.Handlers;
using Routya.WebApi.SourceGen.Demo.Application;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ProductStore>();

// Pipeline behavior — registered before AddGeneratedRoutya so it is resolved first
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Notification handlers
builder.Services.AddTransient<INotificationHandler<OrderShippedNotification>, EmailNotificationHandler>();
builder.Services.AddTransient<INotificationHandler<OrderShippedNotification>, WarehouseNotificationHandler>();

// Source-generated dispatcher — wires all handlers at compile time, no reflection.
//
// This registers handlers from BOTH assemblies: the ones in this project, and the ones in
// Routya.WebApi.SourceGen.Demo.Application, which the generator finds by reading that assembly's
// metadata. Nothing has to be listed here.
//
// Scoped is passed explicitly, giving one handler instance per HTTP request. That is safe in
// ASP.NET Core because endpoints resolve from the request scope. Outside a request, for example in
// a hosted service, a Scoped handler needs a scope creating first, so the default is Transient.
builder.Services.AddGeneratedRoutya(ServiceLifetime.Scoped);

var app = builder.Build();

// ── Handlers from the referenced Application assembly ───────────────────────

// GET /customers/{id} — dispatched to a handler in another project entirely.
// Call it twice and the instanceId differs, because each HTTP request is its own scope.
app.MapGet("/customers/{id:int}", async (int id, IGeneratedRoutya routya, CancellationToken ct) =>
    Results.Ok(await routya.SendAsync(new GetCustomerQuery(id), ct)));

// GET /status — handler pinned to Singleton with [RoutyaHandler], overriding the Scoped above.
// Call it repeatedly and the instance never changes, unlike /customers.
app.MapGet("/status", async (IGeneratedRoutya routya, CancellationToken ct) =>
    Results.Ok(new { status = await routya.SendAsync(new GetServiceStatusQuery(), ct) }));

// POST /customers/{id}/registered — notification handled in the referenced assembly
app.MapPost("/customers/{id:int}/registered", async (int id, IGeneratedRoutya routya, CancellationToken ct) =>
{
    await routya.PublishAsync(new CustomerRegistered(id), ct);
    return Results.Accepted();
});


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

// GET /products/stream — stream all products one by one via IStreamRequest<T>.
// ASP.NET Core natively streams IAsyncEnumerable<T> returned from a route handler, so the
// generated CreateStream result can be returned directly with nothing buffered.
app.MapGet("/products/stream", (IGeneratedRoutya routya, CancellationToken ct)
    => routya.CreateStream(new ExportProductsQuery(), ct));

// POST /orders/{id}/shipped — fan-out notification to multiple handlers
app.MapPost("/orders/{id:int}/shipped", async (int id, ShipOrderRequest body, IGeneratedRoutya routya, CancellationToken ct) =>
{
    await routya.PublishAsync(new OrderShippedNotification(id, body.CustomerId), ct);
    return Results.NoContent();
});

app.Run();

record CreateProductRequest(string Name, decimal Price, int Stock);
record ShipOrderRequest(int CustomerId);
