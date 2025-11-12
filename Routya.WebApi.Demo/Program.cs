using Microsoft.EntityFrameworkCore;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using Routya.WebApi.Demo.Data;
using Routya.WebApi.Demo.Handlers;
using Routya.WebApi.Demo.Models;
using Routya.WebApi.Demo.Notifications;
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
// NOTE: Using manual registration to demonstrate different lifetimes

// Register Request Handlers
builder.Services.AddRoutyaAsyncRequestHandler<CreateProductRequest, Product, CreateProductHandler>(ServiceLifetime.Singleton);
builder.Services.AddRoutyaAsyncRequestHandler<UpdateProductStockRequest, Product?, UpdateProductStockHandler>(ServiceLifetime.Singleton);
builder.Services.AddRoutyaAsyncRequestHandler<GetProductRequest, Product?, GetProductHandler>(ServiceLifetime.Scoped);
builder.Services.AddRoutyaAsyncRequestHandler<DeleteProductRequest, bool, DeleteProductHandler>(ServiceLifetime.Scoped);
builder.Services.AddRoutyaAsyncRequestHandler<GetAllProductsRequest, List<Product>, GetAllProductsHandler>(ServiceLifetime.Transient);

// Register Notification Handlers with different lifetimes
builder.Services.AddRoutyaNotificationHandler<UserCreatedNotification, LoggingNotificationHandler>(ServiceLifetime.Singleton);
builder.Services.AddRoutyaNotificationHandler<UserCreatedNotification, EmailNotificationHandler>(ServiceLifetime.Scoped);
builder.Services.AddRoutyaNotificationHandler<UserCreatedNotification, MetricsNotificationHandler>(ServiceLifetime.Transient);

// Configure Routya core services (dispatcher, etc.) with Scoped dispatch mode
builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);

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
