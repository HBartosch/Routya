using Routya.Core.Abstractions;

namespace Routya.SourceGen.DatabaseDemo.Requests;

public class CreateProductCommand : IRequest<int>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
