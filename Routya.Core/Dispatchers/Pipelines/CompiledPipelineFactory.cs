using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Pipelines
{
    internal static class CompiledPipelineFactory
    {
        private static readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();
        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> GetOrAdd<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>)_asyncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledAsyncPipeline<TRequest, TResponse>());
        }

        private static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> BuildPrecompiledAsyncPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (provider, request, cancellationToken) =>
            {
                // Resolve once
                var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
                var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

                // Build the full chain once using captured handler and behaviors
                RequestHandlerDelegate<TResponse> finalHandler = () =>
                {
                    if (handler is IAsyncRequestHandler<TRequest, TResponse> asyncHandler)
                        return asyncHandler.HandleAsync(request, cancellationToken);

                    return Task.FromResult(handler.Handle(request));
                };

                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var b = behaviors[i];
                    var next = finalHandler;
                    finalHandler = () => b.Handle(request, next, cancellationToken);
                }

                return finalHandler();
            };
        }

        public static Func<IServiceProvider, TRequest, TResponse> GetOrAddSync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, TResponse>)_syncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledSyncPipeline<TRequest, TResponse>());
        }

        private static Func<IServiceProvider, TRequest, TResponse> BuildPrecompiledSyncPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (provider, request) =>
            {
                var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
                var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

                RequestHandlerDelegate<TResponse> finalHandler = () =>
                    Task.FromResult(handler.Handle(request));

                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var b = behaviors[i];
                    var next = finalHandler;
                    finalHandler = () => b.Handle(request, next, CancellationToken.None);
                }

                return finalHandler().GetAwaiter().GetResult();
            };
        }
    }

}