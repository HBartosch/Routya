using Routya.Core.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Demo
{
    public class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"  [PIPELINE] Before handling {typeof(TRequest).Name}");
            var response = await next(cancellationToken);
            Console.WriteLine($"  [PIPELINE] After handling {typeof(TRequest).Name}");
            return response;
        }
    }
}
