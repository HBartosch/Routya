using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Dispatchers.Pipelines;
using Routya.Core.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    public class CompiledRequestInvokerDispatcher : IRoutyaRequestDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly System.Collections.Generic.Dictionary<Type, RequestHandlerInfo> _requestHandlerRegistry;

        public CompiledRequestInvokerDispatcher(
            IServiceProvider provider, 
            System.Collections.Generic.Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry,
            RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _requestHandlerRegistry = requestHandlerRegistry;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        public TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = CompiledPipelineFactory.GetOrAddSync<TRequest, TResponse>(_requestHandlerRegistry);

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                return pipeline(scope.ServiceProvider, request);
            }

            return pipeline(_provider, request);
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = CompiledPipelineFactory.GetOrAdd<TRequest, TResponse>(_requestHandlerRegistry);

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                return await pipeline(scope.ServiceProvider, request, cancellationToken);
            }

            return await pipeline(_provider, request, cancellationToken);
        }
    }
}