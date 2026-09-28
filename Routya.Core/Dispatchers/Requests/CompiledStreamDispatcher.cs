using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;
using Routya.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Requests
{
    /// <summary>
    /// Dispatches <see cref="IStreamRequest{TResponse}"/> to its handler, wrapping the enumeration
    /// in any registered <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/>.
    /// </summary>
    public sealed class CompiledStreamDispatcher : IRoutyaStreamDispatcher
    {
        private readonly IServiceProvider _provider;
        private readonly RoutyaDispatcherOptions _options;
        private readonly Dictionary<Type, RequestHandlerInfo> _requestHandlerRegistry;

        // Records whether any stream behavior is registered for a request type. Registrations are
        // immutable once the container is built, so this is safe to cache. The behavior instances
        // themselves are never cached: they may be Scoped, so they come from the dispatch scope.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _hasBehaviors
            = new System.Collections.Concurrent.ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledStreamDispatcher"/> class.
        /// </summary>
        public CompiledStreamDispatcher(
            IServiceProvider provider,
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry,
            RoutyaDispatcherOptions? options = null)
        {
            _provider = provider;
            _requestHandlerRegistry = requestHandlerRegistry;
            _options = options ?? new RoutyaDispatcherOptions();
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(
            TRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
            where TRequest : IStreamRequest<TResponse>
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // The scope must outlive the whole enumeration, not just the call that starts it.
            // Declaring it inside this iterator ties its disposal to the iterator's own disposal,
            // which happens when enumeration completes or the consumer abandons it.
            if (_options.Scope == RoutyaDispatchScope.Scoped)
            {
                using var scope = _provider.CreateScope();

                await foreach (var item in Stream<TRequest, TResponse>(scope.ServiceProvider, request, cancellationToken)
                    .ConfigureAwait(false))
                {
                    yield return item;
                }
            }
            else
            {
                await foreach (var item in Stream<TRequest, TResponse>(_provider, request, cancellationToken)
                    .ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        private IAsyncEnumerable<TResponse> Stream<TRequest, TResponse>(
            IServiceProvider provider,
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : IStreamRequest<TResponse>
        {
            var handler = ResolveHandler<TRequest, TResponse>(provider);
            var behaviors = ResolveBehaviors<TRequest, TResponse>(provider);

            if (behaviors.Length == 0)
            {
                return handler.Handle(request, cancellationToken);
            }

            StreamHandlerDelegate<TResponse> next = ct => handler.Handle(request, ct);

            // Walked in reverse so behaviors execute in registration order
            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var currentBehavior = behaviors[i];
                var currentDelegate = next;
                next = ct => currentBehavior.Handle(request, currentDelegate, ct);
            }

            return next(cancellationToken);
        }

        private IStreamRequestHandler<TRequest, TResponse> ResolveHandler<TRequest, TResponse>(IServiceProvider provider)
            where TRequest : IStreamRequest<TResponse>
        {
            var handlerInterface = typeof(IStreamRequestHandler<TRequest, TResponse>);

            // Handlers described in the registry are resolved directly by their concrete type
            if (_requestHandlerRegistry.TryGetValue(handlerInterface, out var handlerInfo))
            {
                return (IStreamRequestHandler<TRequest, TResponse>)provider.GetRequiredService(handlerInfo.ConcreteType);
            }

            var handler = provider.GetService<IStreamRequestHandler<TRequest, TResponse>>();

            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No stream handler found for request type {typeof(TRequest).Name}. "
                    + $"Register one with AddRoutyaStreamRequestHandler, or implement "
                    + $"IStreamRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");
            }

            return handler;
        }

        private IStreamPipelineBehavior<TRequest, TResponse>[] ResolveBehaviors<TRequest, TResponse>(IServiceProvider provider)
        {
            if (_hasBehaviors.TryGetValue(typeof(TRequest), out var hasBehaviors) && !hasBehaviors)
            {
                return EmptyBehaviors<TRequest, TResponse>.Instance;
            }

            var resolved = provider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();
            var behaviors = resolved as IStreamPipelineBehavior<TRequest, TResponse>[] ?? resolved.ToArray();

            _hasBehaviors[typeof(TRequest)] = behaviors.Length > 0;

            return behaviors;
        }

        private static class EmptyBehaviors<TRequest, TResponse>
        {
            public static readonly IStreamPipelineBehavior<TRequest, TResponse>[] Instance =
                new IStreamPipelineBehavior<TRequest, TResponse>[0];
        }
    }
}
