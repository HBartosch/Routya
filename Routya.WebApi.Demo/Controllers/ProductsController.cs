using Microsoft.AspNetCore.Mvc;
using Routya.Core.Abstractions;
using Routya.WebApi.Demo.Models;
using Routya.WebApi.Demo.Requests;

namespace Routya.WebApi.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IRoutya _routya;

    public ProductsController(IRoutya routya)
    {
        _routya = routya;
    }

    /// <summary>
    /// Create a new product (SINGLETON handler)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(CreateProductRequest request)
    {
        var product = await _routya.SendAsync<CreateProductRequest, Product>(request);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>
    /// Get a product by ID (SCOPED handler)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _routya.SendAsync<GetProductRequest, Product?>(new GetProductRequest { Id = id });
        
        if (product == null)
            return NotFound();

        return Ok(product);
    }

    /// <summary>
    /// Get all products (TRANSIENT handler)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAllProducts()
    {
        var products = await _routya.SendAsync<GetAllProductsRequest, List<Product>>(new GetAllProductsRequest());
        return Ok(products);
    }

    /// <summary>
    /// Update product stock (SINGLETON handler)
    /// </summary>
    [HttpPut("{id}/stock")]
    public async Task<ActionResult<Product>> UpdateStock(int id, [FromBody] int newStock)
    {
        var product = await _routya.SendAsync<UpdateProductStockRequest, Product?>(new UpdateProductStockRequest 
        { 
            Id = id, 
            NewStock = newStock 
        });

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    /// <summary>
    /// Delete a product (SCOPED handler)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProduct(int id)
    {
        var success = await _routya.SendAsync<DeleteProductRequest, bool>(new DeleteProductRequest { Id = id });

        if (!success)
            return NotFound();

        return NoContent();
    }
}
