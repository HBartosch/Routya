using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Database;
using Routya.SourceGen.DatabaseDemo.Models;
using Routya.SourceGen.DatabaseDemo.Requests;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class CreateProductCommandHandler : IAsyncRequestHandler<CreateProductCommand, int>
{
    private readonly ProductDbContext _dbContext;

    public CreateProductCommandHandler(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> HandleAsync(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  → Creating product: {request.Name}...");
        
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        var productId = await _dbContext.CreateProductAsync(product);
        Console.WriteLine($"  ✓ Product created with ID: {productId}");
        
        return productId;
    }
}
