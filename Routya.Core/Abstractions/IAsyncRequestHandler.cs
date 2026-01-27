using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Defines an asynchronous handler for a request of type <typeparamref name="TRequest"/> that produces a response of type <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response to return.</typeparam>
    /// <remarks>
    /// <para>Use this interface for async operations involving I/O, database access, or other awaitable operations.</para>
    /// <para>For purely synchronous operations, consider using <see cref="IRequestHandler{TRequest, TResponse}"/> for better performance.</para>
    /// <para>Each request type should have exactly one async handler implementation registered in the DI container.</para>
    /// <para>When both <see cref="IAsyncRequestHandler{TRequest, TResponse}"/> and <see cref="IRequestHandler{TRequest, TResponse}"/> are registered, the async handler takes precedence.</para>
    /// <para>
    /// Example:
    /// <code>
    /// public class GetUserByIdHandler : IAsyncRequestHandler&lt;GetUserByIdRequest, User&gt;
    /// {
    ///     private readonly IUserRepository _repository;
    ///     
    ///     public async Task&lt;User&gt; HandleAsync(GetUserByIdRequest request, CancellationToken cancellationToken)
    ///     {
    ///         return await _repository.GetByIdAsync(request.UserId, cancellationToken);
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IAsyncRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request asynchronously and returns the response.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="cancellationToken">Cancellation token to observe for cooperative cancellation.</param>
        /// <returns>A task representing the asynchronous operation, containing the response produced by handling the request.</returns>
        Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }
}