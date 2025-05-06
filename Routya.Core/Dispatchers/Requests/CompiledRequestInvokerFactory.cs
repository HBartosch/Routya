using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    internal static class CompiledRequestInvokerFactory
    {
        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();
        private static readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();

        public static Func<IServiceProvider, TRequest, TResponse> CreateSync<TRequest, TResponse>()
           where TRequest : IRequest<TResponse>
        {
            var key = typeof(TRequest);

            var invoker = (Func<IRequestHandler<TRequest, TResponse>, TRequest, TResponse>)_syncCache.GetOrAdd(key, _ =>
            {
                var handlerParam = Expression.Parameter(typeof(IRequestHandler<TRequest, TResponse>), CompiledConstant.HandlerParameterName);
                var requestParam = Expression.Parameter(typeof(TRequest), CompiledConstant.RequestParameterName);

                var call = Expression.Call(
                    handlerParam,
                    typeof(IRequestHandler<TRequest, TResponse>).GetMethod(nameof(IRequestHandler<TRequest, TResponse>.Handle))!,
                    requestParam
                );

                return Expression
                    .Lambda<Func<IRequestHandler<TRequest, TResponse>, TRequest, TResponse>>(call, handlerParam, requestParam)
                    .Compile();
            });

            return (sp, req) =>
            {
                var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
                return invoker(handler, req);
            };
        }

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> CreateAsync<TRequest, TResponse>()
           where TRequest : IRequest<TResponse>
        {
            var key = typeof(TRequest);

            var asyncInvoker = (Func<IAsyncRequestHandler<TRequest, TResponse>, TRequest, CancellationToken, Task<TResponse>>)_asyncCache.GetOrAdd(key, _ =>
            {
                var handlerParam = Expression.Parameter(typeof(IAsyncRequestHandler<TRequest, TResponse>), CompiledConstant.HandlerParameterName);
                var requestParam = Expression.Parameter(typeof(TRequest), CompiledConstant.RequestParameterName);
                var tokenParam = Expression.Parameter(typeof(CancellationToken), CompiledConstant.CancellationTokenParameterName);

                var call = Expression.Call(
                    handlerParam,
                    typeof(IAsyncRequestHandler<TRequest, TResponse>).GetMethod(nameof(IAsyncRequestHandler<TRequest, TResponse>.HandleAsync))!,
                    requestParam,
                    tokenParam
                );

                return Expression
                    .Lambda<Func<IAsyncRequestHandler<TRequest, TResponse>, TRequest, CancellationToken, Task<TResponse>>>(
                        call, handlerParam, requestParam, tokenParam
                    ).Compile();
            });

            var syncInvoker = CreateSync<TRequest, TResponse>();

            return (sp, req, token) =>
            {
                var asyncHandler = sp.GetService<IAsyncRequestHandler<TRequest, TResponse>>();
                if (asyncHandler != null)
                    return asyncInvoker(asyncHandler, req, token);

                return Task.FromResult(syncInvoker(sp, req));
            };
        }
    }
}