using System.Collections.Concurrent;

namespace Routya.WebApi.SourceGen.Demo;

public sealed class ProductStore
{
    private int _nextId = 21;
    private readonly ConcurrentDictionary<int, Product> _products;

    public ProductStore()
    {
        _products = new ConcurrentDictionary<int, Product>(
            Enumerable.Range(1, 20).Select(i => new Product(i, $"Product {i}", i * 9.99m, i * 10))
                      .ToDictionary(p => p.Id));
    }

    public Product? GetById(int id) => _products.GetValueOrDefault(id);

    public IReadOnlyList<Product> GetAll() => [.. _products.Values.OrderBy(p => p.Id)];

    public Product Add(string name, decimal price, int stock)
    {
        var id = Interlocked.Increment(ref _nextId);
        var product = new Product(id, name, price, stock);
        _products[id] = product;
        return product;
    }

    public IAsyncEnumerable<Product> StreamAllAsync(CancellationToken ct = default)
        => StreamCore(ct);

    private async IAsyncEnumerable<Product> StreamCore(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var product in GetAll())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(50, ct);
            yield return product;
        }
    }
}
