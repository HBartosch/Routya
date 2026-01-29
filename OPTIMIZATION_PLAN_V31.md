# Routya v3.1 Optimization Plan
## Goal: Type-Specific Generated Dispatchers

### Problem
Current v3.0 source generator only optimizes **registration**, but still uses runtime `DefaultRoutya` dispatcher:
- Request dispatch: 349.8 ns (20% slower than v2.0)
- Generic type resolution overhead
- Dictionary lookups per call
- No compile-time optimization

### Solution: Generate Specialized Dispatch Methods

Instead of runtime generic dispatch, generate type-specific methods at compile-time.

---

## Architecture Changes

### Current Flow (v2.0 & v3.0)
```csharp
// User code
var result = await dispatcher.SendAsync<TestRequest, string>(request);

// Runtime execution in DefaultRoutya
public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
{
    // 1. Dictionary lookup for handler type
    var handlerInfo = _handlerRegistry[typeof(IAsyncRequestHandler<TRequest, TResponse>)];
    
    // 2. Resolve from DI container
    var handler = (IAsyncRequestHandler<TRequest, TResponse>)_serviceProvider.GetRequiredService(handlerInfo.InterfaceType);
    
    // 3. Invoke
    return await handler.HandleAsync(request, ct);
}
```

**Overhead Sources:**
- `typeof()` calls
- Dictionary lookup
- Cast operations
- Generic method overhead

---

### Proposed Flow (v3.1)

#### Option 1: Extend IRoutya with Generated Methods

```csharp
// Generated in RoutyaGeneratedExtensions.g.cs
public static class GeneratedRoutyaDispatchers
{
    public static async Task<string> SendTestRequestAsync(
        this IRoutya routya, 
        TestRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Direct, zero-overhead dispatch
        var sp = ((DefaultRoutya)routya).ServiceProvider;
        var handler = sp.GetRequiredService<TestRequestHandler>();
        return await handler.HandleAsync(request, cancellationToken);
    }
}

// User code (optimized)
var result = await dispatcher.SendTestRequestAsync(request);
```

**Benefits:**
- Zero dictionary lookups
- Direct handler resolution
- No generic overhead
- Compile-time type safety

**Estimated Performance:** ~200-220 ns (match MediatR)

---

#### Option 2: Generate Dedicated Dispatcher Class

```csharp
// Generated dispatcher
public sealed class GeneratedRoutya : IRoutya
{
    private readonly IServiceProvider _serviceProvider;
    
    // Generic fallback (for non-generated types)
    public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
    {
        // Fallback to runtime dispatch
        return DefaultRoutya.SendAsync<TRequest, TResponse>(request, ct);
    }
    
    // Type-specific overload (優先 resolution)
    public async Task<string> SendAsync(TestRequest request, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<TestRequestHandler>();
        return await handler.HandleAsync(request, ct);
    }
    
    // Notification dispatch
    public async Task PublishAsync(TestNotification notification, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler1 = scope.ServiceProvider.GetRequiredService<RoutyaNotificationHandler1>();
        var handler2 = scope.ServiceProvider.GetRequiredService<RoutyaNotificationHandler2>();
        
        await Task.WhenAll(
            handler1.HandleAsync(notification, ct),
            handler2.HandleAsync(notification, ct)
        );
    }
}
```

**Registration:**
```csharp
services.AddScoped<IRoutya, GeneratedRoutya>();
```

**User Code:**
```csharp
var result = await dispatcher.SendAsync(request); // Type inference picks optimized overload
```

**Estimated Performance:** ~180-200 ns (beat MediatR!)

---

## Implementation Strategy

### Phase 1: Extend Generator to Emit Dispatcher Methods

**File:** `Routya.SourceGenerators/Emitters/DispatcherEmitter.cs`

