using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Delegate representing the next step in the request pipeline.
    /// Call this delegate to continue execution to the next behavior or the final request handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response that will be returned.</typeparam>
    /// <param name="cancellationToken">Cancellation token to observe for cooperative cancellation.</param>
    /// <returns>A task representing the asynchronous operation, containing the response from the handler or next behavior.</returns>
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);

    /// <summary>
    /// Defines a pipeline behavior that wraps around request handling to provide cross-cutting concerns.
    /// Behaviors execute in the order they are registered in the DI container.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
    /// <remarks>
    /// <para>Pipeline behaviors enable the decorator pattern for request handling, allowing you to:</para>
    /// <list type="bullet">
    /// <item><description>Log requests and responses</description></item>
    /// <item><description>Validate requests before handling</description></item>
    /// <item><description>Handle exceptions and errors</description></item>
    /// <item><description>Add caching, retry logic, or circuit breakers</description></item>
    /// <item><description>Measure performance and collect metrics</description></item>
    /// <item><description>Enforce authorization or authentication</description></item>
    /// </list>
    /// <para>Behaviors form a pipeline where each behavior can execute logic before and after the next behavior or handler.</para>
    /// <para>Register behaviors in the desired execution order using <c>services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(YourBehavior&lt;,&gt;))</c>.</para>
    /// <para>
    /// Example:
    /// <code>
    /// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
    /// {
    ///     private readonly ILogger _logger;
    ///     
    ///     public async Task&lt;TResponse&gt; Handle(
    ///         TRequest request,
    ///         RequestHandlerDelegate&lt;TResponse&gt; next,
    ///         CancellationToken cancellationToken)
    ///     {
    ///         _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
    ///         var response = await next(cancellationToken);
    ///         _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
    ///         return response;
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IPipelineBehavior<TRequest, TResponse>
    {
        /// <summary>
        /// Handles the request by executing cross-cutting logic before and/or after calling the next behavior or handler.
        /// </summary>
        /// <param name="request">The request being handled.</param>
        /// <param name="next">The next step in the pipeline. Call this to continue to the next behavior or the final handler.</param>
        /// <param name="cancellationToken">Cancellation token to observe for cooperative cancellation.</param>
        /// <returns>A task representing the asynchronous operation, containing the response from the handler.</returns>
        /// <remarks>
        /// Always call <paramref name="next"/> unless you intend to short-circuit the pipeline (e.g., returning a cached response).
        /// Pass the <paramref name="cancellationToken"/> to <paramref name="next"/> to enable proper cancellation throughout the pipeline.
        /// </remarks>
        Task<TResponse> Handle(
        TRequest request,        
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
    }
}