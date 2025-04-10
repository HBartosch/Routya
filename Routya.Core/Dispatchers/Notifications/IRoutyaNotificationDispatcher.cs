using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Notifications
{
    public interface IRoutyaNotificationDispatcher
    {
        /// <summary>
        /// Publishes a notification to all its handlers.
        /// </summary>
        /// <typeparam name="TNotification">The type of the notification.</typeparam>
        /// <param name="notification">The notification instance.</param>
        /// <param name="strategy">Dispatch strategy: sequential or parallel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PublishAsync<TNotification>(
            TNotification notification,
            NotificationDispatchStrategy strategy = NotificationDispatchStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }
}