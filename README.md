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

## ✨ Features

- ✅ Clean interface-based abstraction for Requests/Responses and Notifications
- 🚀 **High-performance dispatching** - Competitive with MediatR while offering more flexibility
- ⚙️ **Configurable handler lifetimes** - Choose Singleton, Scoped, or Transient per handler
- 🧩 Optional pipeline behavior support for cross-cutting concerns
- 🔄 Supports both **sequential** and **parallel** notification dispatching
- 🎯 **Multi-framework support** - netstandard2.0, netstandard2.1, .NET 8, .NET 9, .NET 10
- 💾 **Memory efficient** - Zero memory leaks with proper scope management
- ♻️ Simple to extend and integrate with your existing architecture
- 🧪 Built with performance and clarity in mind

---

## 📦 NuGet Package

Latest version:
```bash
dotnet add package Routya.Core --version 1.0.5
```
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
- ✅ **30% faster** for notifications (110ns vs 158ns with Singleton)
- ✅ **Type-safe** - Compile-time verification of handler signatures
- ✅ **Flexible lifetimes** - Choose Singleton/Scoped/Transient per handler

### Option 3: Traditional DI Registration (Still Supported)
You can also use standard DI registration - works with auto-caching fallback:

```C#
// Register Routya core services (no assembly scanning)
builder.Services.AddRoutya();

// Traditional DI registration (automatically cached to registry on first use)
builder.Services.AddSingleton<IAsyncRequestHandler<CreateProductRequest, Product>, CreateProductHandler>();
builder.Services.AddScoped<IAsyncRequestHandler<GetProductRequest, Product?>, GetProductHandler>();
builder.Services.AddTransient<IAsyncRequestHandler<GetAllProductsRequest, List<Product>>, GetAllProductsHandler>();

// Notification handlers (automatically cached on first publish)
builder.Services.AddSingleton<INotificationHandler<UserRegisteredNotification>, SendEmailHandler>();
```

**Trade-off**: First call uses standard DI resolution (~5-10% slower), subsequent calls automatically use optimized registry.

**Performance Comparison:**
- **Singleton**: ~380 ns (2% slower than MediatR, 50% less memory, best for stateless handlers)
- **Transient**: ~384 ns (3% slower than MediatR, matches memory, maximum isolation)  
- **Scoped**: ~440 ns (18% overhead, safe for DbContext and scoped dependencies)

Routya lets YOU choose the right lifetime per handler:
- 🚀 **Singleton** for stateless handlers = fastest, least memory
- 🔄 **Scoped** for handlers with DbContext = safe with proper scope management  
- 🔒 **Transient** when you need maximum isolation = new instance every time

### Backward Compatibility & Auto-Registry
Routya maintains full backward compatibility with traditional DI registration:

```C#
// Traditional registration (still works!)
builder.Services.AddScoped<IAsyncRequestHandler<MyRequest, MyResponse>, MyHandler>();
builder.Services.AddScoped<INotificationHandler<MyNotification>, MyNotificationHandler>();
```

**Smart Fallback with Auto-Caching:**
When handlers aren't found in the registry, Routya automatically:
1. Falls back to `GetService/GetServices` resolution (first call)
2. **Adds discovered handlers to the registry** (automatic optimization!)
3. Uses fast registry-based dispatch for all subsequent calls

This ensures:
- ✅ **First call**: Fallback resolution (~same speed as traditional)
- ✅ **Second+ calls**: Registry-optimized dispatch (~28% faster for notifications!)
- ✅ Smooth migration path from older versions
- ✅ Works with existing code without changes
- ✅ Automatic performance improvement after first use

# Requests

### 📊 Benchmark Results (.NET 8 - November 2025)
Benchmarks comparing Routya against MediatR 13.1.0 with simple request handlers (BenchmarkDotNet v0.14.0)

**Test Environment:**
- CPU: 11th Gen Intel Core i7-11800H @ 2.30GHz (8 cores, 16 logical processors)
- RAM: System with AVX-512F support
- OS: Windows 11 (10.0.22623)
- .NET: 8.0.17 (8.0.1725.26602), X64 RyuJIT
- GC: Concurrent Server

