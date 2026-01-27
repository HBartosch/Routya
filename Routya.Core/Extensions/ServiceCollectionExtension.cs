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
    /// <summary>
    /// Internal registry information for notification handlers, used for optimized dispatch performance.
    /// </summary>
    public class NotificationHandlerInfo
    {
        /// <summary>
        /// Gets or sets the concrete type of the notification handler.
        /// </summary>
        public Type ConcreteType { get; set; } = null!;
        
        /// <summary>
        /// Gets or sets the service lifetime of the notification handler.
        /// </summary>
        public ServiceLifetime Lifetime { get; set; }
    }

    /// <summary>
    /// Internal registry information for request handlers, used for optimized dispatch performance.
    /// </summary>
    public class RequestHandlerInfo
    {
        /// <summary>
        /// Gets or sets the concrete type of the request handler.
        /// </summary>
        public Type ConcreteType { get; set; } = null!;
        
        /// <summary>
        /// Gets or sets the service lifetime of the request handler.
        /// </summary>
        public ServiceLifetime Lifetime { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this is an async request handler.
        /// </summary>
        public bool IsAsync { get; set; }
    }

    /// <summary>
    /// Extension methods for registering Routya services and handlers with the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtension
    {
        /// <summary>
        /// Registers Routya core services and optionally scans assemblies for request and notification handlers.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="configure">Optional configuration action to customize <see cref="RoutyaDispatcherOptions"/>.</param>
        /// <param name="scanAssemblies">Optional assemblies to scan for <see cref="IRequestHandler{TRequest, TResponse}"/>, 
        /// <see cref="IAsyncRequestHandler{TRequest, TResponse}"/>, and <see cref="INotificationHandler{TNotification}"/> implementations.</param>
        /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
        /// <remarks>
        /// <para>This method registers the core Routya dispatcher services and builds an optimized handler registry for fast dispatch.</para>
        /// <para>
        /// When <paramref name="scanAssemblies"/> are provided, all handler implementations are automatically registered with the specified 
        /// <see cref="RoutyaDispatcherOptions.HandlerLifetime"/> (defaults to Scoped).
        /// </para>
        /// <para>
        /// <b>Performance Tip:</b> For optimal performance, use the specialized registration methods instead of assembly scanning:
        /// <list type="bullet">
        /// <item><description><see cref="AddRoutyaRequestHandler{TRequest, TResponse, THandler}"/> for synchronous request handlers</description></item>
        /// <item><description><see cref="AddRoutyaAsyncRequestHandler{TRequest, TResponse, THandler}"/> for asynchronous request handlers</description></item>
        /// <item><description><see cref="AddRoutyaNotificationHandler{TNotification, THandler}"/> for notification handlers</description></item>
        /// </list>
        /// These methods populate the handler registry at startup, avoiding any reflection overhead at runtime.
        /// </para>
        /// <para>
        /// Example - Basic registration without assembly scanning:
        /// <code>
        /// services.AddRoutya(); // Core services only
        /// </code>
        /// </para>
        /// <para>
        /// Example - Assembly scanning with custom configuration:
        /// <code>
        /// services.AddRoutya(
        ///     cfg => {
        ///         cfg.Scope = RoutyaDispatchScope.Scoped;
        ///         cfg.HandlerLifetime = ServiceLifetime.Singleton; // All scanned handlers will be Singleton
        ///     },
        ///     Assembly.GetExecutingAssembly()
        /// );
        /// </code>
        /// </para>
        /// <para>
        /// Example - Optimal performance with manual registration:
        /// <code>
        /// services.AddRoutya(); // Core services only
        /// services.AddRoutyaAsyncRequestHandler&lt;CreateProductRequest, Product, CreateProductHandler&gt;(ServiceLifetime.Singleton);
        /// services.AddRoutyaRequestHandler&lt;GetProductRequest, Product?, GetProductHandler&gt;(ServiceLifetime.Scoped);
        /// services.AddRoutyaNotificationHandler&lt;UserRegisteredNotification, SendEmailHandler&gt;(ServiceLifetime.Singleton);
        /// </code>
        /// </para>
        /// </remarks>
        public static IServiceCollection AddRoutya(
            this IServiceCollection services, 
            Action<RoutyaDispatcherOptions>? configure = null!,
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
        /// Registers a synchronous request handler with the specified lifetime for optimal performance.
        /// Automatically adds the handler to the internal registry for fast dispatch.
        /// </summary>
        /// <typeparam name="TRequest">The type of request to handle, implementing <see cref="IRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
        /// <typeparam name="THandler">The concrete handler type implementing <see cref="IRequestHandler{TRequest, TResponse}"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the handler to.</param>
        /// <param name="lifetime">The service lifetime for the handler. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
        /// <remarks>
        /// <para>This method provides better performance than standard DI registration by populating the handler registry at startup.</para>
        /// <para>
        /// <b>Lifetime Recommendations:</b>
        /// <list type="bullet">
        /// <item><description><b>Singleton:</b> Best performance (~334ns). Use for stateless handlers. Must be thread-safe.</description></item>
        /// <item><description><b>Scoped:</b> Safe for handlers with DbContext or scoped dependencies (~395ns). Default and recommended.</description></item>
        /// <item><description><b>Transient:</b> New instance every time (~336ns). Use when you need maximum isolation.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Example:
        /// <code>
        /// // Singleton for stateless operations (fastest)
        /// services.AddRoutyaRequestHandler&lt;CalculateTaxRequest, decimal, CalculateTaxHandler&gt;(ServiceLifetime.Singleton);
        /// 
        /// // Scoped for database operations
        /// services.AddRoutyaRequestHandler&lt;GetUserRequest, User?, GetUserHandler&gt;(ServiceLifetime.Scoped);
        /// </code>
        /// </para>
        /// </remarks>
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
        /// Registers an asynchronous request handler with the specified lifetime for optimal performance.
        /// Automatically adds the handler to the internal registry for fast dispatch.
        /// </summary>
        /// <typeparam name="TRequest">The type of request to handle, implementing <see cref="IRequest{TResponse}"/>.</typeparam>
        /// <typeparam name="TResponse">The type of response produced by the handler.</typeparam>
        /// <typeparam name="THandler">The concrete handler type implementing <see cref="IAsyncRequestHandler{TRequest, TResponse}"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the handler to.</param>
        /// <param name="lifetime">The service lifetime for the handler. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
        /// <remarks>
        /// <para>This is the recommended registration method for async handlers, providing optimal performance through registry-based dispatch.</para>
        /// <para>
        /// <b>Performance Characteristics:</b>
        /// <list type="bullet">
        /// <item><description><b>Singleton:</b> ~398ns per request. Fastest option for stateless async handlers.</description></item>
        /// <item><description><b>Scoped:</b> ~476ns per request. Safe for handlers using DbContext or other scoped services.</description></item>
        /// <item><description><b>Transient:</b> ~418ns per request. New instance for every dispatch, maximum isolation.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>When to use async handlers:</b> Use <see cref="IAsyncRequestHandler{TRequest, TResponse}"/> for operations involving:
        /// <list type="bullet">
        /// <item><description>Database queries or updates</description></item>
        /// <item><description>HTTP API calls</description></item>
        /// <item><description>File I/O operations</description></item>
        /// <item><description>Any other awaitable async operations</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Example:
        /// <code>
        /// // Singleton for stateless async operations
        /// services.AddRoutyaAsyncRequestHandler&lt;FetchWeatherRequest, WeatherData, FetchWeatherHandler&gt;(ServiceLifetime.Singleton);
        /// 
        /// // Scoped for database operations (most common)
        /// services.AddRoutyaAsyncRequestHandler&lt;CreateProductRequest, Product, CreateProductHandler&gt;(ServiceLifetime.Scoped);
        /// 
        /// // Transient for maximum isolation
        /// services.AddRoutyaAsyncRequestHandler&lt;ProcessPaymentRequest, PaymentResult, ProcessPaymentHandler&gt;(ServiceLifetime.Transient);
        /// </code>
        /// </para>
        /// </remarks>
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
        /// Registers a notification handler with the specified lifetime for optimal performance.
        /// Automatically adds the handler to the internal registry for fast dispatch.
        /// Multiple handlers can be registered for the same notification type.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification to handle, implementing <see cref="INotification"/>.</typeparam>
        /// <typeparam name="THandler">The concrete handler type implementing <see cref="INotificationHandler{TNotification}"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the handler to.</param>
        /// <param name="lifetime">The service lifetime for the handler. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
        /// <remarks>
        /// <para>This method provides superior performance compared to standard DI registration by populating the handler registry at startup.</para>
        /// <para>
        /// <b>Performance Characteristics:</b>
        /// <list type="bullet">
        /// <item><description><b>Singleton Sequential:</b> ~111ns per publish (30% faster than MediatR, 56% less memory)</description></item>
        /// <item><description><b>Singleton Parallel:</b> ~144ns per publish (9% faster than MediatR, 29% less memory)</description></item>
        /// <item><description><b>Scoped Sequential:</b> ~238ns per publish (scoped DI overhead)</description></item>
        /// <item><description><b>Scoped Parallel:</b> ~266ns per publish (scoped + parallel overhead)</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Multiple Handlers:</b> Unlike request handlers, you can register multiple notification handlers for the same notification.
        /// Handlers will execute in registration order for sequential publishing, or concurrently for parallel publishing.
        /// </para>
        /// <para>
        /// <b>Lifetime Recommendations:</b>
        /// <list type="bullet">
        /// <item><description><b>Singleton:</b> Best for stateless handlers like logging, metrics, caching. Maximum performance.</description></item>
        /// <item><description><b>Scoped:</b> Required for handlers with DbContext or scoped dependencies. Use with Scoped dispatch scope.</description></item>
        /// <item><description><b>Transient:</b> Rarely needed. Use only when handlers need isolation.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Example:
        /// <code>
        /// // Multiple handlers for the same notification
        /// services.AddRoutyaNotificationHandler&lt;UserRegisteredNotification, SendWelcomeEmailHandler&gt;(ServiceLifetime.Singleton);
        /// services.AddRoutyaNotificationHandler&lt;UserRegisteredNotification, LogAnalyticsHandler&gt;(ServiceLifetime.Singleton);
        /// services.AddRoutyaNotificationHandler&lt;UserRegisteredNotification, UpdateCrmHandler&gt;(ServiceLifetime.Scoped);
        /// 
        /// // Dispatch sequentially (handlers execute in order)
        /// await routya.PublishAsync(new UserRegisteredNotification(email));
        /// 
        /// // Dispatch in parallel (handlers execute concurrently)
        /// await routya.PublishParallelAsync(new UserRegisteredNotification(email));
        /// </code>
        /// </para>
        /// </remarks>
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
