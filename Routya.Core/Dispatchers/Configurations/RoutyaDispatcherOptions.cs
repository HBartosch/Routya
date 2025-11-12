using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Extensions;

namespace Routya.Core.Dispatchers.Configurations
{
    public class RoutyaDispatcherOptions
    {
        public RoutyaDispatchScope Scope { get; set; } = RoutyaDispatchScope.Scoped;
        
        /// <summary>
        /// Handler lifetime for dependency injection.
        /// Scoped: New handler instances per scope (supports scoped dependencies, slower ~355ns)
        /// Singleton: Cached handler instances (maximum performance ~160ns, handlers must be stateless)
        /// Transient: New handler instances per request (most flexible, slowest)
        /// </summary>
        public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Scoped;
    }
}