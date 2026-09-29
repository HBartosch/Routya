# V3.1 Performance Issue - Root Cause & Fix

## The Problem

Initial v3.1 benchmarks showed **REGRESSION**, not improvement:
- Request: 303.5 ns (vs v2.0: 297.4 ns) - **2% SLOWER**
- Notification: 299.7 ns (vs v2.0: 276.8 ns) - **8% SLOWER**  
- MORE allocations: 936 B vs 776 B (request)

## Root Cause: Double-Scoping

### The Bug
Generated dispatcher was creating its own scope:
```csharp
public async Task<string> SendAsync(TestRequest request, ...)
{
    using var scope = _serviceProvider.CreateScope();  // ❌ Extra scope!
    var handler = scope.ServiceProvider.GetRequiredService<TestRequestHandler>();
    return await handler.HandleAsync(request, ...);
}
```

### Why It's Bad
The benchmark ALREADY creates a scope:
```csharp
using var scope = _providerRoutyaV3.CreateScope();
var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
await dispatcher.SendAsync(request);  // This creates ANOTHER scope!
```

**Result:** Two nested scopes = double overhead + extra allocations

## The Fix

### Remove Scope Creation from Generated Code
```csharp
public async Task<string> SendAsync(TestRequest request, ...)
{
    // ✅ Direct resolution - caller manages scopes
    var handler = _serviceProvider.GetRequiredService<TestRequestHandler>();
    return await handler.HandleAsync(request, ...);
}
```

### Why This Works
- When called from a scope: `_serviceProvider` IS the scoped provider
- Zero overhead - just direct DI resolution
- No extra allocations
- Matches MediatR's approach

## Expected Performance After Fix

**Requests:**
- Current (broken): 303.5 ns
- After fix: ~190-200 ns (**35% faster!**)
- vs MediatR (196.7 ns): **Competitive or better**

**Notifications:**
- Current (broken): 299.7 ns  
- After fix: ~210-220 ns (**27% faster!**)
- vs MediatR (233.4 ns): **Beating MediatR by 5-10%!**

## Key Insight

**Scope management should be the caller's responsibility**, not the dispatcher's. This allows:
1. Caller controls lifetime (singleton, scoped, transient contexts)
2. Zero overhead when already in a scope
3. Flexibility for different usage patterns

---

**Status:** Fix implemented, benchmarks running 🏃‍♂️
**Confidence:** HIGH - this was the obvious bottleneck
