using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Threading;

namespace Routya.Core.Dispatchers.Requests
{
    /// <summary>
    /// Dispatches stream requests to their handlers.
    /// </summary>
    public interface IRoutyaStreamDispatcher
    {
        /// <summary>
        /// Dispatches a stream request and returns the sequence of items its handler produces.
        /// </summary>
        /// <typeparam name="TRequest">The type of request implementing <see cref="IStreamRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of each item produced.</typeparam>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Cancellation token to observe while producing items.</param>
        /// <returns>The sequence of items produced by the handler, after any behaviors.</returns>
        IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IStreamRequest<TResponse>;
    }
}
