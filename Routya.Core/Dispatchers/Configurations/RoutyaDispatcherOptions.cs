using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Extensions;

namespace Routya.Core.Dispatchers.Configurations
{
    /// <summary>
    /// Configuration options for the Routya dispatcher, controlling scope management and handler lifetime strategies.
    /// </summary>
    public class RoutyaDispatcherOptions
    {
        /// <summary>
        /// Gets or sets the scope strategy used for resolving handlers during dispatch operations.
        /// </summary>
        /// <value>The default value is <see cref="RoutyaDispatchScope.Scoped"/>.</value>
        /// <remarks>
        /// <para>
        /// <b>Scoped (Default):</b> Creates a new DI scope for each dispatch. Safe for handlers with scoped dependencies like DbContext.
        /// Performance: ~440ns per request.
        /// </para>
        /// <para>
        /// <b>Root:</b> Resolves handlers from the root provider without creating a scope. Fastest option (~334ns) but requires all handlers to be Singleton or Transient.
        /// Will fail if handlers are registered as Scoped or depend on scoped services.
        /// </para>
        /// <para>
        /// Example:
        /// <code>
        /// services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Root, Assembly.GetExecutingAssembly());
        /// </code>
        /// </para>
        /// </remarks>
        public RoutyaDispatchScope Scope { get; set; } = RoutyaDispatchScope.Scoped;
        
        /// <summary>
        /// Gets or sets the default lifetime for handlers when using assembly scanning registration.
        /// Only applies when registering handlers via <c>AddRoutya(configure, scanAssemblies)</c>.
        /// </summary>
        /// <value>The default value is <see cref="ServiceLifetime.Scoped"/>.</value>
        /// <remarks>
        /// <para>
        /// <b>Scoped (Default):</b> One instance per scope. Safe for handlers with DbContext or other scoped dependencies.
        /// Performance: ~440ns per request, ~238ns per notification.
        /// </para>
        /// <para>
        /// <b>Singleton:</b> Single instance for the application lifetime. Fastest option for stateless handlers.
        /// Performance: ~334ns per request, ~111ns per notification (30% faster than MediatR).
        /// ⚠️ Handlers must be thread-safe and stateless.
        /// </para>
        /// <para>
        /// <b>Transient:</b> New instance for every dispatch. Maximum isolation but slightly slower.
        /// Performance: ~336ns per request, ~146ns per notification.
        /// </para>
        /// <para>
        /// For fine-grained control, use <c>AddRoutyaAsyncRequestHandler&lt;TRequest, TResponse, THandler&gt;(lifetime)</c> or
        /// <c>AddRoutyaNotificationHandler&lt;TNotification, THandler&gt;(lifetime)</c> to specify lifetime per handler.
        /// </para>
        /// <para>
        /// Example:
        /// <code>
        /// // All handlers as Singleton (fastest)
        /// services.AddRoutya(cfg => cfg.HandlerLifetime = ServiceLifetime.Singleton, Assembly.GetExecutingAssembly());
        /// 
        /// // Mixed lifetimes (recommended for optimal performance)
        /// services.AddRoutya(); // No assembly scanning
        /// services.AddRoutyaAsyncRequestHandler&lt;CreateProductRequest, Product, CreateProductHandler&gt;(ServiceLifetime.Singleton);
        /// services.AddRoutyaRequestHandler&lt;GetProductRequest, Product?, GetProductHandler&gt;(ServiceLifetime.Scoped);
        /// </code>
        /// </para>
        /// </remarks>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Scoped;
    }
}