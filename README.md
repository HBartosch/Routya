# Routya

**Routya** is a fast, lightweight message dispatching library built for .NET applications that use the CQRS pattern.  
It provides a flexible way to route requests/responses and notifications to their respective handlers with minimal overhead and high performance.

> 🚧 Routya is currently in **alpha** — APIs may change as we refine the design and gather feedback.

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

Latest prerelease version:
```bash
dotnet add package Routya.Core --version 1.0.0-alpha.2
```
## 🚀 Quick Start

#Dependency injection
On startup you can define if **Routya** should create a new instance of the service provider each time it is called or work on the root service provider. 

Note!!! By default scope is enabled

Scoped
```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
```

Root
```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Root, Assembly.GetExecutingAssembly());
```

You can add an auto registration of IRequestHandler, IAsyncRequestHandler and INotificationHandler by adding the executing assembly

Note!!! By default you would have to manually register your Requests/Notifications and Handlers

```C#
    builder.Services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
```

# Requests

### 📊 Benchmark Results
Note! Benchmarks were run with handlers returning only a string using BenchmarkDotNet
| Method           | Mean      | Error    | StdDev   | Code Size | Allocated |
|----------------- |----------:|---------:|---------:|----------:|----------:|
| Routya_Send      |  76.59 us | 1.491 us | 1.831 us |   8,684 B |   6.31 KB |
| Routya_SendAsync | 246.53 us | 1.848 us | 1.638 us |   8,813 B |   8.94 KB |

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
     _dispatcher = dispatcher
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







