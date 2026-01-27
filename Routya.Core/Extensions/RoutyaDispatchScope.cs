namespace Routya.Core.Extensions
{
    /// <summary>
    /// Defines the scope strategy used for resolving handlers during request and notification dispatching.
    /// </summary>
    /// <remarks>
    /// <para>This setting controls whether Routya creates a new DI scope for each dispatch operation or uses the root service provider.</para>
    /// <para>Choose the appropriate scope based on your handler dependencies and performance requirements.</para>
    /// </remarks>
    public enum RoutyaDispatchScope
    {
        /// <summary>
        /// Creates a new dependency injection scope for each dispatch operation.
        /// </summary>
        /// <remarks>
        /// <para><b>Default and recommended setting.</b></para>
        /// <para>
        /// Use this when:
        /// - Handlers depend on scoped services (e.g., Entity Framework DbContext, IHttpContextAccessor)
        /// - You need isolation between different dispatch operations
        /// - Handlers are registered as Scoped lifetime
        /// </para>
        /// <para>
        /// Performance impact: ~440ns per request (18% overhead compared to Root)
        /// </para>
        /// <para>
        /// This is the safe choice for most applications, especially those using Entity Framework Core or other scoped dependencies.
        /// </para>
        /// </remarks>
        Scoped,

        /// <summary>
        /// Resolves handlers directly from the root service provider without creating a scope.
        /// </summary>
        /// <remarks>
        /// <para><b>Fastest option, but only suitable for stateless handlers.</b></para>
        /// <para>
        /// Use this when:
        /// - All handlers are registered as Singleton or Transient (never Scoped)
        /// - Handlers don't depend on scoped services like DbContext
        /// - You need maximum performance (~334ns per request)
        /// </para>
        /// <para>
        /// ⚠️ WARNING: Will throw an exception if any handler is registered as Scoped or depends on scoped services.
        /// </para>
        /// <para>
        /// Performance benefit: ~24% faster than Scoped (334ns vs 440ns)
        /// </para>
        /// <para>
        /// Only use this mode if you're certain all handlers and their dependencies are stateless.
        /// </para>
        /// </remarks>
        Root
    }
}