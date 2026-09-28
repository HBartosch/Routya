using System.Collections.Generic;
using System.Threading;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Defines a handler that produces a stream of <typeparamref name="TResponse"/> items for a
    /// request of type <typeparamref name="TRequest"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of each item produced.</typeparam>
    /// <remarks>
    /// <para>
    /// Items are produced lazily, so nothing is buffered and the caller can begin consuming before
    /// the handler has finished. Each request type should have exactly one stream handler.
    /// </para>
    /// <para>
    /// Under <c>RoutyaDispatchScope.Scoped</c> the dispatch scope stays alive for the whole
    /// enumeration and is disposed when enumeration finishes or is abandoned, so a handler may hold
    /// a scoped dependency such as a <c>DbContext</c> across the stream.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// public class ExportProductsHandler : IStreamRequestHandler&lt;ExportProducts, Product&gt;
    /// {
    ///     private readonly AppDbContext _db;
    ///
    ///     public async IAsyncEnumerable&lt;Product&gt; Handle(
    ///         ExportProducts request,
    ///         [EnumeratorCancellation] CancellationToken cancellationToken)
    ///     {
    ///         await foreach (var product in _db.Products.AsAsyncEnumerable().WithCancellation(cancellationToken))
    ///         {
    ///             yield return product;
    ///         }
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IStreamRequestHandler<in TRequest, out TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Handles the request, producing items lazily.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Cancellation token to observe while producing items.</param>
        /// <returns>The sequence of items produced.</returns>
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }
}
