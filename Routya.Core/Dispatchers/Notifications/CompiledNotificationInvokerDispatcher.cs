using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Notifications
{
    public sealed class CompiledNotificationDispatcher : IRoutyaNotificationDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;

        private static readonly ConcurrentDictionary<Type, NotificationHandlerInvoker[]> _sequentialCache = new ConcurrentDictionary<Type, NotificationHandlerInvoker[]>();
        private static readonly ConcurrentDictionary<Type, NotificationHandlerInvoker[]> _parallelCache = new ConcurrentDictionary<Type, NotificationHandlerInvoker[]>();

        public CompiledNotificationDispatcher(IServiceProvider provider, RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        public Task PublishAsync<TNotification>(
            TNotification notification,
            NotificationDispatchStrategy strategy = NotificationDispatchStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var notificationType = typeof(TNotification);
            var cache = strategy == NotificationDispatchStrategy.Sequential ? _sequentialCache : _parallelCache;
            var invokers = cache.GetOrAdd(notificationType, BuildInvokerArray<TNotification>);

            var provider = _options.Scope == RoutyaDispatchScope.Scoped
               ? _provider.CreateScope().ServiceProvider
               : _provider;

            if (strategy == NotificationDispatchStrategy.Sequential)
            {
                return InvokeSequential(invokers, provider, notification!, cancellationToken);
            }

            return InvokeParallel(invokers, provider, notification!, cancellationToken);
        }

        private NotificationHandlerInvoker[] BuildInvokerArray<TNotification>(Type _) where TNotification : INotification
        {
            var handlerInterface = typeof(INotificationHandler<TNotification>);
            var handlers = _provider.GetServices(handlerInterface).ToArray();

            var invokers = new List<NotificationHandlerInvoker>();

            foreach (var handler in handlers)
            {
                var handlerType = handler.GetType();
                invokers.Add(BuildHandlerInvoker<TNotification>(handlerType));
            }

            return invokers.ToArray();
        }

        private static NotificationHandlerInvoker BuildHandlerInvoker<TNotification>(Type concreteHandlerType) where TNotification : INotification
        {
            var serviceProviderParam = Expression.Parameter(typeof(IServiceProvider), CompiledConstant.ServiceProviderParameterName);
            var notificationParam = Expression.Parameter(typeof(object), CompiledConstant.NotificationParameterName);
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), CompiledConstant.CancellationTokenParameterName);

            var handlerInterface = typeof(INotificationHandler<TNotification>);
            var handleMethod = handlerInterface.GetMethod(nameof(INotificationHandler<TNotification>.Handle))!;
            var getTypeMethod = typeof(object).GetMethod(nameof(object.GetType))!;

            var getServicesCall = Expression.Call(
                typeof(ServiceProviderServiceExtensions),
                nameof(ServiceProviderServiceExtensions.GetServices),
                new[] { handlerInterface },
                serviceProviderParam
            );

            var handlerVar = Expression.Variable(handlerInterface, CompiledConstant.HandlerParameterName);
            var selectedHandler = Expression.Variable(handlerInterface, CompiledConstant.SelectedHandlerParameterName);

            var breakLabel = Expression.Label(CompiledConstant.LoopBreakName);

            var notificationCast = Expression.Convert(notificationParam, typeof(TNotification));

            var loopBlock = Expression.Block(
                new[] { selectedHandler },
                Expression.Block(
                    new[] { handlerVar },
                    ExpressionExtension.ForEach(
                        handlerInterface,
                        getServicesCall,
                        handlerVar,
                        Expression.IfThen(
                            Expression.Equal(
                                Expression.Call(Expression.Convert(handlerVar, typeof(object)), getTypeMethod),
                                Expression.Constant(concreteHandlerType, typeof(Type))
                            ),
                            Expression.Block(
                                Expression.Assign(selectedHandler, handlerVar),
                                Expression.Break(breakLabel)
                            )
                        )
                    ),
                    Expression.Label(breakLabel)
                ),
                Expression.Call(selectedHandler, handleMethod, notificationCast, cancellationTokenParam)
            );

            var lambda = Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task>>(
                loopBlock, serviceProviderParam, notificationParam, cancellationTokenParam
            );

            return new NotificationHandlerInvoker(lambda.Compile());
        }

        private static async Task InvokeSequential(NotificationHandlerInvoker[] invokers, IServiceProvider provider, object notification, CancellationToken cancellationToken)
        {
            foreach (var invoker in invokers)
            {
                await invoker.Invoke(provider, notification, cancellationToken);
            }
        }

        private static Task InvokeParallel(NotificationHandlerInvoker[] invokers, IServiceProvider provider, object notification, CancellationToken cancellationToken)
        {
            var tasks = new Task[invokers.Length];
            for (int i = 0; i < invokers.Length; i++)
            {
                tasks[i] = invokers[i].Invoke(provider, notification, cancellationToken);
            }

            return Task.WhenAll(tasks);
        }

        private sealed class NotificationHandlerInvoker
        {
            private readonly Func<IServiceProvider, object, CancellationToken, Task> _compiled;

            public NotificationHandlerInvoker(Func<IServiceProvider, object, CancellationToken, Task> compiled)
            {
                _compiled = compiled;
            }

            public Task Invoke(IServiceProvider provider, object notification, CancellationToken cancellationToken)
                => _compiled(provider, notification, cancellationToken);
        }
    }
}