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
        private static readonly ConcurrentDictionary<Type, Delegate> _cache = new ConcurrentDictionary<Type, Delegate>();

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> GetOrAdd<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            var key = typeof(TRequest);

            return (Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>)_cache.GetOrAdd(key, _ =>
            {
                return BuildPipeline<TRequest, TResponse>();
            });
        }

        private static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> BuildPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (provider, request, cancellationToken) =>
            {
                var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

                var coreHandler = CompiledRequestInvokerFactory.CreateAsync<TRequest, TResponse>();

                if (behaviors.Count() == 0)
                    return coreHandler(provider, request, cancellationToken);

                RequestHandlerDelegate<TResponse> handler = () => coreHandler(provider, request, cancellationToken);

                foreach (var behavior in behaviors.AsEnumerable().Reverse())
                {
                    var next = handler;
                    var b = behavior;
                    handler = () => b.Handle(request, next, cancellationToken);
                }

                return handler();
            };
        }

        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();

        public static Func<IServiceProvider, TRequest, TResponse> GetOrAddSync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            var key = typeof(TRequest);

            return (Func<IServiceProvider, TRequest, TResponse>)_syncCache.GetOrAdd(key, _ =>
            {
                return BuildSyncPipeline<TRequest, TResponse>();
            });
        }

        private static Func<IServiceProvider, TRequest, TResponse> BuildSyncPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (provider, request) =>
            {
                var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                var coreHandler = CompiledRequestInvokerFactory.CreateSync<TRequest, TResponse>();

                if (behaviors.Count() == 0)
                    return coreHandler(provider, request);

                RequestHandlerDelegate<TResponse> handler = () => Task.FromResult(coreHandler(provider, request));

                foreach (var behavior in behaviors.AsEnumerable().Reverse())
                {
                    var next = handler;
                    var b = behavior;
                    handler = () => b.Handle(request, next, CancellationToken.None);
                }

                return handler().GetAwaiter().GetResult();
            };
        }
    }
}