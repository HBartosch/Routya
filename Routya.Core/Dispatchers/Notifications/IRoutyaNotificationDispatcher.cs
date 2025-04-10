using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Notifications
{
    public interface IRoutyaNotificationDispatcher
    {
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;

        Task PublishParallel<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification;
    }
}