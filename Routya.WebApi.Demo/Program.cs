using Microsoft.EntityFrameworkCore;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using Routya.WebApi.Demo.Data;
using Routya.WebApi.Demo.Handlers;
using Routya.WebApi.Demo.Models;
using Routya.WebApi.Demo.Requests;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Server=(localdb)\\mssqllocaldb;Database=RoutyaDemo;Trusted_Connection=True;MultipleActiveResultSets=true"));

// Configure Routya with mixed handler lifetimes demonstration
// NOTE: To demonstrate all three lifetimes, we need to register handlers individually
// because AddRoutya's HandlerLifetime option applies to all handlers uniformly

// Register Singleton handlers (fastest, shared instance)
builder.Services.AddSingleton<IAsyncRequestHandler<CreateProductRequest, Product>, CreateProductHandler>();
builder.Services.AddSingleton<IAsyncRequestHandler<UpdateProductStockRequest, Product?>, UpdateProductStockHandler>();

// Register Scoped handlers (one per HTTP request - most common)
builder.Services.AddScoped<IAsyncRequestHandler<GetProductRequest, Product?>, GetProductHandler>();
builder.Services.AddScoped<IAsyncRequestHandler<DeleteProductRequest, bool>, DeleteProductHandler>();

// Register Transient handlers (new instance every time - most isolated)
builder.Services.AddTransient<IAsyncRequestHandler<GetAllProductsRequest, List<Product>>, GetAllProductsHandler>();

// Configure Routya core services (dispatcher, etc.) without assembly scanning
builder.Services.AddRoutya();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();
