using Routya.Core.Abstractions;

namespace Routya.WebApi.SourceGen.Demo.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next(ct);
        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
