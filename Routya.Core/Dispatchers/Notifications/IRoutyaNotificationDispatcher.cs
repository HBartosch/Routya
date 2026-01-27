using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Notifications
{
    /// <summary>
    /// Internal dispatcher interface for publishing notifications to handlers.
    /// </summary>
    /// <remarks>
    /// This is an internal abstraction used by <see cref="Dispatchers.DefaultRoutya"/>. 
    /// Application code should use <see cref="IRoutya"/> instead.
    /// </remarks>
    public interface IRoutyaNotificationDispatcher
    {
        /// <summary>
        /// Publishes a notification to all its handlers using the specified dispatch strategy.
        /// </summary>
        /// <typeparam name="TNotification">The type of the notification.</typeparam>
        /// <param name="notification">The notification instance to publish.</param>
        /// <param name="strategy">The dispatch strategy: sequential or parallel execution.</param>
        /// <param name="cancellationToken">Cancellation token to observe.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishAsync<TNotification>(
            TNotification notification,
            NotificationDispatchStrategy strategy = NotificationDispatchStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }
}