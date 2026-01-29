# Routya v3.0 Release Notes

## 🎉 Introducing Source Generators!

Routya v3.0 brings **compile-time code generation** for zero-overhead request/response dispatching. The new `Routya.SourceGenerators` package analyzes your handlers at build-time and generates optimized, type-specific dispatch code.

---

## 🚀 What's New

### ✨ Source Generator Package

**New NuGet Package: `Routya.SourceGenerators`**

```bash
dotnet add package Routya.SourceGenerators --version 3.0.0
```

**Key Benefits:**
- ⚡ **46% faster than MediatR** on notifications (120ns vs 222ns)
- ⚡ **64% faster than Routya v2** on notifications  
- 🎯 **Zero reflection** - All handlers discovered at compile-time
- 🎯 **Zero dictionary lookups** - Direct method calls
- 🎯 **Type-specific API** - Clean IntelliSense without generic type arguments
- 🎯 **Compile-time safety** - Missing handlers cause build errors

### 📋 New Interface: `IGeneratedRoutya`

Instead of the generic `IRoutya` interface, the source generator creates a type-specific `IGeneratedRoutya`:

```csharp
// Before (Runtime - Routya v2)
var user = await routya.SendAsync<GetUserRequest, User>(request);

// After (Source Generator - Routya v3)
var user = await routya.SendAsync(request);  // Type inference!
```

### 🔥 Features Supported

✅ **Pipeline Behaviors** - Full `IPipelineBehavior<TRequest, TResponse>` support  
✅ **Streaming** - `IAsyncEnumerable<T>` for large datasets  
✅ **Generic Types** - `List<T>`, `Dictionary<K,V>`, etc.  
✅ **Database Integration** - EF Core, Dapper, ADO.NET  
✅ **Notifications** - Parallel execution with multiple handlers  
✅ **Cancellation** - Full `CancellationToken` support  

---

## 📊 Performance Benchmarks

**With 2 Pipeline Behaviors:**

| Scenario              | MediatR   | Routya v3 SourceGen | Improvement |
|-----------------------|-----------|---------------------|-------------|
| **Request Handling**  | 337.1 ns  | **335.3 ns**        | Same        |
| **Notifications**     | 222.5 ns  | **120.7 ns**        | **46% faster** |

**Memory:**
- Routya v3: Minimal allocations (200-1064 B depending on scenario)
- Routya v2: Zero allocations (but slower due to dictionary lookups)
- MediatR: Similar allocations to Routya v3

---

## 🎯 Getting Started

### 1. Install the Package

```bash
dotnet add package Routya.SourceGenerators
```

### 2. Define Handlers

```csharp
using Routya.Core.Abstractions;

public class GetUserRequest : IRequest<User>
{
    public int UserId { get; set; }
}

public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, User>
{
    public async Task<User> HandleAsync(GetUserRequest request, CancellationToken ct)
    {
        return new User { Id = request.UserId, Name = $"User_{request.UserId}" };
    }
}
```

### 3. Register and Use

```csharp
using Routya.Generated;

// DI Registration
services.AddGeneratedRoutya();

// Usage
var routya = provider.GetRequiredService<IGeneratedRoutya>();
var user = await routya.SendAsync(new GetUserRequest { UserId = 123 });
```

---

## 🔄 Migration Guide

### From Routya v2.x → v3.0 Source Generator

**Step 1: Update Package**
```bash
dotnet remove package Routya.Core
dotnet add package Routya.SourceGenerators
```

**Step 2: Update Registration**
```diff
- services.AddRoutya(cfg => 
- {
-     cfg.Scope = RoutyaDispatchScope.Scoped;
-     cfg.HandlerLifetime = ServiceLifetime.Scoped;
- }, Assembly.GetExecutingAssembly());
+ services.AddGeneratedRoutya();
```

**Step 3: Update Interface**
```diff
- var routya = provider.GetRequiredService<IRoutya>();
+ var routya = provider.GetRequiredService<IGeneratedRoutya>();
```

