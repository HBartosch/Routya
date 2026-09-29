namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Marker interface for requests that produce a stream of <typeparamref name="TResponse"/> items.
    /// Implement this on your request objects to dispatch them via
    /// <see cref="IRoutya.CreateStream{TRequest, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item produced by the handler.</typeparam>
    /// <remarks>
    /// <para>
    /// Use this rather than <c>IRequest&lt;IAsyncEnumerable&lt;T&gt;&gt;</c>. That shape returns a
    /// task that hands back a sequence, so the pipeline completes the moment the sequence is handed
    /// over, before a single item has been produced. A behavior wrapped around it cannot observe
    /// items, cannot catch an exception thrown part way through, and times only the handover.
    /// </para>
    /// <para>
    /// A stream request instead runs its behaviors around the whole enumeration, so
    /// <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> sees every item and any failure.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// public class ExportProducts : IStreamRequest&lt;Product&gt;
    /// {
    ///     public int CategoryId { get; init; }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IStreamRequest<out TResponse> { }
}
