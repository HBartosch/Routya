using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Database;
using Routya.SourceGen.DatabaseDemo.Models;
using Routya.SourceGen.DatabaseDemo.Requests;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class GetAllProductsQueryHandler : IAsyncRequestHandler<GetAllProductsQuery, List<Product>>
{
    private readonly ProductDbContext _dbContext;

    public GetAllProductsQueryHandler(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Product>> HandleAsync(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        Console.WriteLine("  → Fetching all products from database...");
        var products = await _dbContext.GetAllProductsAsync();
        Console.WriteLine($"  ✓ Found {products.Count} products");
        return products;
    }
}
