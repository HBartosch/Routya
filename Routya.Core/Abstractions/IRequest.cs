namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Marker interface for requests that return a response of type <typeparamref name="TResponse"/>.
    /// Implement this interface on your request objects to enable them to be dispatched via <see cref="IRoutya"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of response that will be returned by the handler.</typeparam>
    /// <remarks>
    /// <para>Request objects should be immutable and contain all data needed by the handler.</para>
    /// <para>Each request must have exactly one handler: either <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IAsyncRequestHandler{TRequest, TResponse}"/>.</para>
    /// <para>
    /// Example:
    /// <code>
    /// public class GetUserByIdRequest : IRequest&lt;User&gt;
    /// {
    ///     public int UserId { get; init; }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface IRequest<TResponse> { }
}