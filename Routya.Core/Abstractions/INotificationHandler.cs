using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }
}