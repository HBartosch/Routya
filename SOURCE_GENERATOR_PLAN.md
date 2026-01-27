# Routya Source Generator Implementation Plan

## 🎯 Overview

Transform Routya from runtime expression compilation to compile-time code generation, eliminating:
- DI container lookups on every dispatch
- Runtime scope creation overhead
- Expression tree compilation delays
- Reflection-based type discovery

**Target:** 30-50% performance improvement across all scenarios, fixing the scoped async regression.

---

## 📁 Project Structure

```
Routya.SourceGenerators/
├── Routya.SourceGenerators.csproj
├── Generators/
│   ├── HandlerRegistrationGenerator.cs      # Discovers and registers handlers
│   ├── DispatcherGenerator.cs               # Generates optimized dispatchers
│   ├── InterceptorGenerator.cs              # C# interceptor support (.NET 8+)
│   └── DiagnosticDescriptors.cs             # Analyzer diagnostics
├── Models/
│   ├── HandlerDescriptor.cs                 # Handler metadata
│   ├── BehaviorDescriptor.cs                # Pipeline behavior metadata
│   └── DispatcherDescriptor.cs              # Dispatcher configuration
├── Emitters/
│   ├── HandlerRegistrationEmitter.cs        # Emits registration code
│   ├── TypedDispatcherEmitter.cs            # Emits dispatcher implementations
│   └── InterceptorEmitter.cs                # Emits interceptor code
├── Analyzers/
│   ├── HandlerLifetimeAnalyzer.cs           # Validates handler lifetimes
│   ├── CircularDependencyAnalyzer.cs        # Detects circular dependencies
│   └── PerformanceAnalyzer.cs               # Suggests optimizations
└── Templates/
    ├── HandlerRegistration.template         # Registration code template
    ├── TypedDispatcher.template             # Dispatcher code template
    └── Interceptor.template                  # Interceptor code template

Routya.SourceGenerators.Tests/
├── GeneratorTests/
│   ├── HandlerRegistrationGeneratorTests.cs
│   ├── DispatcherGeneratorTests.cs
│   └── InterceptorGeneratorTests.cs
└── AnalyzerTests/
    ├── HandlerLifetimeAnalyzerTests.cs
    └── CircularDependencyAnalyzerTests.cs
```

---

## 🔧 Core Components

### 1. HandlerRegistrationGenerator

**Purpose:** Scan assemblies for handlers and generate compile-time registration.

**Input Detection:**
- Scans for types implementing `IRequestHandler<,>`, `IAsyncRequestHandler<,>`, `INotificationHandler<>`
- Respects `[RoutyaHandler]` attribute for explicit opt-in
- Detects service lifetimes from DI attributes or conventions

