using Microsoft.EntityFrameworkCore;
using Routya.Core.Abstractions;
using Routya.WebApi.Demo.Data;
using Routya.WebApi.Demo.Models;
using Routya.WebApi.Demo.Requests;

namespace Routya.WebApi.Demo.Handlers;

// SINGLETON HANDLER - Fastest, single shared instance across all requests
// Use for stateless operations that don't require per-request or per-scope state
// Note: Uses IAsyncRequestHandler for async database operations with IServiceProvider pattern
public class CreateProductHandler : IAsyncRequestHandler<CreateProductRequest, Product>
{
    private readonly IServiceProvider _serviceProvider;

    public CreateProductHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Product> HandleAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        // Create scope to get DbContext (can't inject DbContext directly in Singleton)
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            Stock = request.Stock,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }
}

// SCOPED HANDLER - One instance per HTTP request (most common pattern)
// Ideal for handlers that need to share state within a single request
public class GetProductHandler : IAsyncRequestHandler<GetProductRequest, Product?>
{
    private readonly AppDbContext _dbContext;

    public GetProductHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> HandleAsync(GetProductRequest request, CancellationToken cancellationToken)
    {
        return await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
    }
}

// TRANSIENT HANDLER - New instance every time (most isolated)
// Use for handlers that maintain state that shouldn't be shared
public class GetAllProductsHandler : IAsyncRequestHandler<GetAllProductsRequest, List<Product>>
{
    private readonly AppDbContext _dbContext;

    public GetAllProductsHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Product>> HandleAsync(GetAllProductsRequest request, CancellationToken cancellationToken)
    {
        return await _dbContext.Products
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}

// SINGLETON HANDLER - Demonstrate another Singleton pattern
public class UpdateProductStockHandler : IAsyncRequestHandler<UpdateProductStockRequest, Product?>
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateProductStockHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Product?> HandleAsync(UpdateProductStockRequest request, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var product = await dbContext.Products.FindAsync(new object[] { request.Id }, cancellationToken);
        if (product == null)
            return null;

        product.Stock = request.NewStock;
        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }
}

// SCOPED HANDLER - Another Scoped example
public class DeleteProductHandler : IAsyncRequestHandler<DeleteProductRequest, bool>
{
    private readonly AppDbContext _dbContext;

    public DeleteProductHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { request.Id }, cancellationToken);
        if (product == null)
            return false;

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
