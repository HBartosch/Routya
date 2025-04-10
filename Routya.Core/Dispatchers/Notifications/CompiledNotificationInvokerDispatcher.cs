using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Extensions;

namespace Routya.Core.Dispatchers.Notifications
{
    public class CompiledNotificationInvokerDispatcher : IRoutyaNotificationDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;

        public CompiledNotificationInvokerDispatcher(IServiceProvider provider, RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var provider = _options.Scope == RoutyaDispatchScope.Scoped
              ? _provider.CreateScope().ServiceProvider
              : _provider;

            var handlers = provider.GetServices<INotificationHandler<TNotification>>();

            foreach (var handler in handlers)
            {
                var invoker = CompiledNotificationInvokerFactory.GetOrAdd(handler.GetType(), typeof(TNotification));
                await invoker(handler, notification!, cancellationToken);
            }
        }

        public async Task PublishParallel<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var provider = _options.Scope == RoutyaDispatchScope.Scoped
              ? _provider.CreateScope().ServiceProvider
              : _provider;

            var handlers = provider.GetServices<INotificationHandler<TNotification>>();

            var tasks = handlers.Select(handler =>
            {
                var invoker = CompiledNotificationInvokerFactory.GetOrAdd(handler.GetType(), typeof(TNotification));
                return invoker(handler, notification!, cancellationToken);
            });

            await Task.WhenAll(tasks);
        }
    }
}