**Output:**
```csharp
// Auto-generated: RoutyaGenerated.Registration.g.cs
namespace Routya.Generated
{
    using Microsoft.Extensions.DependencyInjection;
    using Routya.Core.Abstractions;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Auto-generated handler registration for Routya.
    /// </summary>
    public static partial class RoutyaGeneratedExtensions
    {
        /// <summary>
        /// Registers all auto-discovered handlers and dispatchers.
        /// </summary>
        public static IServiceCollection AddGeneratedRoutya(
            this IServiceCollection services)
        {
            // Handler registrations
            services.AddScoped<IAsyncRequestHandler<GetUserRequest, User>, GetUserHandler>();
            services.AddTransient<IRequestHandler<DeleteUserRequest, bool>, DeleteUserHandler>();
            services.AddScoped<INotificationHandler<UserCreatedNotification>, EmailNotificationHandler>();
            services.AddScoped<INotificationHandler<UserCreatedNotification>, AuditLogHandler>();

            // Type-specific dispatcher registrations
            services.AddSingleton<ITypedRequestDispatcher<GetUserRequest, User>, GetUserRequestDispatcher>();
            services.AddSingleton<ITypedRequestDispatcher<DeleteUserRequest, bool>, DeleteUserRequestDispatcher>();
            
            // Notification dispatcher registrations
            services.AddSingleton<ITypedNotificationDispatcher<UserCreatedNotification>, UserCreatedNotificationDispatcher>();

            // Handler metadata registry
            services.AddSingleton<IRoutyaHandlerRegistry>(sp => new RoutyaHandlerRegistry
            {
                RequestHandlers = new Dictionary<Type, HandlerMetadata>
                {
                    [typeof(GetUserRequest)] = new HandlerMetadata
                    {
                        RequestType = typeof(GetUserRequest),
                        ResponseType = typeof(User),
                        HandlerType = typeof(GetUserHandler),
                        IsAsync = true,
                        Lifetime = ServiceLifetime.Scoped,
                        DispatcherType = typeof(GetUserRequestDispatcher)
                    },
                    [typeof(DeleteUserRequest)] = new HandlerMetadata
                    {
                        RequestType = typeof(DeleteUserRequest),
                        ResponseType = typeof(bool),
                        HandlerType = typeof(DeleteUserHandler),
                        IsAsync = false,
                        Lifetime = ServiceLifetime.Transient,
                        DispatcherType = typeof(DeleteUserRequestDispatcher)
                    }
                },
                NotificationHandlers = new Dictionary<Type, List<HandlerMetadata>>
                {
                    [typeof(UserCreatedNotification)] = new List<HandlerMetadata>
                    {
                        new HandlerMetadata
                        {
                            RequestType = typeof(UserCreatedNotification),
                            HandlerType = typeof(EmailNotificationHandler),
                            Lifetime = ServiceLifetime.Scoped
                        },
                        new HandlerMetadata
                        {
                            RequestType = typeof(UserCreatedNotification),
                            HandlerType = typeof(AuditLogHandler),
                            Lifetime = ServiceLifetime.Scoped
                        }
                    }
                }
            });

            // Main dispatcher
            services.AddScoped<IRoutya, RoutyaGeneratedDispatcher>();

            return services;
        }
    }
}
```

**Algorithm:**
1. Use `IIncrementalGenerator` for efficient incremental generation
2. Collect all syntax nodes implementing handler interfaces
3. Analyze semantic model for type information
4. Group handlers by request type
5. Detect pipeline behaviors
6. Emit registration code

---

### 2. TypedDispatcherGenerator

**Purpose:** Generate zero-overhead, type-specific dispatchers.

**Output Example:**
```csharp
// Auto-generated: RoutyaGenerated.Dispatchers.GetUserRequest.g.cs
namespace Routya.Generated.Dispatchers
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Routya.Core.Abstractions;

    /// <summary>
    /// Optimized dispatcher for <see cref="GetUserRequest"/>.
    /// </summary>
    internal sealed class GetUserRequestDispatcher : ITypedRequestDispatcher<GetUserRequest, User>
    {
        // OPTIMIZATION: Direct field injection - no DI lookups at runtime
        private readonly GetUserHandler _handler;
        private readonly LoggingBehavior<GetUserRequest, User>? _loggingBehavior;
        private readonly ValidationBehavior<GetUserRequest, User>? _validationBehavior;
        private readonly IServiceScopeFactory? _scopeFactory;

        public GetUserRequestDispatcher(
            GetUserHandler handler,  // Scoped
            LoggingBehavior<GetUserRequest, User>? loggingBehavior,  // Singleton
            ValidationBehavior<GetUserRequest, User>? validationBehavior,  // Singleton
            IServiceScopeFactory? scopeFactory)
        {
            _handler = handler;
            _loggingBehavior = loggingBehavior;
            _validationBehavior = validationBehavior;
            _scopeFactory = scopeFactory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<User> DispatchAsync(GetUserRequest request, CancellationToken cancellationToken)
        {
            // OPTIMIZATION: Inline behavior chain - JIT can devirtualize and inline
            if (_loggingBehavior != null && _validationBehavior != null)
            {
                // Two behaviors - most common scenario
                return await _loggingBehavior.Handle(
                    request,
                    ct1 => _validationBehavior.Handle(
                        request,
                        ct2 => _handler.HandleAsync(request, ct2),
                        ct1
                    ),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            else if (_loggingBehavior != null)
            {
                // Single behavior
                return await _loggingBehavior.Handle(
                    request,
                    ct => _handler.HandleAsync(request, ct),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            else if (_validationBehavior != null)
            {
                // Single behavior
                return await _validationBehavior.Handle(
                    request,
                    ct => _handler.HandleAsync(request, ct),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            else
            {
                // No behaviors - direct invocation
                return await _handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
```

