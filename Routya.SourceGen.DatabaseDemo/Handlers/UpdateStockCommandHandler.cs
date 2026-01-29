using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Database;
using Routya.SourceGen.DatabaseDemo.Requests;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class UpdateStockCommandHandler : IAsyncRequestHandler<UpdateStockCommand, bool>
{
    private readonly ProductDbContext _dbContext;

    public UpdateStockCommandHandler(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HandleAsync(UpdateStockCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  → Updating stock for product ID {request.ProductId} to {request.NewQuantity}...");
        var success = await _dbContext.UpdateProductStockAsync(request.ProductId, request.NewQuantity);
        
        if (success)
        {
            Console.WriteLine($"  ✓ Stock updated successfully");
        }
        else
        {
            Console.WriteLine($"  ✗ Failed to update stock (product not found)");
        }

        return success;
    }
}
