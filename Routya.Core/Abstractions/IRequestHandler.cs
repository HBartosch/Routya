namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Defines a synchronous handler for a request of type <typeparamref name="TRequest"/> that produces a response of type <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response to return.</typeparam>
    /// <remarks>
    /// <para>Use this interface for synchronous, CPU-bound operations that don't require async I/O.</para>
    /// <para>For async operations, use <see cref="IAsyncRequestHandler{TRequest, TResponse}"/> instead.</para>
    /// <para>Each request type should have exactly one handler implementation registered in the DI container.</para>
    /// <para>
    /// Performance: Synchronous handlers are ~10% faster than async handlers for simple operations (~334ns vs ~398ns).
    /// However, pipeline behaviors will still execute asynchronously.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// public class GetUserByIdHandler : IRequestHandler&lt;GetUserByIdRequest, User&gt;
    /// {
    ///     public User Handle(GetUserByIdRequest request)
    ///     {
    ///         // Synchronous processing logic
    ///         return new User { Id = request.UserId };
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the request synchronously and returns the response.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <returns>The response produced by handling the request.</returns>
        TResponse Handle(TRequest request);
    }
}