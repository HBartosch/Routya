using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    public interface IRoutya
    {
        TResponse Send<TRequest, TResponse>(TRequest request) 
            where TRequest : IRequest<TResponse>;

        Task<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, 
            CancellationToken cancellationToken = default) 
                where TRequest : IRequest<TResponse>;

        Task PublishAsync<TNotification>(
            TNotification notification, 
            CancellationToken cancellationToken = default)
                where TNotification : INotification;

        Task PublishParallelAsync<TNotification>(
           TNotification notification,
           CancellationToken cancellationToken = default)
               where TNotification : INotification;
    }
}
