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
    /// <summary>
    /// High-performance request dispatcher using compiled expression trees for fast handler invocation.
    /// </summary>
    /// <remarks>
    /// This dispatcher uses a registry-based approach with compiled expressions to eliminate reflection overhead.
    /// Handlers are resolved from the registry first, falling back to standard DI resolution if not found.
    /// </remarks>
    public class CompiledRequestInvokerDispatcher : IRoutyaRequestDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly System.Collections.Generic.Dictionary<Type, RequestHandlerInfo> _requestHandlerRegistry;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledRequestInvokerDispatcher"/> class.
        /// </summary>
        /// <param name="provider">The service provider for resolving handlers.</param>
        /// <param name="requestHandlerRegistry">The registry containing pre-registered handler information.</param>
        /// <param name="options">Configuration options for the dispatcher.</param>
        public CompiledRequestInvokerDispatcher(
            IServiceProvider provider, 
            System.Collections.Generic.Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry,
            RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _requestHandlerRegistry = requestHandlerRegistry;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = CompiledPipelineFactory.GetOrAdd<TRequest, TResponse>(_requestHandlerRegistry);

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                return await pipeline(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }

            return await pipeline(_provider, request, cancellationToken).ConfigureAwait(false);
        }
    }
}