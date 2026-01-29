using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Models;

namespace Routya.SourceGen.DatabaseDemo.Requests;

public class GetAllProductsQuery : IRequest<List<Product>>
{
}
