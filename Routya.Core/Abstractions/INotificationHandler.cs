using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Defines a handler for a notification of type <typeparamref name="TNotification"/>.
    /// Multiple handlers can be registered for the same notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    /// <remarks>
    /// <para>Notification handlers enable the publish-subscribe pattern, allowing multiple independent operations to respond to a single event.</para>
    /// <para>Unlike request handlers, multiple notification handlers can be registered for the same notification type.</para>
    /// <para>Handlers should be designed to be independent and not rely on execution order (unless using sequential publishing).</para>
    /// <para>
    /// Performance considerations:
    /// - Sequential publishing: Handlers execute in registration order, ~111ns for Singleton handlers
    /// - Parallel publishing: Handlers execute concurrently, ~144ns for Singleton handlers
    /// - Singleton handlers are ~50% faster than Scoped handlers for notifications
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// public class SendWelcomeEmailHandler : INotificationHandler&lt;UserRegisteredNotification&gt;
    /// {
    ///     private readonly IEmailService _emailService;
    ///     
    ///     public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    ///     {
    ///         await _emailService.SendWelcomeEmailAsync(notification.Email, cancellationToken);
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the notification asynchronously.
        /// </summary>
        /// <param name="notification">The notification to handle.</param>
        /// <param name="cancellationToken">Cancellation token to observe for cooperative cancellation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }
}