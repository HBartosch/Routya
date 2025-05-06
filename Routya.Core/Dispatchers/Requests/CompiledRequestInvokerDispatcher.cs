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

        public CompiledRequestInvokerDispatcher(IServiceProvider provider, RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        public TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = CompiledPipelineFactory.GetOrAddSync<TRequest, TResponse>();

            var provider = _options.Scope == RoutyaDispatchScope.Scoped
               ? _provider.CreateScope().ServiceProvider
               : _provider;

            return pipeline(provider, request);
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = CompiledPipelineFactory.GetOrAdd<TRequest, TResponse>();

            var provider = _options.Scope == RoutyaDispatchScope.Scoped
               ? _provider.CreateScope().ServiceProvider
               : _provider;

            return await pipeline(provider, request, cancellationToken);
        }
    }
}