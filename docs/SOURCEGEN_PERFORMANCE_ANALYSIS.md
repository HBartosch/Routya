# Routya Source Generator v3.0 - Performance Analysis

**Benchmark Date:** January 27, 2026  
**Comparison:** MediatR 12.4.1 vs Routya v2.0 (Runtime) vs Routya v3.0 (Source Gen)

## Executive Summary

✅ **Notifications: 21% faster than v2.0, competitive with MediatR**  
❌ **Requests: 20% slower than v2.0, 79% slower than MediatR**  

The source generator successfully optimized notification handling but revealed deeper performance issues in request dispatch that require core architecture improvements.

---

## Benchmark Results

| Method                          | Mean     | Error   | StdDev  | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------- |---------:|--------:|--------:|------:|--------:|----------:|------------:|
| MediatR_Request                 | 195.0 ns | 3.16 ns | 2.95 ns |  1.00 |    0.02 |         - |          NA |
| RoutyaV2_Request                | 290.8 ns | 2.23 ns | 1.87 ns |  1.49 |    0.02 |     776 B |          NA |
| **RoutyaV3_SourceGen_Request**  | **349.8 ns** | **3.11 ns** | **2.43 ns** |  **1.79** |    **0.03** |     **-** |      **NA** |
| MediatR_Notification            | 215.8 ns | 1.73 ns | 1.62 ns |  1.11 |    0.02 |     600 B |          NA |
| RoutyaV2_Notification           | 277.3 ns | 2.19 ns | 2.05 ns |  1.42 |    0.02 |     488 B |          NA |
| **RoutyaV3_SourceGen_Notification** | **220.0 ns** | **2.35 ns** | **2.20 ns** | **1.13** |    **0.02** |     **-** |      **NA** |

### Performance Breakdown

#### Request/Response
- **MediatR (Baseline):** 195.0 ns
- **Routya v2.0:** 290.8 ns (+49% vs MediatR)
- **Routya v3.0:** 349.8 ns (+79% vs MediatR, +20% vs v2.0) ⚠️

#### Notifications
- **MediatR:** 215.8 ns
- **Routya v2.0:** 277.3 ns (+28% vs MediatR)
- **Routya v3.0:** 220.0 ns (+2% vs MediatR, -21% vs v2.0) ✅

---

## Root Cause Analysis

### Why Source Gen Improved Notifications

1. **Compile-time handler discovery** eliminates runtime reflection
2. **Direct dictionary registration** reduces lookup overhead
3. **Notification handler resolution** benefits most from pre-computed metadata

### Why Source Gen Degraded Requests

The **dispatcher implementation remains identical** between v2.0 and v3.0:
- Both use `DefaultRoutya` with runtime dispatch logic
- Source generator only optimizes **registration**, not **execution**
- Request dispatch still uses:
  - `RequestHandlerInfo` dictionary lookups
  - Generic type resolution via `typeof()`
  - Expression compilation overhead (from v2.0)

**The V3 regression suggests the generated registration code adds overhead:**
- Larger dictionary initialization
- More complex metadata structures
- Additional indirection layers

---

## Performance Gaps vs MediatR

### Request Handling Gap: +154.8 ns (79% slower)

**MediatR Advantages:**
- Simplified generic constraints
- Optimized internal caching
- Minimal abstraction layers
- Type-specific compiled delegates

**Routya Overhead Sources:**
1. Dictionary lookups: `RequestHandlerInfo<TRequest, TResponse>`
2. Generic type resolution complexity
3. Scope validation checks
4. Handler instantiation through DI

### Notification Handling: Near Parity (+4.2 ns, 2% slower) ✅

Source generation brought notification performance within margin of error of MediatR.

---

## Action Plan: Phase 3 Optimization

### Immediate Priority: Fix Request Dispatch Regression

**Goal:** Match or exceed v2.0 performance (290.8 ns target)

#### Strategy 1: Optimize Generated Code
- [ ] Reduce `RequestHandlerInfo` metadata overhead
- [ ] Generate specialized dispatch methods per request type
- [ ] Eliminate redundant dictionary lookups
- [ ] Pre-compute generic type arguments

#### Strategy 2: Type-Specific Dispatcher Generation
```csharp
// Instead of: dispatcher.SendAsync<TRequest, TResponse>(request)
// Generate:
public async Task<string> SendAsync(TestRequest request, CancellationToken ct = default)
{
    using var scope = _serviceProvider.CreateScope();
    var handler = scope.ServiceProvider.GetRequiredService<TestRequestHandler>();
    return await handler.HandleAsync(request, ct);
}
```

**Estimated Impact:** -100 to -150 ns (down to ~200-250 ns range)

---

### Long-Term: Match MediatR Performance

**Goal:** <200 ns for request handling

#### Strategy: Remove Abstraction Overhead
1. **Generate dedicated dispatcher per request**
   - Zero dictionary lookups
   - Direct handler resolution
   - Inline scope creation

2. **Compile-time generic resolution**
   - No `typeof()` calls
   - Static handler type binding
   - Optimized async state machines

3. **Minimize allocations**
   - Reuse scope instances where safe
   - ValueTask optimizations
   - Struct-based internal types

**Estimated Impact:** -50 to -100 ns (match MediatR 195 ns baseline)

---

## Recommendations

### Short-Term (v3.0.1 Patch)
1. **Investigate V3 regression**
   - Profile generated `AddGeneratedRoutya()` method
   - Identify added overhead sources
   - Optimize metadata structures

2. **Document notification wins**
   - Highlight 21% improvement
   - Promote for notification-heavy workloads

3. **Add benchmark suite to CI**
   - Track performance across releases
   - Prevent regressions

### Medium-Term (v3.1)
1. **Implement type-specific dispatchers**
   - Generate `Send_TestRequest(TestRequest)` methods
   - Direct handler instantiation
   - Remove generic overhead

2. **Optimize handler resolution**
   - Cached delegate invocation
   - Reduce DI container overhead

### Long-Term (v4.0)
1. **Zero-allocation mode**
   - ValueTask patterns
   - ArrayPool usage
   - Struct-based internal types

2. **AOT compilation support**
   - NativeAOT compatibility
   - Trimming-safe metadata

3. **Performance parity with MediatR**
   - <200 ns for all scenarios
   - Zero-allocation for sync paths

---

## Competitive Positioning

### Current State
- ❌ **Request/Response:** 79% slower than MediatR
- ✅ **Notifications:** Competitive with MediatR (2% slower)

### v3.1 Target
- ⚠️ **Request/Response:** <30% slower than MediatR (~250 ns)
- ✅ **Notifications:** Maintain parity (~220 ns)

### v4.0 Vision
- ✅ **Request/Response:** Match or beat MediatR (<195 ns)
- ✅ **Notifications:** Beat MediatR (<200 ns)
- ✅ **Zero allocations** for hot paths
- ✅ **NativeAOT ready**

---

## Conclusion

The source generator v3.0 **successfully validated the compile-time approach** with notification performance improvements. However, it exposed fundamental architectural overhead in request dispatch that requires deeper optimization.

**Next Steps:**
1. Fix v3.0 request regression (target: match v2.0 at 290 ns)
2. Implement type-specific dispatch generation
3. Work toward MediatR performance parity

The path forward is clear: **generate specialized, zero-overhead dispatch code** for each handler type, eliminating runtime generic resolution and dictionary lookups.

---

**Generated by:** Routya Source Generator Performance Analysis  
**Version:** 3.0.0-preview.1  
**Benchmark Runtime:** .NET 10.0.0, X64 RyuJIT AVX2
