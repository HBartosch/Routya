# Routya
![CI](https://img.shields.io/github/actions/workflow/status/hbartosch/routya/dotnet.yml?label=CI&style=flat-square)
![CI](https://img.shields.io/github/actions/workflow/status/hbartosch/routya/build-and-test.yml?label=Tests&style=flat-square)
[![NuGet](https://img.shields.io/nuget/v/Routya.Core)](https://www.nuget.org/packages/Routya.Core)
[![NuGet](https://img.shields.io/nuget/dt/Routya.Core)](https://www.nuget.org/packages/Routya.Core)
![.NET Standard](https://img.shields.io/badge/netstandard-2.0%20%7C%202.1-blue?logo=dotnet&logoColor=white)

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

### Option 2: Manual Registration (No Assembly Scanning)
Register Routya core services without automatic handler registration:

```C#
// Register Routya core services only (no assembly scanning)
builder.Services.AddRoutya();

// Then manually register your handlers as needed
builder.Services.AddScoped<IAsyncRequestHandler<CreateProductRequest, Product>, CreateProductHandler>();
builder.Services.AddScoped<INotificationHandler<UserRegisteredNotification>, SendEmailHandler>();
```

### Option 3: Mixed Lifetimes (Recommended for Production)
Register handlers individually with different lifetimes based on their requirements:

```C#
// Register Routya core services (no assembly scanning)
builder.Services.AddRoutya();

// Singleton - fastest, shared instance (stateless handlers)
builder.Services.AddSingleton<IAsyncRequestHandler<CreateProductRequest, Product>, CreateProductHandler>();

// Scoped - one per HTTP request (handlers using DbContext, HttpContext)
builder.Services.AddScoped<IAsyncRequestHandler<GetProductRequest, Product?>, GetProductHandler>();

// Transient - new instance every time (maximum isolation)
builder.Services.AddTransient<IAsyncRequestHandler<GetAllProductsRequest, List<Product>>, GetAllProductsHandler>();
```

**Performance Comparison:**
- **Singleton**: ~357 ns (competitive with MediatR, best for stateless handlers)
- **Transient**: ~356 ns (matches MediatR performance, maximum isolation)  
- **Scoped**: ~433 ns (22% overhead, safe for DbContext and scoped dependencies)

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
| MediatR_SendAsync          | 354.1 ns |  1.00 | 0.0038 |    1016 B | Baseline |
| **Routya_Singleton_Send**      | **357.5 ns** |  **1.01** | 0.0033 |     920 B | ⚡ Competitive with MediatR |
| **Routya_Transient_Send**      | **356.3 ns** |  **1.01** | 0.0038 |     944 B | ⚡ Equal to MediatR |
| **Routya_Singleton_SendAsync** | **402.8 ns** |  **1.14** | 0.0043 |    1080 B | 14% overhead for async |
| **Routya_Transient_SendAsync** | **396.6 ns** |  **1.12** | 0.0043 |    1104 B | 12% overhead for async |
| **Routya_Scoped_Send**         | **433.6 ns** |  **1.22** | 0.0043 |    1128 B | Scoped DI overhead |
| **Routya_Scoped_SendAsync**    | **455.8 ns** |  **1.29** | 0.0048 |    1288 B | Scoped + async overhead |

**Key Highlights:**
- ✅ **Singleton/Transient handlers** match MediatR performance (~1% difference)
- ✅ **9-10% less memory allocation** for Singleton/Transient handlers
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
| MediatR_Publish             | 163.8 ns |  1.00 | 0.0017 |     440 B | Baseline |
| **Routya_Singleton_Sequential** | **163.5 ns** |  **1.00** | 0.0014 |     384 B | ⚡ Equal to MediatR, 13% less memory |
| **Routya_Singleton_Parallel**   | **181.6 ns** |  **1.11** | 0.0019 |     504 B | 11% overhead for parallelism |
| **Routya_Transient_Sequential** | **195.4 ns** |  **1.19** | 0.0021 |     560 B | Transient with sequential |
| **Routya_Transient_Parallel**   | **219.8 ns** |  **1.34** | 0.0026 |     680 B | Transient with parallel |
| **Routya_Scoped_Sequential**    | **337.9 ns** |  **2.06** | 0.0024 |     656 B | Scoped DI overhead |
| **Routya_Scoped_Parallel**      | **375.6 ns** |  **2.29** | 0.0029 |     776 B | Scoped + parallel overhead |

**Key Highlights:**
- ✅ **Singleton sequential** equals MediatR performance (1.00x ratio)
- ✅ **13% less memory allocation** for Singleton sequential (384B vs 440B)
- ✅ **Parallel dispatching** available with ~11% overhead
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

For complete documentation, see [Routya.WebApi.Demo/README.md](../Routya.WebApi.Demo/README.md)
