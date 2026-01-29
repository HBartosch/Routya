using Routya.Core.Abstractions;

namespace Routya.SourceGen.DatabaseDemo.Requests;

public class UpdateStockCommand : IRequest<bool>
{
    public int ProductId { get; set; }
    public int NewQuantity { get; set; }
}