```csharp
public static class DispatcherEmitter
{
    public static string EmitGeneratedDispatcher(
        List<HandlerDescriptor> requestHandlers,
        List<BehaviorDescriptor> notificationHandlers)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("namespace Routya.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    public sealed class GeneratedRoutya : IRoutya");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine("        public GeneratedRoutya(IServiceProvider serviceProvider)");
        sb.AppendLine("        {");
        sb.AppendLine("            _serviceProvider = serviceProvider;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate SendAsync overloads
        foreach (var handler in requestHandlers)
        {
            EmitRequestDispatchMethod(sb, handler);
        }
        
        // Generate PublishAsync overloads
        var notificationGroups = notificationHandlers
            .GroupBy(h => h.NotificationType);
        
        foreach (var group in notificationGroups)
        {
            EmitNotificationDispatchMethod(sb, group.Key, group.ToList());
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private static void EmitRequestDispatchMethod(StringBuilder sb, HandlerDescriptor handler)
    {
        sb.AppendLine($"        public async Task<{handler.ResponseType}> SendAsync(");
        sb.AppendLine($"            {handler.RequestType} request,");
        sb.AppendLine($"            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var scope = _serviceProvider.CreateScope();");
        sb.AppendLine($"            var handler = scope.ServiceProvider.GetRequiredService<{handler.ConcreteType}>();");
        sb.AppendLine($"            return await handler.HandleAsync(request, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
    
    private static void EmitNotificationDispatchMethod(
        StringBuilder sb, 
        string notificationType, 
        List<BehaviorDescriptor> handlers)
    {
        sb.AppendLine($"        public async Task PublishAsync(");
        sb.AppendLine($"            {notificationType} notification,");
        sb.AppendLine($"            CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var scope = _serviceProvider.CreateScope();");
        
        for (int i = 0; i < handlers.Count; i++)
        {
            sb.AppendLine($"            var handler{i} = scope.ServiceProvider.GetRequiredService<{handlers[i].ConcreteType}>();");
        }
        
        sb.AppendLine();
        sb.AppendLine("            await Task.WhenAll(");
        for (int i = 0; i < handlers.Count; i++)
        {
            var comma = i < handlers.Count - 1 ? "," : "";
            sb.AppendLine($"                handler{i}.HandleAsync(notification, cancellationToken){comma}");
        }
        sb.AppendLine("            );");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
```

---

### Phase 2: Update Registration

**Generated Registration:**
```csharp
public static IServiceCollection AddGeneratedRoutya(this IServiceCollection services)
{
    // Handler registrations (as before)
    services.AddScoped<TestRequestHandler>();
    // ...
    
    // Register optimized dispatcher
    services.AddScoped<IRoutya, GeneratedRoutya>();
    
    return services;
}
```

---

## Expected Performance Improvements

### Request/Response
- **Current v3.0:** 349.8 ns
- **Target v3.1:** 200-220 ns (**37% faster**)
- **vs MediatR (195 ns):** Competitive or better

### Notifications
- **Current v3.0:** 220.0 ns (already excellent)
- **Target v3.1:** 210-220 ns (maintain or slight improvement)

---

## Risks & Mitigations

### Risk 1: Code Size Explosion
**Issue:** Generating methods for every handler increases assembly size

**Mitigation:**
- Only generate for handlers in current assembly
- Use partial classes to split large files
- Keep fallback generic method for dynamic types

### Risk 2: Breaking Changes
**Issue:** Changing dispatcher type breaks existing code

**Mitigation:**
- Keep `IRoutya` interface unchanged
- Generated dispatcher implements same interface
- Fallback to `DefaultRoutya` for unknown types

### Risk 3: DI Container Overhead
**Issue:** `GetRequiredService<T>()` still has overhead

**Mitigation:**
- Future: Cache resolved handlers (requires lifecycle management)
- v4.0: Investigate DI-free dispatch via static handlers

---

## Implementation Timeline

### Sprint 1 (Week 1)
- [ ] Create `DispatcherEmitter.cs`
- [ ] Update `HandlerRegistrationGenerator` to emit dispatcher class
- [ ] Add unit tests for generated code

### Sprint 2 (Week 2)
- [ ] Run benchmarks comparing v3.0 vs v3.1
- [ ] Optimize notification parallel dispatch
- [ ] Add regression tests

### Sprint 3 (Week 3)
- [ ] Documentation updates
- [ ] Blog post: "How Source Generators Made Routya 2x Faster"
- [ ] Release v3.1.0

---

## Success Criteria

✅ Request dispatch: <220 ns (within 15% of MediatR)  
✅ Notification dispatch: <220 ns (maintain current performance)  
✅ Zero breaking changes to public API  
✅ Pass all existing unit tests  
✅ Generated code compiles warning-free  

---

## Next Steps

1. **Prototype dispatcher emitter** (1-2 hours)
2. **Integrate with existing generator** (2-3 hours)
3. **Run benchmarks** (30 minutes)
4. **Iterate based on results**

Target completion: **End of January 2026**

---

**Status:** Ready for implementation  
**Priority:** HIGH - Addresses critical performance regression  
**Impact:** Makes Routya competitive with MediatR on performance
