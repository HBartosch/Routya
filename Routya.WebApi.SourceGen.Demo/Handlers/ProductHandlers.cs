using Routya.Core.Abstractions;

namespace Routya.WebApi.SourceGen.Demo.Handlers;

public record GetProductQuery(int Id) : IRequest<Product?>;
public record GetAllProductsQuery : IRequest<IReadOnlyList<Product>>;
public record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<Product>;

// Streaming workaround: IRequest<IAsyncEnumerable<T>> returns a Task<IAsyncEnumerable<T>>.
// The caller awaits the task to get the enumerable, then streams it.
// First-class IStreamRequest<T> support is planned for a future release.
public record ExportProductsQuery : IRequest<IAsyncEnumerable<Product>>;

public class GetProductHandler(ProductStore store) : IAsyncRequestHandler<GetProductQuery, Product?>
{
    public Task<Product?> HandleAsync(GetProductQuery request, CancellationToken ct)
        => Task.FromResult(store.GetById(request.Id));
}

public class GetAllProductsHandler(ProductStore store) : IAsyncRequestHandler<GetAllProductsQuery, IReadOnlyList<Product>>
{
    public Task<IReadOnlyList<Product>> HandleAsync(GetAllProductsQuery request, CancellationToken ct)
        => Task.FromResult(store.GetAll());
}

public class CreateProductHandler(ProductStore store) : IAsyncRequestHandler<CreateProductCommand, Product>
{
    public Task<Product> HandleAsync(CreateProductCommand request, CancellationToken ct)
        => Task.FromResult(store.Add(request.Name, request.Price, request.Stock));
}

public class ExportProductsHandler(ProductStore store) : IAsyncRequestHandler<ExportProductsQuery, IAsyncEnumerable<Product>>
{
    public Task<IAsyncEnumerable<Product>> HandleAsync(ExportProductsQuery request, CancellationToken ct)
        => Task.FromResult(store.StreamAllAsync(ct));
}
