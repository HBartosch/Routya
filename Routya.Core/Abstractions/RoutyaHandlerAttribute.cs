using System;
using Microsoft.Extensions.DependencyInjection;

namespace Routya.Core.Abstractions
{
    /// <summary>
    /// Sets the service lifetime the source generator registers this handler with, overriding the
    /// lifetime passed to <c>AddGeneratedRoutya</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apply this only to handlers that genuinely differ from the rest. For a solution wide choice,
    /// pass the lifetime to <c>AddGeneratedRoutya</c> instead:
    /// <code>
    /// services.AddGeneratedRoutya(ServiceLifetime.Scoped);
    /// </code>
    /// </para>
    /// <para>
    /// Example, where one handler caches state for the lifetime of the application while the rest
    /// follow whatever <c>AddGeneratedRoutya</c> was given:
    /// <code>
    /// [RoutyaHandler(ServiceLifetime.Singleton)]
    /// public class GetExchangeRatesHandler : IAsyncRequestHandler&lt;GetExchangeRates, Rates&gt;
    /// {
    ///     // holds a cache, so one instance for the whole application
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// A <see cref="ServiceLifetime.Singleton"/> handler must be thread safe, and must not depend on
    /// a scoped service such as a <c>DbContext</c>.
    /// </para>
    /// <para>
    /// This attribute has no effect on the runtime dispatcher. Handlers registered through
    /// <c>AddRoutyaRequestHandler</c>, <c>AddRoutyaAsyncRequestHandler</c>,
    /// <c>AddRoutyaNotificationHandler</c> or <c>AddRoutyaStreamRequestHandler</c> take their
    /// lifetime from the argument passed to those methods.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    public sealed class RoutyaHandlerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoutyaHandlerAttribute"/> class.
        /// </summary>
        /// <param name="lifetime">The service lifetime to register this handler with.</param>
        public RoutyaHandlerAttribute(ServiceLifetime lifetime)
        {
            Lifetime = lifetime;
        }

        /// <summary>
        /// Gets the service lifetime this handler is registered with.
        /// </summary>
        public ServiceLifetime Lifetime { get; }
    }
}
