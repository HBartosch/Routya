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
- 🚀 High-performance dispatching via compiled delegates (no reflection or dynamic resolution)
- 🧩 Optional pipeline behavior support for cross-cutting concerns
- 🔄 Supports both **sequential** and **parallel** notification dispatching
- ♻️ Simple to extend and integrate with your existing architecture
- 🧪 Built with performance and clarity in mind

---

## 📦 NuGet Package

Latest version:
```bash
dotnet add package Routya.Core --version 1.0.4
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

You can add an auto registration of IRequestHandler, IAsyncRequestHandler and INotificationHandler by adding the executing assembly. This however registers all your request handlers as scoped.

Note!!! By default you would have to manually register your Requests/Notifications and Handlers

```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
```

# Requests

### 📊 Benchmark Results
Note! Benchmarks were run with handlers returning only a string using BenchmarkDotNet
| Method            | Mean     | Error   | StdDev  | Code Size | Gen0   | Allocated |
|------------------ |---------:|--------:|--------:|----------:|-------:|----------:|
| Routya_Send       | 296.6 ns | 3.15 ns | 2.94 ns |   8,676 B | 0.0029 |     704 B |
| Routya_SendAsync  | 346.1 ns | 5.49 ns | 5.13 ns |   8,801 B | 0.0029 |     784 B |

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

### 📊 Benchmark Results
Note! Benchmarks were run with handlers returning only Task.Completed using BenchmarkDotNet
| Method                    | Mean     | Error   | StdDev  | Code Size | Gen0   | Allocated |
|-------------------------- |---------:|--------:|--------:|----------:|-------:|----------:|
| RoutyaCompiled_Sequential | 315.9 ns | 2.21 ns | 1.96 ns |     368 B | 0.0019 |     528 B |
| RoutyaCompiled_Parallel   | 338.1 ns | 2.84 ns | 2.52 ns |     368 B | 0.0024 |     648 B |

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