**Key Optimizations:**
1. **Direct field injection:** No `GetService<T>()` calls at runtime
2. **Inline behavior chains:** JIT can devirtualize and inline
3. **AggressiveInlining:** Forces method inlining
4. **ConfigureAwait(false):** Prevents sync context capture
5. **Compile-time behavior count:** Fast paths for 0, 1, 2 behaviors

**Smart Scope Optimization:**
```csharp
// ADVANCED: Only create scope if handler requires it and behaviors don't
public async Task<User> DispatchAsync(GetUserRequest request, CancellationToken cancellationToken)
{
    // Handler is Scoped, behaviors are Singleton
    // → Behaviors can use root provider, only handler needs scope
    
    // OPTIMIZATION: Scope creation deferred and minimized
    using var scope = _scopeFactory!.CreateAsyncScope();
    var scopedHandler = scope.ServiceProvider.GetRequiredService<GetUserHandler>();
    
    // Behaviors injected from root provider (constructor)
    return await _loggingBehavior!.Handle(
        request,
        ct1 => _validationBehavior!.Handle(
            request,
            ct2 => scopedHandler.HandleAsync(request, ct2),
            ct1
        ),
        cancellationToken
    ).ConfigureAwait(false);
}
```

---

### 3. InterceptorGenerator (.NET 8+)

**Purpose:** Use C# interceptors for compile-time method resolution.

**User Code:**
```csharp
public class UserController
{
    private readonly IRoutya _routya;

    public async Task<User> GetUser(int id)
    {
        var request = new GetUserRequest { Id = id };
        return await _routya.SendAsync<GetUserRequest, User>(request);  // ← INTERCEPTED
    }
}
```

**Generated Interceptor:**
```csharp
// Auto-generated: RoutyaGenerated.Interceptors.g.cs
namespace Routya.Generated.Interceptors
{
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Routya.Core.Abstractions;

    public static class RoutyaInterceptors
    {
        [InterceptsLocation(@"C:\Projects\MyApp\Controllers\UserController.cs", line: 12, character: 31)]
        public static Task<User> SendAsync_GetUserRequest_Interceptor(
            this IRoutya routya,
            GetUserRequest request,
            CancellationToken cancellationToken = default)
        {
            // OPTIMIZATION: Zero abstraction - direct dispatcher invocation
            var impl = (RoutyaGeneratedDispatcher)routya;
            return impl.GetTypedDispatcher<GetUserRequest, User>().DispatchAsync(request, cancellationToken);
        }
    }
}
```

**Benefits:**
- ⏭️ **Zero abstraction cost** - compile-time resolution
- ⏭️ **Type safety** - compiler verifies all calls
- ⏭️ **No reflection** - direct method invocation
- ⏭️ **Debuggable** - generated code visible in IDE

---

### 4. RoutyaGeneratedDispatcher

**Purpose:** Main dispatcher implementation using generated code.

