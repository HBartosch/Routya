using System.Threading;
using System.Threading.Tasks;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Core.Dispatchers
{
    public class DefaultRoutya : IRoutya
    {
        private readonly CompiledRequestInvokerDispatcher _requestDispatcher;
        private readonly CompiledNotificationInvokerDispatcher _notificationDispatcher;

        public DefaultRoutya(
            CompiledRequestInvokerDispatcher requestDispatcher,
            CompiledNotificationInvokerDispatcher notificationDispatcher)
        {
            _requestDispatcher = requestDispatcher;
            _notificationDispatcher = notificationDispatcher;
        }

        public Task PublishAsync<TNotification>(
            TNotification notification, 
            CancellationToken cancellationToken = default) 
                where TNotification : INotification
        {
            return _notificationDispatcher.Publish(notification, cancellationToken);
        }

        public Task PublishParallelAsync<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
                where TNotification : INotification
        {
            return _notificationDispatcher.PublishParallel(notification, cancellationToken);
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
