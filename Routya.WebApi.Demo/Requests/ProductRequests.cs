using Routya.Core.Abstractions;
using Routya.WebApi.Demo.Models;

namespace Routya.WebApi.Demo.Requests;

// Request with Singleton Handler
public class CreateProductRequest : IRequest<Product>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

// Request with Scoped Handler
public class GetProductRequest : IRequest<Product?>
{
    public int Id { get; set; }
}

// Request with Transient Handler
public class GetAllProductsRequest : IRequest<List<Product>>
{
}

// Request with Singleton Handler
public class UpdateProductStockRequest : IRequest<Product?>
{
    public int Id { get; set; }
    public int NewStock { get; set; }
}

// Request with Scoped Handler
public class DeleteProductRequest : IRequest<bool>
{
    public int Id { get; set; }
}
