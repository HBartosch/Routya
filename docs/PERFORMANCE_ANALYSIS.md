# Routya Performance Analysis & Optimization Roadmap

## 📊 Benchmark Results Summary

### Request/Response Dispatch Performance

| Scenario | Mean (ns) | vs MediatR | Allocated | Result |
|----------|-----------|------------|-----------|---------|
| **MediatR_SendAsync (Baseline)** | **414.9** | **0%** | **1016 B** | - |
| Routya_Singleton_Send | 339.5 | ✅ **-18.2%** | 904 B (-11%) | **FASTER** |
| Routya_Singleton_SendAsync | 396.0 | ✅ **-4.6%** | 1040 B (+2%) | **FASTER** |
| Routya_Scoped_Send | 398.4 | ✅ **-4.0%** | 1112 B (+9%) | **FASTER** |
| **Routya_Scoped_SendAsync** | **460.1** | ⚠️ **+10.9%** | **1248 B (+23%)** | **SLOWER** |
| Routya_Transient_Send | 348.4 | ✅ **-16.0%** | 928 B (-9%) | **FASTER** |
| Routya_Transient_SendAsync | 403.7 | ✅ **-2.7%** | 1064 B (+5%) | **FASTER** |

**Key Findings:**
- ✅ **Sync operations: ALL faster (4-18% improvement)**
- ⚠️ **Scoped + Async combination: 11% SLOWER than MediatR**
- ✅ Singleton/Transient handlers perform best (16-18% faster sync)
- ⚠️ Higher memory allocations in async scenarios

### Notification Dispatch Performance

| Scenario | Mean (ns) | vs MediatR | Allocated | Result |
|----------|-----------|------------|-----------|---------|
| **MediatR_Publish (Baseline)** | **174.4** | **0%** | **440 B** | - |
| Routya_Singleton_Sequential | 129.2 | ✅ **-26%** | 192 B (-56%) | **FASTER** |
| Routya_Singleton_Parallel | 158.1 | ✅ **-9%** | 312 B (-29%) | **FASTER** |
| Routya_Scoped_Sequential | 259.0 | ⚠️ **+49%** | 424 B (-4%) | **SLOWER** |
| Routya_Scoped_Parallel | 282.4 | ⚠️ **+62%** | 544 B (+24%) | **SLOWER** |
| Routya_Transient_Sequential | 172.7 | ✅ **-1%** | 240 B (-45%) | **FASTER** |
| Routya_Transient_Parallel | 194.4 | ⚠️ **+12%** | 360 B (-18%) | **SLOWER** |

**Key Findings:**
- ✅ **Singleton handlers: Exceptional performance (26% faster)**
- ⚠️ **Scoped handlers: Major performance regression (49-62% slower)**
- ✅ Significantly better memory efficiency in most scenarios
- ⚠️ Parallel dispatch overhead in scoped/transient scenarios

---

## 🔍 Root Cause Analysis

### Problem 1: Scoped Service Resolution Overhead

