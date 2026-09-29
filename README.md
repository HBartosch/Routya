# Routya
![CI](https://img.shields.io/github/actions/workflow/status/hbartosch/routya/dotnet.yml?label=CI&style=flat-square)
![CI](https://img.shields.io/github/actions/workflow/status/hbartosch/routya/build-and-test.yml?label=Tests&style=flat-square)
[![NuGet](https://img.shields.io/nuget/v/Routya.Core)](https://www.nuget.org/packages/Routya.Core)
[![NuGet](https://img.shields.io/nuget/dt/Routya.Core)](https://www.nuget.org/packages/Routya.Core)
![.NET Standard](https://img.shields.io/badge/netstandard-2.0%20%7C%202.1-blue?logo=dotnet&logoColor=white)
![.NET 8](https://img.shields.io/badge/net-8.0%20%7C%209.0%20%7C%2010-blue?logo=dotnet&logoColor=white)

**Routya** is a fast, lightweight message dispatching library built for .NET applications that use the CQRS pattern.  
It provides a flexible way to route requests/responses and notifications to their respective handlers with minimal overhead and high performance.

---

## ⚡ **Source Generator: compile time dispatch**

Get **compile-time code generation** with no reflection on the dispatch path:

```bash
dotnet add package Routya.SourceGenerators --version 4.0.0
```

```csharp
using Routya.Generated;

builder.Services.AddGeneratedRoutya(); // Auto-registers all handlers!

public class MyController : ControllerBase
{
    private readonly IGeneratedRoutya _routya;
    
    public MyController(IGeneratedRoutya routya) => _routya = routya;
    
    public async Task<User> GetUser(int id)
    {
        return await _routya.SendAsync(new GetUserRequest { UserId = id });
    }
}
```

**Why use it:**
- 📉 **Less memory than MediatR** - 272 B per notification publish against 600 B, and 976 B per request against 1008 B. Allocations are deterministic, so these hold on any machine
- 🔥 **Zero reflection** - all dispatch code generated at compile-time
- 📦 **Zero dictionary lookups** - direct method calls
- 🎯 **Full IntelliSense** - type-specific interface with your exact methods
- 🔬 **Trimming and Native AOT ready** - verified with a running native binary

📖 **[Getting Started Guide →](./docs/GETTING_STARTED_V3.md)** | 📦 **[Changelog →](./CHANGELOG.md)** | 📚 **[Source Generator Docs →](./Routya.SourceGenerators/README.md)** | 🗂️ **[All docs →](./docs/)**

---

## ✨ Features

- ✅ Clean interface-based abstraction for Requests/Responses and Notifications
- 🚀 **Low allocation dispatching** - Less memory per dispatch than MediatR, with more lifetime flexibility
- **⚡ Source generation** - Compile-time code generation for maximum speed
- **🌊 Streaming** - `IStreamRequest<T>` with lazy, unbuffered `IAsyncEnumerable<T>` and behaviours that wrap the whole enumeration. See [Streaming](#-streaming)
- **🔬 Trimming and Native AOT** - No IL warnings, verified with a running Native AOT binary. See [Trimming and Native AOT](#-trimming-and-native-aot)
- ⚙️ **Configurable handler lifetimes** - Choose Singleton, Scoped, or Transient per handler
- 🧩 Pipeline behavior support for cross-cutting concerns
- 🔄 Supports both **sequential** and **parallel** notification dispatching
- 🎯 **Multi-framework support** - netstandard2.0, netstandard2.1, .NET 8, .NET 9, .NET 10
- 💾 **Memory efficient** - Zero memory leaks with proper scope management
- ♻️ Simple to extend and integrate with your existing architecture

---

## 📦 NuGet Packages

### Source Generator (recommended for new projects)
```bash
dotnet add package Routya.SourceGenerators --version 4.0.0
```
Includes `Routya.Core` automatically. Compile time dispatch, no reflection, and the only option that
is verified under trimming and Native AOT.

### Runtime Dispatcher
```bash
dotnet add package Routya.Core --version 4.0.0
```
Use for existing projects or when runtime flexibility is needed.

### Everything in one package
```bash
dotnet add package Routya --version 4.0.0
```
Pulls in both `Routya.Core` and `Routya.SourceGenerators`.

### ⚠️ Breaking Changes in v4.0.0

Both are narrow. If you resolve `IRoutya` from the container, which is the normal case, **no source
change is required**.

**1. `IRoutya` gained `CreateStream`**

Only affects code that *implements* the interface, typically a hand written test double. Add the
member, or switch to a mocking framework, which generates it for you.

```csharp
IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(
    TRequest request,
    CancellationToken cancellationToken = default)
        where TRequest : IStreamRequest<TResponse>;
```

**2. `DefaultRoutya`'s constructor gained a parameter**

Only affects code calling `new DefaultRoutya(...)` directly. `AddRoutya` registers the new
`IRoutyaStreamDispatcher` for you.

**Behaviour changes worth knowing about**, all fixes rather than redesigns:

- `Scoped` pipeline behaviours are now built once per dispatch rather than once per process. They
  were previously reused after their scope had been disposed. Costs about 272 B per dispatch for two
  behaviours; register them `Singleton` where they are stateless
- `PublishParallelAsync` now gives each handler its own scope, so parallel handlers can no longer
  share scoped state with one another. Sequential publishing still shares one
- `internal` handlers previously skipped by the source generator are now discovered and registered.
  If you registered one manually as a workaround, remove that registration

Full detail, including upgrade notes, is in the [changelog](./CHANGELOG.md).

### ⚠️ Breaking Changes in v2.0.0

**1. Pipeline Behavior Delegate Signature Change**
The `RequestHandlerDelegate<TResponse>` now requires a `CancellationToken` parameter:

```csharp
// ❌ Old (v1.x)
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

// ✅ New (v2.0)
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);
```

**Migration Guide:**
Update your pipeline behaviors to pass the `CancellationToken` to the `next()` delegate:

```csharp
// ❌ Old code (v1.x)
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Your logic before
    var result = await next(); // ❌ No parameter
    // Your logic after
    return result;
}

// ✅ New code (v2.0)
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Your logic before
    var result = await next(cancellationToken); // ✅ Pass cancellationToken
    // Your logic after
    return result;
}
```

**2. Performance Improvements**
- Registry-based dispatch for handlers registered through the `AddRoutya*Handler` methods or assembly scanning

---

## 🚀 Quick Start

# Dependency injection
On startup you can define if **Routya** should create a new instance of the service provider each time it is called or work on the root service provider. 

Note!!! By default scope is enabled

# Scoped
Creates a new DI scope for each dispatch
- Safely supports handlers registered as Scoped
- ✅ Use this if your handlers depend on:
  - EF Core DbContext
  - IHttpContextAccessor
  - IMemoryCache, etc.
```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
```

# Root
Fastest option 
- avoids creating a service scope per dispatch
- Resolves handlers directly from the root IServiceProvider
- ✅ Ideal for stateless handlers that are registered as Transient or Singleton
- ⚠️ Will fail if your handler is registered as Scoped (e.g., it uses DbContext or IHttpContextAccessor)
```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Root, Assembly.GetExecutingAssembly());
```

You can add an auto registration of IRequestHandler, IAsyncRequestHandler and INotificationHandler by adding the executing assembly. This however registers all your request handlers with the default lifetime (Scoped).

Note!!! By default you would have to manually register your Requests/Notifications and Handlers

```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
```

# Handler Lifetimes (New in v1.0.5)

Configure handler lifetimes for optimal performance based on your use case:

### Option 1: Assembly Scanning with Uniform Lifetime
```C#
// Register all handlers as Singleton (fastest, stateless handlers only)
builder.Services.AddRoutya(cfg => cfg.HandlerLifetime = ServiceLifetime.Singleton, Assembly.GetExecutingAssembly());

// Register all handlers as Scoped (default, works with DbContext)
builder.Services.AddRoutya(cfg => cfg.HandlerLifetime = ServiceLifetime.Scoped, Assembly.GetExecutingAssembly());

// Register all handlers as Transient (new instance every time)
builder.Services.AddRoutya(cfg => cfg.HandlerLifetime = ServiceLifetime.Transient, Assembly.GetExecutingAssembly());
```

### Option 2: Optimized Manual Registration (Recommended for Performance)
Use Routya's specialized registration methods for optimal performance:

```C#
// Register Routya core services
builder.Services.AddRoutya();

// Use AddRoutyaAsyncRequestHandler for request handlers
builder.Services.AddRoutyaAsyncRequestHandler<CreateProductRequest, Product, CreateProductHandler>(ServiceLifetime.Singleton);
builder.Services.AddRoutyaRequestHandler<GetProductRequest, Product?, GetProductHandler>(ServiceLifetime.Scoped);

// Use AddRoutyaNotificationHandler for notifications
builder.Services.AddRoutyaNotificationHandler<UserRegisteredNotification, SendEmailHandler>(ServiceLifetime.Singleton);
builder.Services.AddRoutyaNotificationHandler<UserRegisteredNotification, LogAuditHandler>(ServiceLifetime.Scoped);
```

**Why use these methods?**
- ✅ **Automatic registry population** - Handlers added to high-performance registry
- ✅ **Lower allocation** for notifications (160 B vs 440 B with Singleton)
- ✅ **Type-safe** - Compile-time verification of handler signatures
- ✅ **Flexible lifetimes** - Choose Singleton/Scoped/Transient per handler

### Option 3: Traditional DI Registration (Still Supported)
You can also use standard DI registration - these handlers are resolved through the container on each dispatch:

```C#
// Register Routya core services (no assembly scanning)
builder.Services.AddRoutya();

// Traditional DI registration
builder.Services.AddSingleton<IAsyncRequestHandler<CreateProductRequest, Product>, CreateProductHandler>();
builder.Services.AddScoped<IAsyncRequestHandler<GetProductRequest, Product?>, GetProductHandler>();
builder.Services.AddTransient<IAsyncRequestHandler<GetAllProductsRequest, List<Product>>, GetAllProductsHandler>();

// Notification handlers
builder.Services.AddSingleton<INotificationHandler<UserRegisteredNotification>, SendEmailHandler>();
```

**Trade-off**: handlers registered this way are resolved through the container on every dispatch, so they do not get the registry's direct resolution. Use the `AddRoutya*Handler` methods above, or assembly scanning, if you want that.

**Performance Comparison** (memory figures are deterministic; timings are indicative):
- **Singleton**: 808 B per dispatch, 20% less than MediatR, best for stateless handlers
- **Transient**: 832 B per dispatch, 18% less than MediatR, maximum isolation
- **Scoped**: 1016 B per dispatch, matching MediatR, safe for DbContext and scoped dependencies

Routya lets YOU choose the right lifetime per handler:
- 🚀 **Singleton** for stateless handlers = fastest, least memory
- 🔄 **Scoped** for handlers with DbContext = safe with proper scope management  
- 🔒 **Transient** when you need maximum isolation = new instance every time

### Backward Compatibility
Routya maintains full backward compatibility with traditional DI registration:

```C#
// Traditional registration (still works!)
builder.Services.AddScoped<IAsyncRequestHandler<MyRequest, MyResponse>, MyHandler>();
builder.Services.AddScoped<INotificationHandler<MyNotification>, MyNotificationHandler>();
```

**How these are dispatched:**
Handlers registered this way are not described in Routya's registry, so Routya resolves them through
their interface on every dispatch, from the current dispatch scope. That is the same work any
container does, and it is required for correctness: registering `IAsyncRequestHandler<,>` against an
implementation does not register the implementation type, and a `Scoped` handler must be built once
per scope rather than reused.

This ensures:
- ✅ Smooth migration path from older versions
- ✅ Works with existing code without changes
- ✅ Correct behaviour for `Scoped` handlers and handlers holding scoped dependencies

**If you want the registry's faster path**, register through `AddRoutyaRequestHandler`,
`AddRoutyaAsyncRequestHandler`, `AddRoutyaNotificationHandler`, or assembly scanning. Those record
the concrete type and lifetime up front, which lets Routya resolve the handler directly.

# Requests

### 📊 Allocation per dispatch

Compared against MediatR 12.5.0 with simple request handlers.

| Configuration | Allocated | vs MediatR |
|---|---:|---:|
| MediatR `SendAsync` | 1016 B | baseline |
| **Routya `Send`, Singleton handler** | **808 B** | **20% less** |
| **Routya `Send`, Transient handler** | **832 B** | **18% less** |
| Routya `SendAsync`, Singleton handler | 928 B | 9% less |
| Routya `SendAsync`, Transient handler | 952 B | 6% less |
| Routya `Send`, Scoped handler | 1016 B | same |
| Routya `SendAsync`, Scoped handler | 1136 B | 12% more |

> **Why allocations and not timings.** Allocated bytes are deterministic: they do not depend on CPU,
> machine load or GC mode, so these figures hold on your hardware as well as ours, and they are
> asserted on every build by the allocation budget tests. Timings are not published here because
> they are not reproducible. On our own measurements the MediatR baseline, running unchanged code,
> drifted by a third between runs. If you need timings for your hardware, run
> [Routya.Benchmark](./Routya.Benchmark) on a quiet machine.
>
> Pipeline behaviors registered as `Scoped` are constructed once per dispatch scope, which costs a
> further 272 B per dispatch over `Singleton` behaviors. Register behaviors as `Singleton` where
> they are stateless.

**Key Highlights:**
- ✅ **20% less memory than MediatR** with a Singleton handler
- ✅ **Registry-based dispatch** for handlers registered through `AddRoutya*Handler` or assembly scanning
- ✅ **Zero memory leaks** with proper scope disposal
- ✅ **Fast-path optimization** when no behaviors configured
- 🎯 **Configurable handler lifetimes** (Singleton/Scoped/Transient)

Define a request
```C#
    public class HelloRequest(string name) : IRequest<string>
    {
        public string Name { get; } = name;
    }
```
Implement the Sync handler ...
```C#
    public class HelloSyncHandler : IRequestHandler<HelloRequest, string>
    {
        public string Handle(HelloRequest request)
        {
            return $"Hello, {request.Name}!";
        }
    }
```

or Implement the async handler
```C#
    public class HelloAsyncHandler : IAsyncRequestHandler<HelloRequest, string>
    {
        public async Task<string> HandleAsync(HelloRequest request, CancellationToken cancellationToken)
        {
            return await Task.FromResult($"[Async] Hello, {request.Name}!");
        }
    }
```

Inject the **IRoutya** interface and dispatch your requests in sync...
```C#
    public class Example : ControllerBase
    {
      private readonly IRoutya _dispatcher;

      public Example(IRoutya dispatcher)
      {
         _dispatcher = dispatcher;
      }
    }
```

```C#
    _dispatcher.Send<HelloRequest, string>(new HelloRequest("Sync World"));
```


or async
```C#
    await _dispatcher.SendAsync<HelloRequest, string>(new HelloRequest("Async World"));
```

# Pipeline Behaviors
You can add pipeline behaviors to execute around your requests. These behaviors need to be registered manually and execute in the order they are registered.
```C#
     services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
     services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));   
```

In the following example the LoggingBehavior will write to console before your request, wait for the request(in the example above first execute the ValidationBehavior and then in the ValidationBehavior it will execute the request) to execute and then write to the console afterward executing the request.
```C#
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            Routya.Core.Abstractions.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Logging] → {typeof(TRequest).Name}");
            var result = await next(cancellationToken); // Pass cancellationToken to next
            Console.WriteLine($"[Logging] ✓ {typeof(TRequest).Name}");
            return result;
        }
    }
```

# Notifications

### 📊 Allocation per publish

Compared against MediatR 12.5.0, two handlers per notification.

| Configuration | Allocated | vs MediatR |
|---|---:|---:|
| MediatR `Publish` | 440 B | baseline |
| **Routya sequential, Singleton handlers** | **160 B** | **64% less** |
| **Routya sequential, Transient handlers** | **208 B** | **53% less** |
| Routya parallel, Singleton handlers | 280 B | 36% less |
| Routya parallel, Transient handlers | 328 B | 25% less |
| Routya sequential, Scoped handlers | 392 B | 11% less |
| Routya parallel, Scoped handlers | 824 B | 87% more |

> Parallel publishing with `Scoped` handlers gives every handler its own dispatch scope, which is
> what makes it safe to use a `DbContext` in concurrent handlers. That is where the extra allocation
> goes. Sequential publishing shares one scope and stays at 392 B.
>
> As above, allocations rather than timings, because allocations are deterministic and asserted on
> every build.

**Key Highlights:**
- ✅ **64% less memory than MediatR** with Singleton handlers, 160 B against 440 B
- ✅ **Registry-based dispatch** - no `GetServices` call for handlers described in the registry
- ✅ **Parallel dispatching** with a scope per handler, safe for `DbContext`
- ✅ **Flexible lifetime management** for different use cases

Define your notification
```C#
  public class UserRegisteredNotification(string email) : INotification
  {
      public string Email { get; } = email;
  }
```

Define your handlers

```C#
  public class LogAnalyticsHandler : INotificationHandler<UserRegisteredNotification>
  {
      public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken = default)
      {
          await Task.Delay(100, cancellationToken);
          Console.WriteLine($"📊 Analytics event logged for {notification.Email}");
      }
  }
```

```C#
  public class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredNotification>
  {
      public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken = default)
      {
          await Task.Delay(200, cancellationToken);
          Console.WriteLine($"📧 Welcome email sent to {notification.Email}");
      }
  }
```

Inject the **IRoutya** interface and dispatch your notifications sequentially...
```C#
    public class Example : ControllerBase
    {
      private readonly IRoutya _dispatcher;

      public Example(IRoutya dispatcher)
      {
         _dispatcher = dispatcher
      }
    }
```

```C#
    await dispatcher.PublishAsync(new UserRegisteredNotification("john.doe@example.com"));
```

or in parallel
```C#
     await dispatcher.PublishParallelAsync(new UserRegisteredNotification("john.doe@example.com"));
```

### ⚠️ Scope behaviour differs between the two

Under `RoutyaDispatchScope.Scoped`, the two publish methods handle scopes differently, and the
difference matters if your handlers use `DbContext` or anything else registered as `Scoped`.

| | Scope | Consequence |
|---|---|---|
| `PublishAsync` (sequential) | **One shared scope** for all handlers | Handlers can share scoped state. A later handler can commit work an earlier one staged through a shared unit of work |
| `PublishParallelAsync` | **One scope per handler** | Handlers cannot share scoped state with each other. Each gets its own `DbContext`, so concurrent handlers cannot collide |

This is deliberate. Handlers running in parallel would otherwise be handed the same scoped instance
simultaneously, and `DbContext` throws `InvalidOperationException: A second operation was started on
this context before a previous operation completed` when that happens. The extra scopes cost roughly
300 bytes per additional handler.

If your parallel handlers need to share scoped state, they are not independent, and
`PublishAsync` is the correct choice.

---

## 🌊 Streaming

Use `IStreamRequest<TResponse>` when a handler produces a sequence rather than a single response.
Items are produced lazily and nothing is buffered, so a consumer can start processing before the
handler has finished.

```C#
public record ExportProducts(int CategoryId) : IStreamRequest<Product>;

public class ExportProductsHandler(AppDbContext db) : IStreamRequestHandler<ExportProducts, Product>
{
    public async IAsyncEnumerable<Product> Handle(
        ExportProducts request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var product in db.Products
            .Where(p => p.CategoryId == request.CategoryId)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return product;
        }
    }
}
```

Register it and dispatch:

```C#
services.AddRoutyaStreamRequestHandler<ExportProducts, Product, ExportProductsHandler>(ServiceLifetime.Scoped);

// Runtime dispatch
await foreach (var product in routya.CreateStream<ExportProducts, Product>(new ExportProducts(7), ct))
{
    // ...
}

// Source generated — no generic arguments needed
await foreach (var product in routya.CreateStream(new ExportProducts(7), ct))
{
    // ...
}
```

ASP.NET Core streams `IAsyncEnumerable<T>` natively, so a minimal API endpoint can return the
result directly with nothing buffered:

```C#
app.MapGet("/products/stream", (IGeneratedRoutya routya, CancellationToken ct)
    => routya.CreateStream(new ExportProducts(7), ct));
```

### Why not `IRequest<IAsyncEnumerable<T>>`?

That shape works, but it is not streaming in any useful sense. The handler returns a *task* that
hands back a sequence, so the pipeline finishes the moment the sequence is handed over, **before a
single item has been produced**. A behaviour wrapped around it cannot observe items, cannot catch an
exception thrown part way through, and times only the handover.

`IStreamPipelineBehavior<TRequest, TResponse>` wraps the enumeration itself:

```C#
public class CountingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var count = 0;

        await foreach (var item in next(ct).WithCancellation(ct))
        {
            count++;
            yield return item;
        }

        Console.WriteLine($"streamed {count} items");
    }
}
```

### Scope lifetime

Under `RoutyaDispatchScope.Scoped`, the dispatch scope lives for the **whole enumeration** and is
disposed when it completes or the consumer abandons it. A handler may therefore hold a `DbContext`
across the stream. Each call to `CreateStream` gets its own scope.

---

## 🔬 Trimming and Native AOT

`Routya.Core` builds with the .NET trimming and AOT analyzers enabled and produces **no IL warnings**.
It contains no expression tree construction or compilation, and no reflection on the dispatch path.

A Native AOT console application using the source generator has been verified to compile and run,
covering async and synchronous handlers, pipeline behaviours and notification fan out.

### What is supported

| | Trimming / Native AOT |
|---|---|
| `Routya.SourceGenerators` with `AddGeneratedRoutya()` | ✅ Supported and verified |
| `AddRoutyaRequestHandler` / `AddRoutyaAsyncRequestHandler` / `AddRoutyaNotificationHandler` | ✅ Supported |
| `AddRoutya(cfg, assembly)` assembly scanning | ❌ Not supported. Marked `[RequiresUnreferencedCode]`, so you get a build warning rather than a runtime surprise |

### ⚠️ Register pipeline behaviours closed under Native AOT

Microsoft's DI container **cannot construct an open generic service under Native AOT when a type
argument is a value type**. This is a limitation of `Microsoft.Extensions.DependencyInjection`, not
of Routya, but it affects any request whose response is `int`, `decimal`, `bool`, a `Guid`, an enum
or any other struct:

```C#
// ❌ Throws under Native AOT for IRequest<decimal>, IRequest<int>, etc.
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// ✅ Register each behaviour closed instead
services.AddTransient<IPipelineBehavior<CalculateTotal, decimal>, LoggingBehavior<CalculateTotal, decimal>>();
```

Adding a closed registration *alongside* the open generic one does **not** help, because
`GetServices` enumerates every registration for the service type and still tries to construct the
open generic. The open generic registration has to be absent entirely.

Reference type responses are unaffected.

### Native AOT trades throughput for startup and size

Native AOT is not a performance optimisation for dispatch. Measured on the same machine, source
generated dispatch under Native AOT runs roughly **three to four times slower** than under the JIT,
because AOT gives up tiered compilation and dynamic profile guided optimisation, and Routya's
dispatch path is dense with interface calls that benefit from them.

What AOT buys is startup time, a self contained binary of a few megabytes, and no runtime
dependency. Choose it for those reasons, not for throughput.

---

## 🌐 Web API Demos

### Source Generator demo — [`Routya.WebApi.SourceGen.Demo`](./Routya.WebApi.SourceGen.Demo)

The recommended starting point if you're using `IGeneratedRoutya`. Demonstrates:
- ✅ **Compile-time dispatch** via `AddGeneratedRoutya()` — no reflection, no assembly scanning
- ✅ **Open-generic pipeline behavior** (`LoggingBehavior<TRequest, TResponse>`)
- ✅ **Notification fan-out** — two handlers dispatched in parallel from a single `PublishAsync`
- ✅ **`IAsyncEnumerable<T>` streaming** — products streamed one by one to the HTTP response

```powershell
cd Routya.WebApi.SourceGen.Demo
dotnet run
# http://localhost:5080
```

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/products` | List all products |
| GET | `/products/{id}` | Get a single product |
| POST | `/products` | Create a product |
| GET | `/products/stream` | Stream products via `IAsyncEnumerable<T>` |
| POST | `/orders/{id}/shipped` | Publish a notification to two handlers |

---

### Runtime dispatch demo — [`Routya.WebApi.Demo`](./Routya.WebApi.Demo)

Demonstrates `IRoutya` (runtime dispatch) with Entity Framework Core:
- ✅ **All three handler lifetimes** (Singleton, Scoped, Transient)
- ✅ **Entity Framework Core** with SQL Server
- ✅ **Full CRUD operations** via RESTful API
- ✅ **`IStreamRequest<T>` streaming** — `GET /api/products/stream` reads rows straight from the
  database with `AsAsyncEnumerable()` and writes each product to the response as it arrives, so
  nothing is buffered. The `Scoped` handler keeps its `DbContext` for the whole enumeration

```powershell
cd Routya.WebApi.Demo
dotnet run
# http://localhost:5079
```