**Output:**
```csharp
// Auto-generated: RoutyaGenerated.Dispatcher.g.cs
namespace Routya.Generated
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Routya.Core.Abstractions;

    internal sealed class RoutyaGeneratedDispatcher : IRoutya
    {
        private readonly IServiceProvider _serviceProvider;
        
        // Type-specific dispatcher cache (injected via DI)
        private readonly GetUserRequestDispatcher _getUserRequestDispatcher;
        private readonly DeleteUserRequestDispatcher _deleteUserRequestDispatcher;
        private readonly UserCreatedNotificationDispatcher _userCreatedNotificationDispatcher;

        public RoutyaGeneratedDispatcher(
            IServiceProvider serviceProvider,
            GetUserRequestDispatcher getUserRequestDispatcher,
            DeleteUserRequestDispatcher deleteUserRequestDispatcher,
            UserCreatedNotificationDispatcher userCreatedNotificationDispatcher)
        {
            _serviceProvider = serviceProvider;
            _getUserRequestDispatcher = getUserRequestDispatcher;
            _deleteUserRequestDispatcher = deleteUserRequestDispatcher;
            _userCreatedNotificationDispatcher = userCreatedNotificationDispatcher;
        }

        public TResponse Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<TResponse>
        {
            // OPTIMIZATION: Compile-time type switch - JIT generates jump table
            return typeof(TRequest) switch
            {
                Type t when t == typeof(DeleteUserRequest) => 
                    (TResponse)(object)_deleteUserRequestDispatcher.Dispatch((DeleteUserRequest)(object)request),
                
                _ => throw new InvalidOperationException($"No handler registered for {typeof(TRequest).Name}")
            };
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest<TResponse>
        {
            // OPTIMIZATION: Compile-time type switch - zero allocation
            return typeof(TRequest) switch
            {
                Type t when t == typeof(GetUserRequest) => 
                    (Task<TResponse>)(object)_getUserRequestDispatcher.DispatchAsync(
                        (GetUserRequest)(object)request, 
                        cancellationToken),
                
                Type t when t == typeof(DeleteUserRequest) =>
                    Task.FromResult((TResponse)(object)_deleteUserRequestDispatcher.Dispatch(
                        (DeleteUserRequest)(object)request)),
                
                _ => throw new InvalidOperationException($"No handler registered for {typeof(TRequest).Name}")
            };
        }

        public Task PublishAsync<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return typeof(TNotification) switch
            {
                Type t when t == typeof(UserCreatedNotification) =>
                    _userCreatedNotificationDispatcher.PublishAsync(
                        (UserCreatedNotification)(object)notification,
                        cancellationToken),
                
                _ => throw new InvalidOperationException($"No handlers registered for {typeof(TNotification).Name}")
            };
        }

        public Task PublishParallelAsync<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return typeof(TNotification) switch
            {
                Type t when t == typeof(UserCreatedNotification) =>
                    _userCreatedNotificationDispatcher.PublishParallelAsync(
                        (UserCreatedNotification)(object)notification,
                        cancellationToken),
                
                _ => throw new InvalidOperationException($"No handlers registered for {typeof(TNotification).Name}")
            };
        }

        // Helper for interceptor support
        public ITypedRequestDispatcher<TRequest, TResponse> GetTypedDispatcher<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return typeof(TRequest) switch
            {
                Type t when t == typeof(GetUserRequest) =>
                    (ITypedRequestDispatcher<TRequest, TResponse>)(object)_getUserRequestDispatcher,
                
                _ => throw new InvalidOperationException($"No dispatcher found for {typeof(TRequest).Name}")
            };
        }
    }
}
```

**Performance Characteristics:**
- ⏭️ **Type switch compiled to jump table** by JIT
- ⏭️ **Zero allocations** - direct field access
- ⏭️ **No dictionary lookups** - compile-time resolution
- ⏭️ **Inline potential** - all methods can be inlined

---

## 📊 Performance Comparison

### Current Runtime Approach

```csharp
// Runtime: ~460ns for scoped async
public async Task<TResponse> SendAsync<TRequest, TResponse>(
    TRequest request,
    CancellationToken ct)
{
    // 1. Registry lookup (cached, but dictionary overhead)
    var pipeline = CompiledPipelineFactory.GetOrAdd<TRequest, TResponse>(_registry);
    
    // 2. Scope creation (allocates ServiceProviderEngineScope)
    using var scope = _provider.CreateScope();  // ~80-100ns overhead
    
    // 3. Pipeline invocation (delegate call)
    return await pipeline(scope.ServiceProvider, request, ct);  // Calls into:
    
    // 4. DI resolution (GetService lookup)
    var handler = sp.GetRequiredService<IAsyncRequestHandler<...>>();  // ~50-70ns
    
    // 5. Behavior resolution (GetServices + ToArray)
    var behaviors = sp.GetServices<IPipelineBehavior<...>>().ToArray();  // ~40-60ns
    
    // 6. Behavior chain execution (closure allocations)
    RequestHandlerDelegate<TResponse> next = ...;  // ~30-50ns in closures
    return await behaviors[0].Handle(request, next, ct);
}
```

