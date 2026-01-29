using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Models;

namespace Routya.SourceGen.DatabaseDemo.Requests;

public class GetProductQuery : IRequest<Product>
{
    public int ProductId { get; set; }
}
