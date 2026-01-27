using System.Threading;
using System.Threading.Tasks;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Core.Dispatchers
{
    /// <summary>
    /// Default implementation of <see cref="IRoutya"/> that delegates to specialized request and notification dispatchers.
    /// </summary>
    /// <remarks>
    /// This class is automatically registered by the <c>AddRoutya</c> extension method and serves as the main entry point for dispatching.
    /// </remarks>
    public class DefaultRoutya : IRoutya
    {
        private readonly IRoutyaRequestDispatcher _requestDispatcher;
        private readonly IRoutyaNotificationDispatcher _notificationDispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultRoutya"/> class.
        /// </summary>
        /// <param name="requestDispatcher">The dispatcher responsible for handling requests.</param>
        /// <param name="notificationDispatcher">The dispatcher responsible for handling notifications.</param>
        public DefaultRoutya(
            IRoutyaRequestDispatcher requestDispatcher,
            IRoutyaNotificationDispatcher notificationDispatcher)
        {
            _requestDispatcher = requestDispatcher;
            _notificationDispatcher = notificationDispatcher;
        }

        /// <inheritdoc />
        public Task PublishAsync<TNotification>(
            TNotification notification, 
            CancellationToken cancellationToken = default) 
                where TNotification : INotification
        {
            return _notificationDispatcher.PublishAsync(notification, NotificationDispatchStrategy.Sequential, cancellationToken);
        }

        /// <inheritdoc />
        public Task PublishParallelAsync<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
                where TNotification : INotification
        {
            return _notificationDispatcher.PublishAsync(notification, NotificationDispatchStrategy.Parallel, cancellationToken);
        }

        /// <inheritdoc />
        public TResponse Send<TRequest, TResponse>(TRequest request) 
            where TRequest : IRequest<TResponse>
        {
            return _requestDispatcher.Send<TRequest, TResponse>(request);
        }

        /// <inheritdoc />
        public Task<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, 
            CancellationToken cancellationToken = default) 
                where TRequest : IRequest<TResponse>
        {
            return _requestDispatcher.SendAsync<TRequest, TResponse>(request, cancellationToken);
        }
    }
}