#### Request Dispatching Performance
| Method                     | Mean     | Ratio | Gen0   | Allocated | Notes |
|--------------------------- |---------:|------:|-------:|----------:|-------|
| MediatR_SendAsync          | 356.3 ns |  1.00 | 0.0038 |    1016 B | Baseline |
| **Routya_Singleton_Send**      | **377.8 ns** |  **1.06** | 0.0038 |    1008 B | ⚡ 6% slower, matches memory |
| **Routya_Transient_Send**      | **397.0 ns** |  **1.11** | 0.0038 |    1032 B | ⚡ 11% slower, matches memory |
| **Routya_Scoped_Send**         | **427.0 ns** |  **1.20** | 0.0048 |    1216 B | Scoped DI overhead |
| **Routya_Singleton_SendAsync** | **436.8 ns** |  **1.23** | 0.0048 |    1168 B | 23% overhead for async |
| **Routya_Transient_SendAsync** | **450.2 ns** |  **1.26** | 0.0048 |    1192 B | 26% overhead for async |
| **Routya_Scoped_SendAsync**    | **495.0 ns** |  **1.39** | 0.0048 |    1376 B | Scoped + async overhead |

**Key Highlights:**
- ✅ **Singleton/Transient handlers** are competitive with MediatR (6-11% difference)
- ✅ **Registry-based dispatch** with auto-caching fallback
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
            var result = await next();
            Console.WriteLine($"[Logging] ✓ {typeof(TRequest).Name}");
            return result;
        }
    }
```

# Notifications

### 📊 Notification Dispatching Performance
Benchmarks comparing Routya against MediatR 13.1.0 for notification patterns (BenchmarkDotNet v0.14.0)

| Method                      | Mean     | Ratio | Gen0   | Allocated | Notes |
|---------------------------- |---------:|------:|-------:|----------:|-------|
| MediatR_Publish             | 157.6 ns |  1.00 | 0.0017 |     440 B | Baseline |
| **Routya_Singleton_Sequential** | **110.5 ns** |  **0.70** | 0.0007 |     192 B | ⚡ **30% faster, 56% less memory!** |
| **Routya_Singleton_Parallel**   | **143.6 ns** |  **0.91** | 0.0012 |     312 B | 9% faster, 29% less memory |
| **Routya_Transient_Sequential** | **146.0 ns** |  **0.93** | 0.0010 |     240 B | 7% faster, 45% less memory |
| **Routya_Transient_Parallel**   | **170.6 ns** |  **1.08** | 0.0014 |     360 B | 8% slower (parallel overhead) |
| **Routya_Scoped_Sequential**    | **238.1 ns** |  **1.51** | 0.0014 |     424 B | Scoped DI overhead |
| **Routya_Scoped_Parallel**      | **265.8 ns** |  **1.69** | 0.0019 |     544 B | Scoped + parallel overhead |

**Key Highlights:**
- ✅ **Singleton sequential** 30% faster than MediatR with 56% less memory (192B vs 440B) 🚀
- ✅ **Transient sequential** 7% faster than MediatR with 45% less memory (240B vs 440B)
- ✅ **Registry-based dispatch with auto-caching** - Zero GetServices calls after first use
- ✅ **Parallel dispatching** available with minimal overhead
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

---

## 🌐 Web API Demo

The **Routya.WebApi.Demo** project demonstrates Routya in a production-like environment with:
- ✅ **All three handler lifetimes** (Singleton, Scoped, Transient)
- ✅ **Entity Framework Core** with SQL Server
- ✅ **Full CRUD operations** via RESTful API
- ✅ **Real-world performance** testing

### Running the Demo

```powershell
# Start the Web API
cd Routya.WebApi.Demo
dotnet run
```

The API will be available at: `http://localhost:5079`

### Testing with the PowerShell Script

```powershell
# Run the comprehensive test script
cd Routya.WebApi.Demo
.\test-requests.ps1
```

The test script demonstrates:
- **Singleton handlers**: Product creation & stock updates (fastest performance)
- **Scoped handlers**: Get single product & delete (one instance per HTTP request)
- **Transient handlers**: Get all products (new instance every call, maximum isolation)

### API Endpoints

| Method | Endpoint | Handler Lifetime | Description |
|--------|----------|------------------|-------------|
| POST | `/api/products` | Singleton | Create new product |
| GET | `/api/products` | Transient | Get all products |
| GET | `/api/products/{id}` | Scoped | Get product by ID |
| PUT | `/api/products/{id}/stock` | Singleton | Update product stock |
| DELETE | `/api/products/{id}` | Scoped | Delete product |