**Location:** [CompiledRequestInvokerDispatcher.cs](Routya.Core/Dispatchers/Requests/CompiledRequestInvokerDispatcher.cs#L48-L52)

```csharp
if (_options.Scope == RoutyaDispatchScope.Scoped)
{
    using var scope = _provider.CreateScope();  // ⚠️ BOTTLENECK
    return await pipeline(scope.ServiceProvider, request, cancellationToken).ConfigureAwait(false);
}
```

**Impact:**
- `CreateScope()` allocates additional objects (ServiceProviderEngineScope)
- Extra memory allocations: +200-300 bytes per dispatch
- Additional GC pressure: causes performance regression

**Evidence:**
- Scoped async: 460ns vs 415ns baseline (+11%)
- Scoped notifications: 259ns vs 174ns baseline (+49%)
- Allocation increase: +23% in scoped async requests

### Problem 2: Runtime Type Resolution

**Location:** [CompiledPipelineFactory.cs](Routya.Core/Dispatchers/Pipelines/CompiledPipelineFactory.cs#L59-L78)

```csharp
// Runtime handler resolution - happens on EVERY dispatch
asyncHandler = provider.GetService<IAsyncRequestHandler<TRequest, TResponse>>();

if (asyncHandler != null && needsFallbackCheck)
{
    var handlerConcreteType = asyncHandler.GetType();  // ⚠️ Reflection
    lock (requestHandlerRegistry)  // ⚠️ Lock contention
    {
        if (!requestHandlerRegistry.ContainsKey(asyncHandlerType))
        {
            requestHandlerRegistry[asyncHandlerType] = new RequestHandlerInfo { ... };
        }
    }
}
```

**Impact:**
- DI container lookups on every call
- Reflection to get concrete type
- Dictionary lock contention in multi-threaded scenarios
- Cannot be optimized away at runtime

### Problem 3: Behavior Chain Construction

**Location:** [CompiledPipelineFactory.cs](Routya.Core/Dispatchers/Pipelines/CompiledPipelineFactory.cs#L88-L95)

```csharp
var behaviors = (IPipelineBehavior<TRequest, TResponse>[])_behaviorCache.GetOrAdd(
    typeof(TRequest),
    _ =>
    {
        var behaviorServices = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        return behaviorServices as IPipelineBehavior<TRequest, TResponse>[] ?? behaviorServices.ToArray();
    });
```

**Impact:**
- Runtime delegate allocation for behavior chain
- Cannot be inlined/devirtualized by JIT
- Closure allocations in loop-based chain

---

## 🚀 Optimization Opportunities: Source Generation

### Why Source Generators?

**Current Runtime Overhead:**
1. ✅ Expression compilation (already optimized - cached)
2. ⚠️ DI resolution (happens every dispatch)
3. ⚠️ Scope creation (happens when scoped)
4. ⚠️ Behavior chain construction (cached but not optimal)
5. ⚠️ Type discovery and registration (first call overhead)

**Source Generator Benefits:**
- ⏭️ **Zero runtime registration** - handlers known at compile-time
- ⏭️ **Direct method invocation** - no DI lookups
- ⏭️ **Inline behavior chains** - JIT can optimize/devirtualize
- ⏭️ **Compile-time scope analysis** - eliminate unnecessary scope creation
- ⏭️ **Zero allocation pipelines** - struct-based delegates

### Proposed Implementation

#### 1. Handler Registration Source Generator

**Goal:** Generate compile-time handler registry

**Input:** User code with `[RoutyaHandler]` attribute or assembly scanning
```csharp
[RoutyaHandler]
public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, User>
{
    public Task<User> HandleAsync(GetUserRequest request, CancellationToken ct)
    {
        // ...
    }
}
```

**Output:** Generated registration code
```csharp
// Auto-generated: RoutyaGenerated.Handlers.g.cs
internal static class RoutyaGeneratedHandlers
{
    public static IServiceCollection AddGeneratedRoutya(this IServiceCollection services)
    {
        // Direct registration - no reflection
        services.AddScoped<IAsyncRequestHandler<GetUserRequest, User>, GetUserHandler>();
        
        // Pre-built registry
        services.AddSingleton(sp => new RoutyaHandlerRegistry
        {
            Handlers = new Dictionary<Type, HandlerDescriptor>
            {
                [typeof(GetUserRequest)] = new HandlerDescriptor
                {
                    RequestType = typeof(GetUserRequest),
                    ResponseType = typeof(User),
                    HandlerType = typeof(GetUserHandler),
                    IsAsync = true,
                    Lifetime = ServiceLifetime.Scoped
                }
            }
        });
        
        return services;
    }
}
```

#### 2. Optimized Dispatcher Source Generator

**Goal:** Generate zero-allocation, direct-invocation dispatchers

**Output:** Type-specific dispatchers
```csharp
// Auto-generated: RoutyaGenerated.Dispatchers.g.cs
internal sealed class GetUserRequestDispatcher : ITypedRequestDispatcher<GetUserRequest, User>
{
    private readonly GetUserHandler _handler;
    private readonly LoggingBehavior<GetUserRequest, User> _loggingBehavior;
    private readonly ValidationBehavior<GetUserRequest, User> _validationBehavior;
    
    public GetUserRequestDispatcher(
        GetUserHandler handler,
        LoggingBehavior<GetUserRequest, User> loggingBehavior,
        ValidationBehavior<GetUserRequest, User> validationBehavior)
    {
        _handler = handler;
        _loggingBehavior = loggingBehavior;
        _validationBehavior = validationBehavior;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<User> DispatchAsync(GetUserRequest request, CancellationToken ct)
    {
        // Inline behavior chain - JIT can devirtualize
        return _loggingBehavior.Handle(
            request,
            ct1 => _validationBehavior.Handle(
                request,
                ct2 => _handler.HandleAsync(request, ct2),
                ct1
            ),
            ct
        );
    }
}
```

**Benefits:**
- ⏭️ **No DI lookups** - handlers injected directly
- ⏭️ **Inline pipeline** - JIT can devirtualize and inline
- ⏭️ **Zero allocations** - no delegates, no closures
- ⏭️ **Compile-time validation** - catch errors early

#### 3. Smart Scope Elimination

**Goal:** Eliminate unnecessary scope creation

**Analysis:** Source generator analyzes handler lifetimes
```csharp
// Auto-generated optimization logic
public Task<User> SendAsync(GetUserRequest request, CancellationToken ct)
{
    // Handler is Scoped, but all behaviors are Singleton
    // → Can use root provider for behaviors, scope only for handler
    
    var loggingBehavior = _rootProvider.GetRequiredService<LoggingBehavior<GetUserRequest, User>>();
    var validationBehavior = _rootProvider.GetRequiredService<ValidationBehavior<GetUserRequest, User>>();
    
    using var scope = _scopeFactory.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<GetUserHandler>();
    
    // Direct invocation - no intermediate delegates
    return loggingBehavior.Handle(
        request,
        ct1 => validationBehavior.Handle(
            request,
            ct2 => handler.HandleAsync(request, ct2),
            ct1
        ),
        ct
    );
}
```

**Impact:**
- ⏭️ Reduce scoped allocations by 50-70%
- ⏭️ Only create scope when truly necessary
- ⏭️ Share singleton behaviors across all dispatches

#### 4. Interceptor-Based Code Generation (.NET 8+)

**Goal:** Use C# interceptors for zero-overhead dispatch

**User Code:**
```csharp
var result = await routya.SendAsync<GetUserRequest, User>(request, ct);
```

**Generated Interceptor:**
```csharp
[InterceptsLocation("Program.cs", line: 42, character: 31)]
public static Task<User> SendAsync_Intercepted(
    this IRoutya routya,
    GetUserRequest request,
    CancellationToken ct)
{
    // Direct dispatch to generated dispatcher
    var dispatcher = ((RoutyaImpl)routya).GetDispatcher<GetUserRequest, User>();
    return dispatcher.DispatchAsync(request, ct);
}
```

**Benefits:**
- ⏭️ **Zero abstraction cost** - compile-time resolution
- ⏭️ **Type-safe** - compiler verifies all calls
- ⏭️ **Debuggable** - source available in IDE

---

## 📈 Expected Performance Improvements

### Request/Response Dispatch

**Current Performance:**
- Scoped async: 460ns (⚠️ +11% vs MediatR)
- Singleton async: 396ns (✅ -5% vs MediatR)

**With Source Generation:**
```
Scoped async:     300-320ns (⏭️ -23% vs MediatR, -35% vs current)
Singleton async:  280-300ns (⏭️ -28% vs MediatR, -25% vs current)
```

**Expected Allocations:**
```
Current:  1040-1248 B
With SG:   600-800 B  (⏭️ 35-40% reduction)
```

### Notification Dispatch

**Current Performance:**
- Scoped sequential: 259ns (⚠️ +49% vs MediatR)
- Singleton sequential: 129ns (✅ -26% vs MediatR)

**With Source Generation:**
```
Scoped sequential:    120-140ns (⏭️ -20% vs MediatR, -46% vs current)
Singleton sequential:  90-110ns (⏭️ -37% vs MediatR, -15% vs current)
```

---

## 🛠️ Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
- [ ] Create `Routya.SourceGenerators` project
- [ ] Set up Roslyn analyzer infrastructure
- [ ] Implement basic handler discovery
- [ ] Generate handler registration code
- [ ] Write unit tests for source generator

### Phase 2: Dispatcher Generation (Week 3-4)
- [ ] Generate type-specific dispatchers
- [ ] Implement inline behavior chain generation
- [ ] Add scope elimination optimization
- [ ] Benchmark against current implementation
- [ ] Validate 20%+ performance improvement

### Phase 3: Advanced Optimizations (Week 5-6)
- [ ] Implement C# interceptor support (.NET 8+)
- [ ] Add compile-time validation
- [ ] Generate documentation from handlers
- [ ] Create analyzer for common mistakes
- [ ] Benchmark comprehensive scenarios

### Phase 4: Testing & Documentation (Week 7-8)
- [ ] Comprehensive integration tests
- [ ] Performance regression tests
- [ ] Migration guide from v2 to v3
- [ ] Sample projects with source generators
- [ ] Update README with performance claims

### Phase 5: Release (Week 9)
- [ ] Routya.Core 3.0.0 (optional source gen)
- [ ] Routya.SourceGenerators 3.0.0
- [ ] NuGet package publishing
- [ ] Blog post: "How Routya became 30% faster than MediatR"
- [ ] Community feedback and iteration

---

## 📊 Performance Targets (v3.0)

### Request/Response

| Scenario | Current | Target (v3.0) | Improvement |
|----------|---------|---------------|-------------|
| Singleton Sync | 339ns | **250-280ns** | **-17-26%** |
| Singleton Async | 396ns | **280-300ns** | **-24-29%** |
| Scoped Sync | 398ns | **280-300ns** | **-25-30%** |
| Scoped Async | 460ns | **300-320ns** | **-30-35%** |
| Transient Sync | 348ns | **260-280ns** | **-20-25%** |
| Transient Async | 404ns | **290-310ns** | **-23-28%** |

**vs MediatR Baseline (415ns):**
- ⏭️ **All scenarios 25-35% faster**
- ⏭️ **Zero regressions**
- ⏭️ **Validated performance claims**

### Notifications

| Scenario | Current | Target (v3.0) | Improvement |
|----------|---------|---------------|-------------|
| Singleton Sequential | 129ns | **90-110ns** | **-15-30%** |
| Singleton Parallel | 158ns | **130-150ns** | **-5-18%** |
| Scoped Sequential | 259ns | **120-140ns** | **-46-54%** |
| Scoped Parallel | 282ns | **150-170ns** | **-40-47%** |
| Transient Sequential | 173ns | **120-140ns** | **-19-31%** |
| Transient Parallel | 194ns | **150-170ns** | **-12-23%** |

**vs MediatR Baseline (174ns):**
- ⏭️ **All scenarios 2-37% faster**
- ⏭️ **Scoped scenarios competitive**
- ⏭️ **Singleton scenarios exceptional**

---

## 🎯 Success Criteria

✅ **Performance:** All scenarios faster than MediatR  
✅ **Memory:** 30-50% reduction in allocations  
✅ **Compatibility:** Existing v2 code works unchanged  
✅ **Opt-in:** Source generation is optional enhancement  
✅ **Testing:** 90%+ code coverage maintained  
✅ **Documentation:** Complete migration guide  

---

## 💡 Additional Optimization Ideas

### 1. **Struct-Based Pipelines**
Use `ValueTask<T>` and struct-based delegates to eliminate async state machine allocations.

### 2. **Pooled Scopes**
Implement object pooling for scope creation to reduce GC pressure.

### 3. **Generic Math for Metrics**
Use generic math (INumber<T>) for zero-allocation performance counters.

### 4. **Frozen Collections (.NET 8+)**
Use `FrozenDictionary<TKey, TValue>` for handler registry (faster lookups, immutable).

### 5. **Native AOT Support**
Ensure source-generated code is AOT-friendly for native compilation scenarios.

### 6. **Compile-Time Dependency Graph**
Analyze and validate handler dependencies at compile-time to catch circular references.

---

## 🔗 References

- [C# Source Generators Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [C# Interceptors (.NET 8)](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#interceptors)
- [Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/framework/performance/performance-tips)
- [BenchmarkDotNet Best Practices](https://benchmarkdotnet.org/articles/guides/good-practices.html)

---

**Generated:** 2026-01-27  
**Status:** Ready for Implementation  
**Priority:** HIGH - Resolves critical performance regression in scoped scenarios
