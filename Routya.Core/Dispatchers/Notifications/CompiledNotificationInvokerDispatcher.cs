using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Notifications
{
    /// <summary>
    /// Notification dispatcher that fans a notification out to every registered handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handlers described in the registry, meaning those registered through
    /// <c>AddRoutyaNotificationHandler</c> or by assembly scanning, get a wrapper built once per
    /// notification type. Singleton handlers are resolved once; Scoped and Transient handlers are
    /// resolved from the dispatch scope on every publish. Handlers registered directly against
    /// <c>IServiceCollection</c> are resolved through their interface on every publish, because
    /// their concrete types are not guaranteed to be registered and they may be Scoped.
    /// </para>
    /// <para>
    /// Supports both sequential and parallel publishing strategies.
    /// </para>
    /// <para>
    /// No expression trees are built or compiled. Everything here is ordinary delegates and generic
    /// dispatch, which keeps the type free of the runtime code generation that trimming and Native
    /// AOT cannot support.
    /// </para>
    /// </remarks>
    public sealed class CompiledNotificationDispatcher : IRoutyaNotificationDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly Dictionary<Type, List<Extensions.NotificationHandlerInfo>> _notificationHandlerRegistry;

        // Cache per dispatcher instance (not static) to avoid cross-contamination between DI containers.
        // Only holds wrappers built from the registry, where the concrete handler type and lifetime
        // are both known, so resolution can be deferred to the dispatch scope safely.
        private readonly ConcurrentDictionary<Type, NotificationHandlerWrapper[]> _cache = new ConcurrentDictionary<Type, NotificationHandlerWrapper[]>();

        // Notification types whose handlers are registered directly against IServiceCollection rather
        // than through the registry. Their concrete types are not guaranteed to be registered in DI and
        // they may be Scoped, so they are resolved from the dispatch scope on every publish.
        private readonly ConcurrentDictionary<Type, bool> _requiresDynamicResolution = new ConcurrentDictionary<Type, bool>();

        // Notification types known to have no handlers at all. Registrations are immutable once the
        // container is built, so publishing these can return without creating a dispatch scope.
        private readonly ConcurrentDictionary<Type, bool> _knownEmpty = new ConcurrentDictionary<Type, bool>();

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
            // Fast path: this notification type is known to have no handlers, so there is nothing
            // to publish and no dispatch scope needs to be created.
            if (_knownEmpty.ContainsKey(typeof(TNotification)))
                return;

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                await PublishCoreAsync(scope.ServiceProvider, notification, strategy, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await PublishCoreAsync(_provider, notification, strategy, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Publishes a notification using <paramref name="provider"/>, which is the dispatch scope
        /// when <see cref="RoutyaDispatchScope.Scoped"/> is configured.
        /// </summary>
        private Task PublishCoreAsync<TNotification>(
            IServiceProvider provider,
            TNotification notification,
            NotificationDispatchStrategy strategy,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            var notificationType = typeof(TNotification);

            // Registry backed handlers: wrappers are built once, and resolve per dispatch internally
            // according to the lifetime recorded at registration time.
            if (_cache.TryGetValue(notificationType, out var cachedWrappers))
            {
                return InvokeWrappers(cachedWrappers, provider, notification!, strategy, cancellationToken);
            }

            if (_requiresDynamicResolution.ContainsKey(notificationType))
            {
                return InvokeResolvedAsync(provider, notification, strategy, cancellationToken);
            }

            var handlerType = typeof(INotificationHandler<TNotification>);

            if (_notificationHandlerRegistry.TryGetValue(handlerType, out var handlerInfos) && handlerInfos.Count > 0)
            {
                var wrappers = _cache.GetOrAdd(notificationType, _ => BuildRegistryWrappers<TNotification>(handlerInfos));
                return InvokeWrappers(wrappers, provider, notification!, strategy, cancellationToken);
            }

            // Not described in the registry, so fall back to resolving the handler services. This is
            // recorded so that later publishes skip the registry lookup, but the handlers themselves
            // are deliberately never cached: they may be Scoped and must come from the dispatch scope.
            _requiresDynamicResolution[notificationType] = true;
            return InvokeResolvedAsync(provider, notification, strategy, cancellationToken);
        }

        private NotificationHandlerWrapper[] BuildRegistryWrappers<TNotification>(
            List<Extensions.NotificationHandlerInfo> handlerInfos)
            where TNotification : INotification
        {
            var wrappers = new NotificationHandlerWrapper[handlerInfos.Count];

            for (int i = 0; i < handlerInfos.Count; i++)
            {
                var handlerInfo = handlerInfos[i];

                // Singleton handlers live in the root provider, so the instance can be resolved once
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

        private static Task InvokeWrappers(
            NotificationHandlerWrapper[] handlers,
            IServiceProvider provider,
            object notification,
            NotificationDispatchStrategy strategy,
            CancellationToken cancellationToken)
        {
            if (handlers.Length == 0)
                return Task.CompletedTask;

            return strategy == NotificationDispatchStrategy.Sequential
                ? InvokeSequential(handlers, provider, notification, cancellationToken)
                : InvokeParallel(handlers, provider, notification, cancellationToken);
        }

        /// <summary>
        /// Resolves handler services from the dispatch scope and invokes them. Used for handlers
        /// registered directly against <c>IServiceCollection</c>, whose concrete types may not be
        /// registered in DI and therefore cannot be resolved by concrete type.
        /// </summary>
        private Task InvokeResolvedAsync<TNotification>(
            IServiceProvider provider,
            TNotification notification,
            NotificationDispatchStrategy strategy,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            var resolved = provider.GetServices<INotificationHandler<TNotification>>();
            var handlers = resolved as INotificationHandler<TNotification>[] ?? resolved.ToArray();

            if (handlers.Length == 0)
            {
                _knownEmpty[typeof(TNotification)] = true;
                return Task.CompletedTask;
            }

            return strategy == NotificationDispatchStrategy.Sequential
                ? InvokeSequentialDirect(handlers, notification, cancellationToken)
                : InvokeParallelDirect(handlers, notification, cancellationToken);
        }

        private static async Task InvokeSequentialDirect<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            foreach (var handler in handlers)
            {
                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
            }
        }

        private static Task InvokeParallelDirect<TNotification>(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
            where TNotification : INotification
        {
            if (handlers.Length == 1)
            {
                return handlers[0].Handle(notification, cancellationToken);
            }

            var tasks = new Task[handlers.Length];
            for (int i = 0; i < handlers.Length; i++)
            {
                tasks[i] = handlers[i].Handle(notification, cancellationToken);
            }

            return Task.WhenAll(tasks);
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
    }
}