using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    /// <summary>
    /// Internal dispatcher interface for routing requests to their handlers.
    /// </summary>
    /// <remarks>
    /// This is an internal abstraction used by <see cref="Dispatchers.DefaultRoutya"/>.
    /// Application code should use <see cref="IRoutya"/> instead.
    /// </remarks>
    public interface IRoutyaRequestDispatcher
    {
        /// <summary>
        /// Synchronously sends a request to its handler and returns the response.
        /// </summary>
        /// <typeparam name="TRequest">The type of request.</typeparam>
        /// <typeparam name="TResponse">The type of response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <returns>The response from the handler.</returns>
        TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>;

        /// <summary>
        /// Asynchronously sends a request to its handler and returns the response.
        /// </summary>
        /// <typeparam name="TRequest">The type of request.</typeparam>
        /// <typeparam name="TResponse">The type of response.</typeparam>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancellation token to observe.</param>
        /// <returns>A task representing the asynchronous operation containing the response.</returns>
        Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>;
    }
}