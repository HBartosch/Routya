# Routya Source Generator

**Compile-time code generation for zero-overhead request/response dispatching**

[![NuGet](https://img.shields.io/nuget/v/Routya.SourceGenerators)](https://www.nuget.org/packages/Routya.SourceGenerators)
[![NuGet](https://img.shields.io/nuget/dt/Routya.SourceGenerators)](https://www.nuget.org/packages/Routya.SourceGenerators)

---

## Why Source Generation?

**Runtime dispatch (`Routya.Core` alone):**
- Discovers handlers at startup via reflection
- Resolves handlers via dictionary lookups on every call
- Generic type arguments required at every call site — `SendAsync<MyRequest, MyResponse>(req)`

**Source generator (this package):**
- ✅ **Zero reflection** — handlers discovered at compile-time
- ✅ **Zero dictionary lookups** — direct, type-specific method calls
- ✅ **Clean API** — `SendAsync(req)` with no type arguments, full IntelliSense per handler
- ✅ **Compile-time safety** — missing handlers are build errors, not runtime exceptions
- ✅ **55% less memory than MediatR** on notifications (272 B against 600 B)

---

## Installation

### Recommended — `Routya` meta-package (gets both packages in one step)

```bash
dotnet add package Routya
```

### Standalone — install both packages separately

`Routya.SourceGenerators` is a source-generator-only package with no runtime output. It requires `Routya.Core` for the abstractions it generates code against.

```bash
dotnet add package Routya.Core
dotnet add package Routya.SourceGenerators
```

---

## Quick Start

Define your handlers exactly as you would for runtime dispatch — the generator discovers them automatically. See [Routya.Core](https://www.nuget.org/packages/Routya.Core) for the full handler and notification documentation.

```csharp
using Routya.Core.Abstractions;

public record GetUserRequest(int UserId) : IRequest<User>;
public record User(int Id, string Name);

public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, User>
{
    public Task<User> HandleAsync(GetUserRequest request, CancellationToken ct)
        => Task.FromResult(new User(request.UserId, $"User_{request.UserId}"));
}
```

Register with one call — no assembly scanning, no manual handler wiring:

```csharp
using Routya.Generated;

services.AddGeneratedRoutya();
```

Dispatch with no generic type arguments:

```csharp
var routya = provider.GetRequiredService<IGeneratedRoutya>();

var user = await routya.SendAsync(new GetUserRequest(123));
await routya.PublishAsync(new UserCreatedNotification { UserId = 456 });
```

---

## Handler Discovery Rules

The generator scans the assembly at compile-time for classes that are:

- `public` and non-`abstract`
- Implement `IRequestHandler<TRequest, TResponse>`, `IAsyncRequestHandler<TRequest, TResponse>`, or `INotificationHandler<TNotification>`

No attributes or markers needed.

---

## Inspecting the Generated Code

By default, generated source files exist only in memory during the build. To write them to disk so you can read and step through them, add these two properties to your `.csproj`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

`$(BaseIntermediateOutputPath)` resolves to your `obj/` folder, so the files land at `obj/Generated/` by default. You can set any path you like — for example `Generated/` in your project root if you want them always visible in the IDE without digging into `obj/`.

After building, two files appear under the configured path:

| File | Contents |
|---|---|
| `RoutyaGenerated.Registration.g.cs` | `IGeneratedRoutya` interface + `AddGeneratedRoutya()` extension |
| `RoutyaGenerated.Dispatcher.g.cs` | `GeneratedRoutya` class with a typed method per handler |

> **Note:** If you set `CompilerGeneratedFilesOutputPath` to a folder inside your project (not `obj/`), add it to `.gitignore` — the files are always regenerated on build and should not be committed.

You can also view the generated output without touching `.csproj` by opening the **Analyzers** node in the Solution Explorer tree in Visual Studio or Rider, under `Routya.SourceGenerators`.

---

## What Gets Generated

For each handler the generator emits a concrete typed method on `IGeneratedRoutya` and `GeneratedRoutya`. For example, `GetUserHandler` produces:

```csharp
// In IGeneratedRoutya
Task<User> SendAsync(GetUserRequest request, CancellationToken cancellationToken = default);

// In GeneratedRoutya
public async Task<User> SendAsync(GetUserRequest request, CancellationToken cancellationToken = default)
{
    var behaviors = _serviceProvider.GetServices<IPipelineBehavior<GetUserRequest, User>>();
    var handler = _serviceProvider.GetRequiredService<GetUserHandler>();

    if (behaviors.Any())
    {
        RequestHandlerDelegate<User> next = ct => handler.HandleAsync(request, ct);
        foreach (var behavior in behaviors.Reverse())
        {
            var current = next;
            next = ct => behavior.Handle(request, current, ct);
        }
        return await next(cancellationToken).ConfigureAwait(false);
    }

    return await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
}
```

Pipeline behaviors (`IPipelineBehavior<,>`) and notification handlers work identically to runtime dispatch — register them the same way before calling `AddGeneratedRoutya()`.

---

## Streaming

A handler implementing `IStreamRequestHandler<TRequest, TResponse>` gets a generated `CreateStream`
member, alongside `SendAsync` for async handlers and `Send` for synchronous ones:

```csharp
public record ExportProducts(int CategoryId) : IStreamRequest<Product>;

public class ExportProductsHandler(AppDbContext db) : IStreamRequestHandler<ExportProducts, Product>
{
    public IAsyncEnumerable<Product> Handle(ExportProducts request, CancellationToken ct)
        => db.Products.Where(p => p.CategoryId == request.CategoryId).AsAsyncEnumerable();
}

// Generated: no generic arguments needed at the call site
await foreach (var product in routya.CreateStream(new ExportProducts(7), ct))
{
    // ...
}
```

Items are produced lazily and nothing is buffered. Any registered
`IStreamPipelineBehavior<TRequest, TResponse>` wraps the whole enumeration, so it observes every item
and any exception thrown part way through.

---

## Performance

| Benchmark | Routya Source Gen | MediatR | vs MediatR |
|---|---:|---:|---:|
| Request/response, allocated | **976 B** | 1008 B | 3% less |
| Notifications, 2 handlers, allocated | **272 B** | 600 B | **55% less** |

Allocated bytes are deterministic, so these figures hold on any machine. Timings are deliberately
not published here: they depend on hardware, and the MediatR baseline in these benchmarks has been
observed drifting by a third between runs on identical code. Run
[Routya.SourceGen.Benchmark](../Routya.SourceGen.Benchmark) on a quiet machine if you need timings
for your own hardware.

Allocation figures are asserted on every build by the budget tests in `Routya.SourceGen.Test`, so
they cannot go stale unnoticed.

---

## Switching from Runtime Dispatch

Both approaches can coexist — `IRoutya` and `IGeneratedRoutya` are independent interfaces with no breaking changes between them.

**Runtime dispatch:**
```csharp
services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());

var routya = provider.GetRequiredService<IRoutya>();
var user = await routya.SendAsync<GetUserRequest, User>(request);
```

**Source generator:**
```csharp
services.AddGeneratedRoutya();

var routya = provider.GetRequiredService<IGeneratedRoutya>();
var user = await routya.SendAsync(request); // no type arguments
```

---

## Which project generates the dispatcher

**The generator emits into the project that calls `AddGeneratedRoutya`, and nowhere else.**

`Routya.Generated` is a fixed, public namespace, so two assemblies in one reference chain cannot
both define `IGeneratedRoutya` without colliding as `CS0436`. Handlers in referenced assemblies are
discovered automatically, so the composition root's dispatcher covers the whole solution and the
projects holding the handlers generate nothing.

| Project | Calls `AddGeneratedRoutya` | Generates |
|---|---|---|
| `Shop.Application` (handlers live here) | no | nothing |
| `Shop.Api` (composition root) | yes | the dispatcher, covering handlers in both projects |
| `Shop.Tests` (references `Shop.Api`) | no | nothing |

`ROUTYA008` (Info) reports a project that has handlers but composes no container, which is the
normal shape for a library project and needs no action. `ROUTYA009` (Warning) reports two
assemblies in one chain both generating. Override the decision either way with:

```xml
<PropertyGroup>
  <RoutyaGenerateDispatcher>false</RoutyaGenerateDispatcher>
</PropertyGroup>
```

---

## Handler lifetimes

`AddGeneratedRoutya` registers handlers as **Transient** by default. Pass a lifetime to change it
for all of them, and override an individual handler with `[RoutyaHandler]`, which wins over the
parameter:

```csharp
services.AddGeneratedRoutya(ServiceLifetime.Scoped);

[RoutyaHandler(ServiceLifetime.Singleton)]
public class GetExchangeRatesHandler : IAsyncRequestHandler<GetExchangeRates, Rates>
{
    // holds a cache, so one instance for the whole application
}
```

`Transient` is the default because `IGeneratedRoutya` uses whatever provider it was given rather
than creating a scope per dispatch, so a `Scoped` handler only resolves from inside a scope. In
ASP.NET Core that is always true of a request, so `ServiceLifetime.Scoped` is safe there. Dispatching
from a singleton service, an `IHostedService` that makes no scope, or a console application requires
either `Transient` or a scope you create yourself.

---

## Limitations

- **Non-generic handler types** — the handler class itself cannot be a generic type (e.g., `class MyHandler<T>`), though request and response types can be.
- **Non-abstract only** — abstract handler classes are ignored.
- **Typed members need publicly visible types** — an `internal` handler is registered and dispatched normally, but a typed member on `IGeneratedRoutya` cannot expose an `internal` request or response type, because the interface is public. Such a handler reports `ROUTYA005` and stays reachable through `IRoutya`.

---

## Example Projects

| Project | Description |
|---|---|
| [Routya.SourceGen.Demo](../Routya.SourceGen.Demo) | Console app — basic request/response and notification dispatch via the source generator |
| [Routya.WebApi.SourceGen.Demo](../Routya.WebApi.SourceGen.Demo) | ASP.NET Core minimal API — CRUD endpoints, open-generic pipeline behavior, notification fan-out, and `IAsyncEnumerable<T>` streaming |

---

## Links

- **NuGet (meta-package)**: [Routya](https://www.nuget.org/packages/Routya)
- **NuGet (standalone)**: [Routya.SourceGenerators](https://www.nuget.org/packages/Routya.SourceGenerators)
- **Core docs**: [Routya.Core](https://www.nuget.org/packages/Routya.Core)
- **GitHub**: [github.com/HBartosch/Routya](https://github.com/HBartosch/Routya)
- **Issues**: [github.com/HBartosch/Routya/issues](https://github.com/HBartosch/Routya/issues)
- **License**: MIT — see [LICENSE](../LICENSE)
