using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Core.Extensions
{
    public class NotificationHandlerInfo
    {
        public Type ConcreteType { get; set; }
        public ServiceLifetime Lifetime { get; set; }
    }

    public class RequestHandlerInfo
    {
        public Type ConcreteType { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public bool IsAsync { get; set; }
    }

    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddRoutya(
            this IServiceCollection services, 
            Action<RoutyaDispatcherOptions>? configure = null,
            params Assembly[] scanAssemblies)
        {
            var options = new RoutyaDispatcherOptions();
            configure?.Invoke(options);

            // Get or create notification handler registry
            var existingNotificationRegistry = services
                .Where(sd => sd.ServiceType == typeof(Dictionary<Type, List<NotificationHandlerInfo>>))
                .Select(sd => sd.ImplementationInstance as Dictionary<Type, List<NotificationHandlerInfo>>)
                .FirstOrDefault();

            var notificationHandlerRegistry = existingNotificationRegistry ?? new Dictionary<Type, List<NotificationHandlerInfo>>();

            // Get or create request handler registry
            var existingRequestRegistry = services
                .Where(sd => sd.ServiceType == typeof(Dictionary<Type, RequestHandlerInfo>))
                .Select(sd => sd.ImplementationInstance as Dictionary<Type, RequestHandlerInfo>)
                .FirstOrDefault();

            var requestHandlerRegistry = existingRequestRegistry ?? new Dictionary<Type, RequestHandlerInfo>();

            if (scanAssemblies?.Length > 0)
            {
                foreach (var assembly in scanAssemblies)
                {
                    RegisterRoutyaHandlersFromAssembly(services, assembly, options.HandlerLifetime, notificationHandlerRegistry, requestHandlerRegistry);
                }
            }

            // Register the notification handler registry as a singleton (if not already registered)
            if (existingNotificationRegistry == null)
            {
                services.AddSingleton(notificationHandlerRegistry);
            }

            // Register the request handler registry as a singleton (if not already registered)
            if (existingRequestRegistry == null)
            {
                services.AddSingleton(requestHandlerRegistry);
            }

            services.AddSingleton<IRoutyaRequestDispatcher>(sp => new CompiledRequestInvokerDispatcher(
                sp, 
                sp.GetRequiredService<Dictionary<Type, RequestHandlerInfo>>(), 
                options));

            services.AddSingleton<IRoutyaNotificationDispatcher>(sp => new CompiledNotificationDispatcher(
                sp, 
                sp.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>(), 
                options));

            services.AddSingleton<IRoutya, DefaultRoutya>();

            return services;
        }

        /// <summary>
        /// Manually register a request handler with the specified lifetime (defaults to Scoped).
        /// </summary>
        public static IServiceCollection AddRoutyaRequestHandler<TRequest, TResponse, THandler>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TRequest : IRequest<TResponse>
            where THandler : class, IRequestHandler<TRequest, TResponse>
        {
            var handlerInterface = typeof(IRequestHandler<TRequest, TResponse>);
            var handlerType = typeof(THandler);

            // Get or create the request handler registry
            var registry = services
                .Where(sd => sd.ServiceType == typeof(Dictionary<Type, RequestHandlerInfo>))
                .Select(sd => sd.ImplementationInstance as Dictionary<Type, RequestHandlerInfo>)
                .FirstOrDefault();

            if (registry == null)
            {
                registry = new Dictionary<Type, RequestHandlerInfo>();
                services.AddSingleton(registry);
            }

            // Add to registry
            registry[handlerInterface] = new RequestHandlerInfo
            {
                ConcreteType = handlerType,
                Lifetime = lifetime,
                IsAsync = false
            };

            // Register with DI
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(handlerInterface, handlerType);
                    services.AddSingleton(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScoped(handlerInterface, handlerType);
                    services.AddScoped(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(handlerInterface, handlerType);
                    services.AddTransient(handlerType); // Also register concrete type
                    break;
            }

            return services;
        }

        /// <summary>
        /// Manually register an async request handler with the specified lifetime (defaults to Scoped).
        /// </summary>
        public static IServiceCollection AddRoutyaAsyncRequestHandler<TRequest, TResponse, THandler>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TRequest : IRequest<TResponse>
            where THandler : class, IAsyncRequestHandler<TRequest, TResponse>
        {
            var handlerInterface = typeof(IAsyncRequestHandler<TRequest, TResponse>);
            var handlerType = typeof(THandler);

            // Get or create the request handler registry
            var registry = services
                .Where(sd => sd.ServiceType == typeof(Dictionary<Type, RequestHandlerInfo>))
                .Select(sd => sd.ImplementationInstance as Dictionary<Type, RequestHandlerInfo>)
                .FirstOrDefault();

            if (registry == null)
            {
                registry = new Dictionary<Type, RequestHandlerInfo>();
                services.AddSingleton(registry);
            }

            // Add to registry
            registry[handlerInterface] = new RequestHandlerInfo
            {
                ConcreteType = handlerType,
                Lifetime = lifetime,
                IsAsync = true
            };

            // Register with DI
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(handlerInterface, handlerType);
                    services.AddSingleton(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScoped(handlerInterface, handlerType);
                    services.AddScoped(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(handlerInterface, handlerType);
                    services.AddTransient(handlerType); // Also register concrete type
                    break;
            }

            return services;
        }

        /// <summary>
        /// Manually register a notification handler with the specified lifetime (defaults to Scoped).
        /// Ensures the notification handler registry is properly maintained.
        /// </summary>
        public static IServiceCollection AddRoutyaNotificationHandler<TNotification, THandler>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TNotification : INotification
            where THandler : class, INotificationHandler<TNotification>
        {
            var handlerInterface = typeof(INotificationHandler<TNotification>);
            var handlerType = typeof(THandler);

            // Get or create the notification handler registry
            var registry = services
                .Where(sd => sd.ServiceType == typeof(Dictionary<Type, List<NotificationHandlerInfo>>))
                .Select(sd => sd.ImplementationInstance as Dictionary<Type, List<NotificationHandlerInfo>>)
                .FirstOrDefault();

            if (registry == null)
            {
                registry = new Dictionary<Type, List<NotificationHandlerInfo>>();
                services.AddSingleton(registry);
            }

            // Add to registry
            if (!registry.ContainsKey(handlerInterface))
            {
                registry[handlerInterface] = new List<NotificationHandlerInfo>();
            }

            registry[handlerInterface].Add(new NotificationHandlerInfo
            {
                ConcreteType = handlerType,
                Lifetime = lifetime
            });

            // Register with DI
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(handlerInterface, handlerType);
                    services.AddSingleton(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScoped(handlerInterface, handlerType);
                    services.AddScoped(handlerType); // Also register concrete type
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(handlerInterface, handlerType);
                    services.AddTransient(handlerType); // Also register concrete type
                    break;
            }

            return services;
        }

        private static void RegisterRoutyaHandlersFromAssembly(
            IServiceCollection services, 
            Assembly assembly, 
            ServiceLifetime lifetime,
            Dictionary<Type, List<NotificationHandlerInfo>> notificationHandlerRegistry,
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
        {
            var allTypes = assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in allTypes)
            {
                var interfaces = type.GetInterfaces();

                foreach (var iface in interfaces)
                {
                    if (!iface.IsGenericType) continue;

                    var def = iface.GetGenericTypeDefinition();

                    if (def == typeof(IRequestHandler<,>))
                    {
                        // Add to request handler registry (sync)
                        requestHandlerRegistry[iface] = new RequestHandlerInfo
                        {
                            ConcreteType = type,
                            Lifetime = lifetime,
                            IsAsync = false
                        };
                        
                        // Register with DI
                        switch (lifetime)
                        {
                            case ServiceLifetime.Singleton:
                                services.AddSingleton(iface, type);
                                services.AddSingleton(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Scoped:
                                services.AddScoped(iface, type);
                                services.AddScoped(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Transient:
                                services.AddTransient(iface, type);
                                services.AddTransient(type); // Also register concrete type
                                break;
                        }
                    }
                    else if (def == typeof(IAsyncRequestHandler<,>))
                    {
                        // Add to request handler registry (async)
                        requestHandlerRegistry[iface] = new RequestHandlerInfo
                        {
                            ConcreteType = type,
                            Lifetime = lifetime,
                            IsAsync = true
                        };
                        
                        // Register with DI
                        switch (lifetime)
                        {
                            case ServiceLifetime.Singleton:
                                services.AddSingleton(iface, type);
                                services.AddSingleton(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Scoped:
                                services.AddScoped(iface, type);
                                services.AddScoped(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Transient:
                                services.AddTransient(iface, type);
                                services.AddTransient(type); // Also register concrete type
                                break;
                        }
                    }
                    else if (def == typeof(INotificationHandler<>))
                    {
                        // Add to notification handler registry
                        if (!notificationHandlerRegistry.ContainsKey(iface))
                        {
                            notificationHandlerRegistry[iface] = new List<NotificationHandlerInfo>();
                        }
                        
                        notificationHandlerRegistry[iface].Add(new NotificationHandlerInfo
                        {
                            ConcreteType = type,
                            Lifetime = lifetime
                        });
                        
                        // Register with DI
                        switch (lifetime)
                        {
                            case ServiceLifetime.Singleton:
                                services.AddSingleton(iface, type);
                                services.AddSingleton(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Scoped:
                                services.AddScoped(iface, type);
                                services.AddScoped(type); // Also register concrete type
                                break;
                            case ServiceLifetime.Transient:
                                services.AddTransient(iface, type);
                                services.AddTransient(type); // Also register concrete type
                                break;
                        }
                    }
                }
            }
        }
    }
}
