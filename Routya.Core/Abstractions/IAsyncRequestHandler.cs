using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    public interface IAsyncRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
    }
}