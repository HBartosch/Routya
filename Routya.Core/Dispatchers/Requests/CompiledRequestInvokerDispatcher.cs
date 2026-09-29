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
    /// Request dispatcher that builds one cached dispatch delegate per request type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delegate is built once per request type and reused, so the handler lookup is not repeated
    /// on every dispatch. Handlers described in the registry, meaning those registered through the
    /// <c>AddRoutya*Handler</c> methods or by assembly scanning, are resolved directly by their
    /// concrete type. Anything else is resolved through its handler interface.
    /// </para>
    /// <para>
    /// No expression trees are built or compiled. Everything here is ordinary delegates and generic
    /// dispatch, which keeps the type free of the runtime code generation that trimming and Native
    /// AOT cannot support.
    /// </para>
    /// </remarks>
    public class CompiledRequestInvokerDispatcher : IRoutyaRequestDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly System.Collections.Generic.Dictionary<Type, RequestHandlerInfo> _requestHandlerRegistry;

        // Owned per dispatcher, and therefore per DI container, so that no pipeline state is
        // shared between containers living in the same process.
        private readonly CompiledPipelineFactory _pipelineFactory;

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
            _pipelineFactory = new CompiledPipelineFactory(requestHandlerRegistry);
        }

        /// <inheritdoc />
        public TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>
        {
            var pipeline = _pipelineFactory.GetOrAddSync<TRequest, TResponse>();

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
            var pipeline = _pipelineFactory.GetOrAdd<TRequest, TResponse>();

            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();
                return await pipeline(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
            }

            return await pipeline(_provider, request, cancellationToken).ConfigureAwait(false);
        }
    }
}