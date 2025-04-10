using System;
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
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddRoutya(
            this IServiceCollection services, 
            Action<RoutyaDispatcherOptions>? configure = null,
            params Assembly[] scanAssemblies)
        {
            var options = new RoutyaDispatcherOptions();
            configure?.Invoke(options);

            services.AddSingleton<IRoutyaRequestDispatcher>(sp => new CompiledRequestInvokerDispatcher(sp, options));

            services.AddSingleton<IRoutyaNotificationDispatcher>(sp => new CompiledNotificationDispatcher(sp));

            services.AddSingleton<IRoutya, DefaultRoutya>();

            if (scanAssemblies?.Length > 0)
            {
                foreach (var assembly in scanAssemblies)
                {
                    RegisterRoutyaHandlersFromAssembly(services, assembly);
                }
            }

            return services;
        }

        private static void RegisterRoutyaHandlersFromAssembly(IServiceCollection services, Assembly assembly)
        {
            var allTypes = assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in allTypes)
            {
                var interfaces = type.GetInterfaces();

                foreach (var iface in interfaces)
                {
                    if (!iface.IsGenericType) continue;

                    var def = iface.GetGenericTypeDefinition();

                    if (def == typeof(IRequestHandler<,>) ||
                        def == typeof(IAsyncRequestHandler<,>) ||
                        def == typeof(INotificationHandler<>))
                    {
                        services.AddScoped(iface, type);
                    }
                }
            }
        }
    }
}
