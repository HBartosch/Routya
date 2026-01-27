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
    /// <summary>
    /// High-performance notification dispatcher using compiled expression trees for fast handler invocation.
    /// </summary>
    /// <remarks>
    /// This dispatcher uses a registry-based approach with compiled expressions and per-instance caching.
    /// Handlers are discovered from the registry and compiled on first use, then cached for subsequent dispatches.
    /// Supports both sequential and parallel notification publishing strategies.
    /// </remarks>
    public sealed class CompiledNotificationDispatcher : IRoutyaNotificationDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly Dictionary<Type, List<Extensions.NotificationHandlerInfo>> _notificationHandlerRegistry;

        // Cache per dispatcher instance (not static) to avoid cross-contamination between DI containers
        private readonly ConcurrentDictionary<Type, NotificationHandlerWrapper[]> _cache = new ConcurrentDictionary<Type, NotificationHandlerWrapper[]>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledNotificationDispatcher"/> class.
        /// </summary>
        /// <param name="provider">The service provider for resolving handlers.</param>
        /// <param name="notificationHandlerRegistry">The registry containing pre-registered handler information.</param>
        /// <param name="options">Configuration options for the dispatcher.</param>
        public CompiledNotificationDispatcher(
            IServiceProvider provider, 
            Dictionary<Type, List<Extensions.NotificationHandlerInfo>> notificationHandlerRegistry,
            RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _notificationHandlerRegistry = notificationHandlerRegistry;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        /// <inheritdoc />
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
                    await InvokeSequential(handlers, scope.ServiceProvider, notification!, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await InvokeParallel(handlers, scope.ServiceProvider, notification!, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                if (strategy == NotificationDispatchStrategy.Sequential)
                {
                    await InvokeSequential(handlers, _provider, notification!, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await InvokeParallel(handlers, _provider, notification!, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private NotificationHandlerWrapper[] BuildHandlerWrappers<TNotification>() where TNotification : INotification
        {
            var handlerType = typeof(INotificationHandler<TNotification>);
            
            // Try to get handler info from registry first (fast path)
            if (_notificationHandlerRegistry.TryGetValue(handlerType, out var handlerInfos) && handlerInfos.Count > 0)
            {
                var wrappers = new NotificationHandlerWrapper[handlerInfos.Count];

                for (int i = 0; i < handlerInfos.Count; i++)
                {
                    var handlerInfo = handlerInfos[i];
                    
                    // For Singleton handlers, resolve and cache the instance NOW
                    if (handlerInfo.Lifetime == ServiceLifetime.Singleton)
                    {
                        var singletonInstance = (INotificationHandler<TNotification>)_provider.GetRequiredService(handlerInfo.ConcreteType);
                        wrappers[i] = new NotificationHandlerWrapper<TNotification>(
                            singletonInstance, 
                            handlerInfo.Lifetime, 
                            handlerInfo.ConcreteType);
                    }
                    else
                    {
                        // For Scoped/Transient, just store the type info
                        wrappers[i] = new NotificationHandlerWrapper<TNotification>(
                            null!, 
                            handlerInfo.Lifetime, 
                            handlerInfo.ConcreteType);
                    }
                }

                return wrappers;
            }
            
            // Fallback: Not in registry, try GetServices (for backward compatibility)
            var handlers = _provider.GetServices<INotificationHandler<TNotification>>().ToArray();
            if (handlers.Length == 0)
                return Array.Empty<NotificationHandlerWrapper>();
            
            // Add discovered handlers to registry for future optimization
            lock (_notificationHandlerRegistry)
            {
                // Double-check it wasn't added by another thread
                if (!_notificationHandlerRegistry.ContainsKey(handlerType))
                {
                    var discoveredHandlerInfos = new List<Extensions.NotificationHandlerInfo>();
                    foreach (var handler in handlers)
                    {
                        discoveredHandlerInfos.Add(new Extensions.NotificationHandlerInfo
                        {
                            ConcreteType = handler.GetType(),
                            Lifetime = ServiceLifetime.Transient // Default fallback lifetime
                        });
                    }
                    _notificationHandlerRegistry[handlerType] = discoveredHandlerInfos;
                }
            }
            
            // Build wrappers for handlers found via GetServices
            // Cache the actual handler instances since we already resolved them
            var fallbackWrappers = new NotificationHandlerWrapper[handlers.Length];
            for (int i = 0; i < handlers.Length; i++)
            {
                // Store the resolved handler instance directly (no type, since we already have the instance)
                fallbackWrappers[i] = new NotificationHandlerWrapperWithInstance<TNotification>(handlers[i]);
            }
            
            return fallbackWrappers;
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

        private class NotificationHandlerWrapper<TNotification> : NotificationHandlerWrapper
            where TNotification : INotification
        {
            private readonly INotificationHandler<TNotification>? _cachedHandler;
            private readonly Type? _concreteHandlerType;
            private readonly ServiceLifetime _lifetime;

            // Constructor: cache Singleton instance or store concrete type for Scoped/Transient
            public NotificationHandlerWrapper(INotificationHandler<TNotification> handler, ServiceLifetime lifetime, Type concreteType)
            {
                _lifetime = lifetime;
                _concreteHandlerType = concreteType;
                
                // Only cache the instance for Singleton handlers
                _cachedHandler = lifetime == ServiceLifetime.Singleton ? handler : null;
            }

            public override Task Handle(IServiceProvider provider, object notification, CancellationToken cancellationToken)
            {
                INotificationHandler<TNotification> handler;
                
                if (_cachedHandler != null)
                {
                    // Fast path: Use cached instance (Singleton or from fallback GetServices)
                    handler = _cachedHandler;
                }
                else
                {
                    // Scoped/Transient: Resolve by concrete type directly!
                    handler = (INotificationHandler<TNotification>)provider.GetRequiredService(_concreteHandlerType!);
                }
                
                return handler.Handle((TNotification)notification!, cancellationToken);
            }
        }
        
        // Wrapper for fallback handlers resolved via GetServices (already have instance)
        private class NotificationHandlerWrapperWithInstance<TNotification> : NotificationHandlerWrapper
            where TNotification : INotification
        {
            private readonly INotificationHandler<TNotification> _handler;

            public NotificationHandlerWrapperWithInstance(INotificationHandler<TNotification> handler)
            {
                _handler = handler;
            }

            public override Task Handle(IServiceProvider provider, object notification, CancellationToken cancellationToken)
            {
                return _handler.Handle((TNotification)notification!, cancellationToken);
            }
        }
    }
}