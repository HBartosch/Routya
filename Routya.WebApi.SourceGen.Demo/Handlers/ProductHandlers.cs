using Routya.Core.Abstractions;

namespace Routya.WebApi.SourceGen.Demo.Handlers;

public record GetProductQuery(int Id) : IRequest<Product?>;
public record GetAllProductsQuery : IRequest<IReadOnlyList<Product>>;
public record CreateProductCommand(string Name, decimal Price, int Stock) : IRequest<Product>;

// First class streaming. IStreamRequest<T> produces items lazily, and any registered
// IStreamPipelineBehavior wraps the whole enumeration rather than just the handover.
public record ExportProductsQuery : IStreamRequest<Product>;

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

public class ExportProductsHandler(ProductStore store) : IStreamRequestHandler<ExportProductsQuery, Product>
{
    public IAsyncEnumerable<Product> Handle(ExportProductsQuery request, CancellationToken ct)
        => store.StreamAllAsync(ct);
}
