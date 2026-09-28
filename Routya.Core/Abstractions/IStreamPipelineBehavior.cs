using System.Collections.Generic;
using System.Threading;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Delegate representing the next step in a stream pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item produced.</typeparam>
    /// <param name="cancellationToken">Cancellation token to observe while producing items.</param>
    /// <returns>The sequence produced by the next behavior or the handler.</returns>
    public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>(CancellationToken cancellationToken);

    /// <summary>
    /// Defines a pipeline behavior that wraps stream request handling.
    /// Behaviors execute in the order they are registered.
    /// </summary>
    /// <typeparam name="TRequest">The type of request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of each item produced.</typeparam>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="IPipelineBehavior{TRequest, TResponse}"/> wrapped around a request that
    /// returns a sequence, a stream behavior wraps the enumeration itself. It sees every item as it
    /// is produced, observes an exception thrown part way through, and its timing covers the whole
    /// stream rather than just the handover.
    /// </para>
    /// <para>
    /// Example, logging each item and the total:
    /// <code>
    /// public class CountingBehavior&lt;TRequest, TResponse&gt; : IStreamPipelineBehavior&lt;TRequest, TResponse&gt;
    /// {
    ///     public async IAsyncEnumerable&lt;TResponse&gt; Handle(
    ///         TRequest request,
    ///         StreamHandlerDelegate&lt;TResponse&gt; next,
    ///         [EnumeratorCancellation] CancellationToken cancellationToken)
    ///     {
    ///         var count = 0;
    ///
    ///         await foreach (var item in next(cancellationToken).WithCancellation(cancellationToken))
    ///         {
    ///             count++;
    ///             yield return item;
    ///         }
    ///
    ///         Console.WriteLine($"streamed {count} items");
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IStreamPipelineBehavior<in TRequest, TResponse>
    {
        /// <summary>
        /// Handles the request, wrapping the sequence produced by the next behavior or the handler.
        /// </summary>
        /// <param name="request">The request being handled.</param>
        /// <param name="next">The next step in the pipeline. Enumerate it to produce items.</param>
        /// <param name="cancellationToken">Cancellation token to observe while producing items.</param>
        /// <returns>The sequence of items to pass on.</returns>
        /// <remarks>
        /// Enumerate <paramref name="next"/> unless you intend to short circuit the pipeline, for
        /// example by producing a cached sequence instead.
        /// </remarks>
        IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            StreamHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }
}
