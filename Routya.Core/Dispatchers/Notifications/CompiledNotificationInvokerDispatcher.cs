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

        private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper[]> _cache = new ConcurrentDictionary<Type, NotificationHandlerWrapper[]>();

        public CompiledNotificationDispatcher(IServiceProvider provider, RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        public async Task PublishAsync<TNotification>(
            TNotification notification,
            NotificationDispatchStrategy strategy = NotificationDispatchStrategy.Sequential,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var notificationType = typeof(TNotification);
            var handlers = _cache.GetOrAdd(notificationType, _ => BuildHandlerWrappers<TNotification>());

            if (handlers.Length == 0)
                return;

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                if (strategy == NotificationDispatchStrategy.Sequential)
                {
                    await InvokeSequential(handlers, scope.ServiceProvider, notification!, cancellationToken);
                }
                else
                {
                    await InvokeParallel(handlers, scope.ServiceProvider, notification!, cancellationToken);
                }
            }
            else
            {
                if (strategy == NotificationDispatchStrategy.Sequential)
                {
                    await InvokeSequential(handlers, _provider, notification!, cancellationToken);
                }
                else
                {
                    await InvokeParallel(handlers, _provider, notification!, cancellationToken);
                }
            }
        }

        private NotificationHandlerWrapper[] BuildHandlerWrappers<TNotification>() where TNotification : INotification
        {
            var handlerType = typeof(INotificationHandler<TNotification>);
            
            // Get handler types from DI once during cache building to know how many handlers exist
            var handlers = _provider.GetServices(handlerType).ToArray();

            if (handlers.Length == 0)
                return Array.Empty<NotificationHandlerWrapper>();

            var wrappers = new NotificationHandlerWrapper[handlers.Length];

            for (int i = 0; i < handlers.Length; i++)
            {
                var concreteHandlerType = handlers[i]!.GetType();
                wrappers[i] = new NotificationHandlerWrapper<TNotification>(concreteHandlerType);
            }

            return wrappers;
        }

        private static async Task InvokeSequential(NotificationHandlerWrapper[] handlers, IServiceProvider provider, object notification, CancellationToken cancellationToken)
        {
            foreach (var handler in handlers)
            {
                await handler.Handle(provider, notification, cancellationToken).ConfigureAwait(false);
            }
        }

        private static Task InvokeParallel(NotificationHandlerWrapper[] handlers, IServiceProvider provider, object notification, CancellationToken cancellationToken)
        {
            if (handlers.Length == 1)
            {
                return handlers[0].Handle(provider, notification, cancellationToken);
            }

            var tasks = new Task[handlers.Length];
            for (int i = 0; i < handlers.Length; i++)
            {
                tasks[i] = handlers[i].Handle(provider, notification, cancellationToken);
            }

            return Task.WhenAll(tasks);
        }

        private abstract class NotificationHandlerWrapper
        {
            public abstract Task Handle(IServiceProvider provider, object notification, CancellationToken cancellationToken);
        }

        private sealed class NotificationHandlerWrapper<TNotification> : NotificationHandlerWrapper
            where TNotification : INotification
        {
            private readonly Type _concreteHandlerType;
            private readonly Func<IServiceProvider, INotificationHandler<TNotification>> _handlerFactory;

            public NotificationHandlerWrapper(Type concreteHandlerType)
            {
                _concreteHandlerType = concreteHandlerType;
                
                // Cache the handler resolution logic to avoid GetServices + FirstOrDefault on every notification
                _handlerFactory = (provider) =>
                {
                    // Direct type resolution is much faster than GetServices + FirstOrDefault
                    return (INotificationHandler<TNotification>)provider.GetRequiredService(_concreteHandlerType);
                };
            }

            public override Task Handle(IServiceProvider provider, object notification, CancellationToken cancellationToken)
            {
                var handler = _handlerFactory(provider);
                return handler.Handle((TNotification)notification!, cancellationToken);
            }
        }
    }
}