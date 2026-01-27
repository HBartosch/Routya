namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Marker interface for notification messages that can be published to multiple handlers.
    /// Implement this interface on your notification objects to enable them to be published via <see cref="IRoutya"/>.
    /// </summary>
    /// <remarks>
    /// <para>Notifications follow the publish-subscribe pattern, allowing multiple handlers to respond to a single notification.</para>
    /// <para>Unlike requests, notifications can have zero, one, or many handlers registered.</para>
    /// <para>Notification objects should be immutable and contain all data needed by the handlers.</para>
    /// <para>
    /// Use cases:
    /// - Domain events (e.g., OrderCreated, UserRegistered)
    /// - Cross-cutting concerns (e.g., logging, caching, analytics)
    /// - Side effects that don't affect the primary operation's result
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// public class UserRegisteredNotification : INotification
    /// {
    ///     public string Email { get; init; }
    ///     public DateTime RegisteredAt { get; init; }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface INotification { }
}