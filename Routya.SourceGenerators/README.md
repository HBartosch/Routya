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
- ✅ **46% faster than MediatR** on notifications

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

## Performance

| Benchmark | Routya Source Gen | MediatR | vs MediatR |
|---|---:|---:|---:|
| Request/response (2 behaviors) | 335 ns | 337 ns | on par |
| Notifications (2 handlers) | **121 ns** | 223 ns | **46% faster** |

Full benchmark results are in [Routya.SourceGen.Benchmark](../Routya.SourceGen.Benchmark).

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

## Limitations

- **Single assembly** — handlers in referenced libraries are not discovered automatically.
- **Non-generic handler types** — the handler class itself cannot be a generic type (e.g., `class MyHandler<T>`), though request and response types can be.
- **Public, non-abstract only** — internal or abstract handler classes are ignored.

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
