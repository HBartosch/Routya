using System.Threading;
using System.Threading.Tasks;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Core.Dispatchers
{
    public class DefaultRoutya : IRoutya
    {
        private readonly IRoutyaRequestDispatcher _requestDispatcher;
        private readonly IRoutyaNotificationDispatcher _notificationDispatcher;

        public DefaultRoutya(
            IRoutyaRequestDispatcher requestDispatcher,
            IRoutyaNotificationDispatcher notificationDispatcher)
        {
            _requestDispatcher = requestDispatcher;
            _notificationDispatcher = notificationDispatcher;
        }

        public Task PublishAsync<TNotification>(
            TNotification notification, 
            CancellationToken cancellationToken = default) 
                where TNotification : INotification
        {
            return _notificationDispatcher.PublishAsync(notification, NotificationDispatchStrategy.Sequential, cancellationToken);
        }

        public Task PublishParallelAsync<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
                where TNotification : INotification
        {
            return _notificationDispatcher.PublishAsync(notification, NotificationDispatchStrategy.Parallel, cancellationToken);
        }

        public TResponse Send<TRequest, TResponse>(TRequest request) 
            where TRequest : IRequest<TResponse>
        {
            return _requestDispatcher.Send<TRequest, TResponse>(request);
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, 
            CancellationToken cancellationToken = default) 
                where TRequest : IRequest<TResponse>
        {
            return _requestDispatcher.SendAsync<TRequest, TResponse>(request, cancellationToken);
        }
    }
}