**Total overhead:** ~200-280ns  
**Allocations:** 1248 bytes (scope + delegates + closures)

### Source-Generated Approach

```csharp
// Generated: ~300-320ns for scoped async
public async Task<User> SendAsync(
    GetUserRequest request,
    CancellationToken ct)
{
    // 1. Direct dispatcher field access (0ns - inlined)
    var dispatcher = _getUserRequestDispatcher;
    
    // 2. Direct method call (0ns - inlined)
    return await dispatcher.DispatchAsync(request, ct);
    
    // Inside dispatcher (all fields injected at construction time):
    // 3. Direct handler field access (0ns - inlined)
    // 4. Direct behavior field access (0ns - inlined)
    // 5. Inline behavior chain (0ns - devirtualized)
    return await _loggingBehavior.Handle(
        request,
        ct1 => _handler.HandleAsync(request, ct1),
        ct
    );
}
```

**Total overhead:** ~20-40ns (only async state machine)  
**Allocations:** 600-800 bytes (only async state machine)

**Improvement:**
- ⏭️ **Latency:** 140-160ns savings (-30-35%)
- ⏭️ **Allocations:** 448-648 bytes savings (-35-52%)
- ⏭️ **Throughput:** 50-75% higher requests/second

---

## 🧪 Testing Strategy

### Unit Tests

```csharp
[Fact]
public void Generator_DiscoversRequestHandlers()
{
    var source = @"
        using Routya.Core.Abstractions;
        
        public class GetUserRequest : IRequest<User> { }
        public class User { }
        public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, User>
        {
            public Task<User> HandleAsync(GetUserRequest request, CancellationToken ct)
                => Task.FromResult(new User());
        }
    ";
    
    var compilation = CreateCompilation(source);
    var generator = new HandlerRegistrationGenerator();
    
    var driver = CSharpGeneratorDriver.Create(generator);
    driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
    
    var generatedFiles = driver.GetRunResult().GeneratedTrees;
    Assert.Contains(generatedFiles, f => f.FilePath.Contains("RoutyaGenerated.Registration.g.cs"));
    
    var registrationCode = generatedFiles.First().GetText().ToString();
    Assert.Contains("AddScoped<IAsyncRequestHandler<GetUserRequest, User>, GetUserHandler>", registrationCode);
}
```

### Integration Tests

```csharp
[Fact]
public async Task GeneratedDispatcher_ExecutesHandler()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddGeneratedRoutya();  // Generated extension method
    var provider = services.BuildServiceProvider();
    var routya = provider.GetRequiredService<IRoutya>();
    
    // Act
    var request = new GetUserRequest { Id = 123 };
    var result = await routya.SendAsync<GetUserRequest, User>(request);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(123, result.Id);
}
```

### Performance Regression Tests

```csharp
[Fact]
public void GeneratedDispatcher_IsFasterThanRuntimeDispatcher()
{
    var generatedTime = BenchmarkRunner.Run<GeneratedDispatcherBenchmark>();
    var runtimeTime = BenchmarkRunner.Run<RuntimeDispatcherBenchmark>();
    
    Assert.True(generatedTime.Mean < runtimeTime.Mean * 0.7,
        "Generated dispatcher should be at least 30% faster");
}
```

---

## 📦 NuGet Package Structure

### Routya.Core 3.0.0

**Changes:**
- Add `ITypedRequestDispatcher<TRequest, TResponse>` interface
- Add `ITypedNotificationDispatcher<TNotification>` interface
- Add `IRoutyaHandlerRegistry` interface
- Mark `CompiledRequestInvokerDispatcher` as `[Obsolete]` (still available for non-SG scenarios)

### Routya.SourceGenerators 3.0.0 (NEW)

**Contents:**
- `HandlerRegistrationGenerator`
- `DispatcherGenerator`
- `InterceptorGenerator` (.NET 8+ only)
- Roslyn analyzers for validation

**Dependencies:**
- `Microsoft.CodeAnalysis.CSharp` 4.8.0+
- `Routya.Core` 3.0.0

---

## 🚀 Migration Guide

