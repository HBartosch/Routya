using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    internal static class CompiledRequestInvokerFactory
    {
        public static Func<IServiceProvider, TRequest, TResponse> CreateSync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            var spParam = Expression.Parameter(typeof(IServiceProvider), CompiledConstant.ServiceProviderParameterName);
            var requestParam = Expression.Parameter(typeof(TRequest), CompiledConstant.RequestParameterName);

            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            var getHandler = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetRequiredService),
                new[] { handlerType },
                spParam
            );

            var callHandle = Expression.Call(
                Expression.Convert(getHandler, handlerType),
                handlerType.GetMethod(nameof(IRequestHandler<TRequest, TResponse>.Handle))!,
                requestParam
            );

            var lambda = Expression.Lambda<Func<IServiceProvider, TRequest, TResponse>>(callHandle, spParam, requestParam);
            return lambda.Compile();
        }

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> CreateAsync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            var spParam = Expression.Parameter(typeof(IServiceProvider), CompiledConstant.ServiceProviderParameterName);
            var requestParam = Expression.Parameter(typeof(TRequest), CompiledConstant.RequestParameterName);
            var tokenParam = Expression.Parameter(typeof(CancellationToken), CompiledConstant.CancellationTokenParameterName);

            var asyncHandlerType = typeof(IAsyncRequestHandler<TRequest, TResponse>);
            var syncHandlerType = typeof(IRequestHandler<TRequest, TResponse>);

            var getAsync = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetService),
                new[] { asyncHandlerType },
                spParam
            );

            var callAsync = Expression.Call(
                Expression.Convert(getAsync, asyncHandlerType),
                asyncHandlerType.GetMethod(nameof(IAsyncRequestHandler<TRequest, TResponse>.HandleAsync))!,
                requestParam,
                tokenParam
            );

            var getSync = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetRequiredService),
                new[] { syncHandlerType },
                spParam
            );

            var callSync = Expression.Call(
                Expression.Convert(getSync, syncHandlerType),
                syncHandlerType.GetMethod(nameof(IRequestHandler<TRequest, TResponse>.Handle))!,
                requestParam
            );

            var wrapInTask = Expression.Call(
                typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(TResponse)),
                callSync
            );

            var condition = Expression.Condition(
                Expression.NotEqual(getAsync, Expression.Constant(null, asyncHandlerType)),
                callAsync,
                wrapInTask
            );

            return Expression
                .Lambda<Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>>(
                    condition, spParam, requestParam, tokenParam)
                .Compile();
        }
    }
}