using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Main entry point for dispatching requests and notifications in the Routya library.
    /// Provides methods for both synchronous and asynchronous request dispatching, as well as sequential and parallel notification publishing.
    /// </summary>
    public interface IRoutya
    {
        /// <summary>
        /// Synchronously sends a request and returns the response.
        /// Uses registry-based dispatch for optimal performance when handlers are registered via AddRoutyaRequestHandler.
        /// </summary>
        /// <typeparam name="TRequest">The type of request implementing <see cref="IRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of response expected from the request handler.</typeparam>
        /// <param name="request">The request object to be handled.</param>
        /// <returns>The response from the request handler.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <remarks>
        /// Allocates about 104 bytes per dispatch with a Singleton handler and no behaviors.
        /// Requires either <see cref="IRequestHandler{TRequest, TResponse}"/> to be registered.
        /// Pipeline behaviors are executed asynchronously even for synchronous handlers.
        /// </remarks>
        TResponse Send<TRequest, TResponse>(TRequest request) 
            where TRequest : IRequest<TResponse>;

        /// <summary>
        /// Asynchronously sends a request and returns the response.
        /// Supports cancellation and pipeline behaviors. Uses registry-based dispatch for optimal performance.
        /// </summary>
        /// <typeparam name="TRequest">The type of request implementing <see cref="IRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of response expected from the request handler.</typeparam>
        /// <param name="request">The request object to be handled.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the response from the request handler.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        /// <exception cref="System.OperationCanceledException">Thrown when the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Allocates about 208 bytes per dispatch with a Singleton handler, or about 544 bytes
        /// with a Scoped handler, in both cases with no behaviors.
        /// Prefers <see cref="IAsyncRequestHandler{TRequest, TResponse}"/> if registered, falls back to <see cref="IRequestHandler{TRequest, TResponse}"/> wrapped in Task.
        /// All pipeline behaviors receive the cancellation token for proper cooperative cancellation.
        /// </remarks>
        Task<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, 
            CancellationToken cancellationToken = default) 
                where TRequest : IRequest<TResponse>;

        /// <summary>
        /// Asynchronously publishes a notification to all registered handlers in sequential order.
        /// Handlers are executed one after another, waiting for each to complete before starting the next.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification implementing <see cref="INotification"/>.</typeparam>
        /// <param name="notification">The notification object to be published.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that completes when all handlers have executed.</returns>
        /// <exception cref="System.OperationCanceledException">Thrown when the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// Sequential execution ensures handlers run in registration order.
        /// If a handler throws an exception, subsequent handlers will not execute.
        /// <para>
        /// Under <c>RoutyaDispatchScope.Scoped</c>, all handlers share a single dispatch scope. That
        /// is safe here because handlers run one after another, and it lets a later handler commit
        /// work an earlier one staged through a shared scoped dependency such as a unit of work.
        /// This differs from <see cref="PublishParallelAsync{TNotification}(TNotification, CancellationToken)"/>,
        /// which gives each handler its own scope.
        /// </para>
        /// Use <see cref="PublishParallelAsync{TNotification}(TNotification, CancellationToken)"/> for independent, concurrent handler execution.
        /// </remarks>
        Task PublishAsync<TNotification>(
            TNotification notification, 
            CancellationToken cancellationToken = default)
                where TNotification : INotification;

        /// <summary>
        /// Asynchronously publishes a notification to all registered handlers in parallel.
        /// All handlers are executed concurrently, improving performance when handlers are independent.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification implementing <see cref="INotification"/>.</typeparam>
        /// <param name="notification">The notification object to be published.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that completes when all handlers have executed.</returns>
        /// <exception cref="System.OperationCanceledException">Thrown when the operation is canceled via the cancellation token.</exception>
        /// <exception cref="System.AggregateException">Thrown when one or more handlers throw exceptions during parallel execution.</exception>
        /// <remarks>
        /// Parallel execution provides better performance for I/O-bound or independent operations.
        /// If any handler throws an exception, it will be wrapped in an AggregateException along with other failures.
        /// Execution order is non-deterministic. Use <see cref="PublishAsync{TNotification}(TNotification, CancellationToken)"/> if order matters.
        /// <para>
        /// Under <c>RoutyaDispatchScope.Scoped</c>, each handler gets its own dispatch scope. Handlers
        /// run concurrently, so sharing one scope would hand the same scoped dependency to all of them
        /// at once, and types such as <c>DbContext</c> do not tolerate concurrent use. Handlers
        /// therefore cannot share scoped state with one another here; use
        /// <see cref="PublishAsync{TNotification}(TNotification, CancellationToken)"/> if they need to.
        /// </para>
        /// <para>
        /// The extra scopes cost roughly 300 bytes per additional handler compared with sequential
        /// publishing.
        /// </para>
        /// </remarks>
        Task PublishParallelAsync<TNotification>(
           TNotification notification,
           CancellationToken cancellationToken = default)
               where TNotification : INotification;

        /// <summary>
        /// Dispatches a stream request and returns the sequence of items its handler produces.
        /// </summary>
        /// <typeparam name="TRequest">The type of request implementing <see cref="IStreamRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of each item produced.</typeparam>
        /// <param name="request">The request to dispatch.</param>
        /// <param name="cancellationToken">Cancellation token to observe while producing items.</param>
        /// <returns>The sequence of items produced by the handler, after any registered behaviors.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when no stream handler is registered for the request type.</exception>
        /// <remarks>
        /// <para>
        /// Items are produced lazily. Nothing runs until the returned sequence is enumerated, and
        /// nothing is buffered, so a consumer can begin processing before the handler has finished.
        /// </para>
        /// <para>
        /// Registered <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> instances wrap the
        /// whole enumeration, so they observe every item and any exception thrown part way through.
        /// This is the difference from declaring <c>IRequest&lt;IAsyncEnumerable&lt;T&gt;&gt;</c>,
        /// where the pipeline finishes as soon as the sequence is handed over.
        /// </para>
        /// <para>
        /// Under <c>RoutyaDispatchScope.Scoped</c>, the dispatch scope lives for the duration of the
        /// enumeration and is disposed when it completes or is abandoned.
        /// </para>
        /// </remarks>
        IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
                where TRequest : IStreamRequest<TResponse>;
    }
}