### Existing Users (v2 → v3 - No Breaking Changes)

**Current Code (v2):**
```csharp
services.AddRoutya(cfg =>
{
    cfg.Scope = RoutyaDispatchScope.Scoped;
    cfg.HandlerLifetime = ServiceLifetime.Scoped;
}, Assembly.GetExecutingAssembly());
```

**Remains Compatible (v3):**
Same code works - uses runtime dispatcher (no performance regression)

### Opt-In to Source Generation (v3)

**Step 1:** Add NuGet package
```bash
dotnet add package Routya.SourceGenerators --version 3.0.0
```

**Step 2:** Replace registration
```csharp
// Before
services.AddRoutya(cfg => { ... }, Assembly.GetExecutingAssembly());

// After
services.AddGeneratedRoutya();  // Auto-generated extension method
```

**Step 3:** Build and verify
```bash
dotnet build
# Generated files appear in obj/Debug/net8.0/generated/
```

**Performance Gain:** 30-50% improvement automatically!

---

## 📝 Implementation Checklist

### Week 1: Foundation
- [ ] Create `Routya.SourceGenerators` project
- [ ] Set up incremental generator infrastructure
- [ ] Implement `HandlerDescriptor` model
- [ ] Write handler discovery logic
- [ ] Test handler discovery with unit tests

### Week 2: Registration Generation
- [ ] Implement `HandlerRegistrationEmitter`
- [ ] Generate `AddGeneratedRoutya()` extension
- [ ] Generate handler registry
- [ ] Write integration tests
- [ ] Validate registration in sample app

### Week 3: Dispatcher Generation
- [ ] Implement `TypedDispatcherEmitter`
- [ ] Generate request dispatchers
- [ ] Generate notification dispatchers
- [ ] Inline behavior chains
- [ ] Test dispatcher generation

### Week 4: Main Dispatcher
- [ ] Generate `RoutyaGeneratedDispatcher`
- [ ] Implement type switch logic
- [ ] Add error handling
- [ ] Test end-to-end dispatching
- [ ] Benchmark vs runtime version

### Week 5: Optimizations
- [ ] Implement scope elimination
- [ ] Add `AggressiveInlining` attributes
- [ ] Optimize allocation paths
- [ ] Profile with BenchmarkDotNet
- [ ] Validate 30%+ improvement

### Week 6: Interceptors (.NET 8+)
- [ ] Implement `InterceptorGenerator`
- [ ] Generate intercept locations
- [ ] Test interceptor calls
- [ ] Validate zero abstraction cost
- [ ] Document interceptor usage

### Week 7: Analyzers & Validation
- [ ] Implement `HandlerLifetimeAnalyzer`
- [ ] Implement `CircularDependencyAnalyzer`
- [ ] Add diagnostic messages
- [ ] Test analyzer warnings
- [ ] Document best practices

### Week 8: Testing & Documentation
- [ ] Comprehensive unit tests (90%+ coverage)
- [ ] Integration tests with sample apps
- [ ] Performance regression tests
- [ ] Migration guide
- [ ] API documentation

### Week 9: Release
- [ ] NuGet package preparation
- [ ] Version 3.0.0 release notes
- [ ] Blog post: "Routya 3.0: 30% Faster with Source Generators"
- [ ] Community announcement
- [ ] Monitor feedback and issues

---

## 🎯 Success Metrics

**Performance:**
- ✅ All scenarios faster than MediatR
- ✅ Scoped async: 300-320ns (vs 460ns current, vs 415ns MediatR)
- ✅ 30-50% reduction in allocations

**Compatibility:**
- ✅ 100% backward compatible with v2
- ✅ Opt-in source generation
- ✅ No breaking changes to public API

**Quality:**
- ✅ 90%+ code coverage
- ✅ Zero analyzer warnings
- ✅ Comprehensive documentation
- ✅ Sample projects demonstrating usage

**Adoption:**
- ✅ Migration guide published
- ✅ Community feedback positive
- ✅ NuGet downloads trending up

---

**Status:** Ready for Implementation  
**Priority:** HIGH  
**Owner:** TBD  
**Target Release:** Q2 2026
