using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Database;
using Routya.SourceGen.DatabaseDemo.Models;
using Routya.SourceGen.DatabaseDemo.Requests;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class GetProductQueryHandler : IAsyncRequestHandler<GetProductQuery, Product>
{
    private readonly ProductDbContext _dbContext;

    public GetProductQueryHandler(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product> HandleAsync(GetProductQuery request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  → Fetching product with ID {request.ProductId} from database...");
        var product = await _dbContext.GetProductByIdAsync(request.ProductId);
        
        if (product != null)
        {
            Console.WriteLine($"  ✓ Found: {product.Name} - ${product.Price} (Stock: {product.StockQuantity})");
            return product;
        }
        else
        {
            Console.WriteLine($"  ✗ Product not found");
            return new Product { Id = 0, Name = "Not Found", Price = 0, StockQuantity = 0 };
        }
    }
}