**Step 4: Remove Type Arguments**
```diff
- var user = await routya.SendAsync<GetUserRequest, User>(request);
+ var user = await routya.SendAsync(request);

- await routya.PublishAsync<UserCreatedNotification>(notification);
+ await routya.PublishAsync(notification);
```

**That's it!** The source generator automatically discovers all handlers in your assembly.

---

## 🆕 Advanced Features

### Streaming Large Datasets

```csharp
public class GetLargeDatasetRequest : IRequest<IAsyncEnumerable<DataChunk>>
{
    public int TotalRecords { get; set; }
}

public class GetLargeDatasetHandler : IAsyncRequestHandler<GetLargeDatasetRequest, IAsyncEnumerable<DataChunk>>
{
    public async Task<IAsyncEnumerable<DataChunk>> HandleAsync(
        GetLargeDatasetRequest request, 
        CancellationToken ct)
    {
        return StreamDataAsync(request, ct);
    }

    private async IAsyncEnumerable<DataChunk> StreamDataAsync(
        GetLargeDatasetRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < request.TotalRecords; i += 100)
        {
            yield return new DataChunk { /* ... */ };
        }
    }
}

// Usage
var stream = await routya.SendAsync(new GetLargeDatasetRequest { TotalRecords = 1000 });
await foreach (var chunk in stream)
{
    // Process each chunk as it arrives
}
```

### Pipeline Behaviors with Source Generator

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Before: {typeof(TRequest).Name}");
        var response = await next(ct);
        Console.WriteLine($"After: {typeof(TRequest).Name}");
        return response;
    }
}

// Register
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddGeneratedRoutya();
```

---

## ⚠️ Breaking Changes

### For Users Upgrading from v2.x

1. **Different Interface**: `IGeneratedRoutya` instead of `IRoutya`
2. **No Type Arguments**: Method signatures are type-specific
3. **Single Assembly**: Handlers must be in the same assembly as registration
4. **Compile-Time Discovery**: No runtime assembly scanning

### Compatibility

- **Routya.Core v2.x** still available for runtime dispatch
- **Routya.SourceGenerators v3.x** for compile-time generation
- Both can coexist in different projects

---

## 📦 Package Versions

| Package                    | Version | Description                          |
|---------------------------|---------|--------------------------------------|
| `Routya.Core`             | 2.0.0   | Runtime dispatcher (v2.x)           |
| `Routya.SourceGenerators` | 3.0.0   | Compile-time source generator (NEW) |

---

## 🎓 Examples

**Check out the example projects:**

- [Routya.SourceGen.Demo](./Routya.SourceGen.Demo) - Basic usage with requests, notifications, and pipelines
- [Routya.SourceGen.DatabaseDemo](./Routya.SourceGen.DatabaseDemo) - SQLite integration with CRUD operations  
- [Routya.SourceGen.Benchmark](./Routya.SourceGen.Benchmark) - Performance comparisons

---

## 🐛 Known Issues

None at this time. Please report issues on [GitHub](https://github.com/hbartosch/routya/issues).

---

## 🔮 Future Roadmap

- Multi-assembly support
- Compile-time diagnostics and warnings
- Batch dispatch capabilities
- Additional optimizations

---

## 🙏 Credits

Thank you to all contributors and users who provided feedback during the beta phase!

---

## 📄 License

MIT License - see [LICENSE](./LICENSE) for details.

---

## 🔗 Links

- **GitHub**: [hbartosch/routya](https://github.com/hbartosch/routya)
- **NuGet (Core)**: [Routya.Core](https://www.nuget.org/packages/Routya.Core)
- **NuGet (SourceGen)**: [Routya.SourceGenerators](https://www.nuget.org/packages/Routya.SourceGenerators)
- **Issues**: [Report bugs](https://github.com/hbartosch/routya/issues)
