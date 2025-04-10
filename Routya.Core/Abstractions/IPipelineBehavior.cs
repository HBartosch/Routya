using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Abstractions
{
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

    public interface IPipelineBehavior<TRequest, TResponse>
    {
        Task<TResponse> Handle(
        TRequest request,        
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
    }
}