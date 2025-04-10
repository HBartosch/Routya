using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    public interface IRoutyaRequestDispatcher
    {
        TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>;

        Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>;
    }
}