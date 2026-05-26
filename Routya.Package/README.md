# Routya

The all-in-one Routya package — install this and get everything you need.

[![NuGet](https://img.shields.io/nuget/v/Routya)](https://www.nuget.org/packages/Routya)
[![NuGet](https://img.shields.io/nuget/dt/Routya)](https://www.nuget.org/packages/Routya)

---

## What's Included

| Package | Description |
|---|---|
| [Routya.Core](https://www.nuget.org/packages/Routya.Core) | High-performance CQRS dispatcher — request/response, notifications, pipeline behaviors |
| [Routya.SourceGenerators](https://www.nuget.org/packages/Routya.SourceGenerators) | Compile-time source generator — zero reflection, type-specific dispatch, 46% faster than MediatR |

Instead of installing both packages separately, install `Routya` and both are pulled in automatically, including the source generator.

---

## Installation

```bash
dotnet add package Routya
```

That's it. No second package to remember.

---

## Quick Start

### 1. Define handlers

```csharp
using Routya.Core.Abstractions;

public record GetUserRequest(int UserId) : IRequest<User>;
public record User(int Id, string Name);

public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, User>
{
    public Task<User> HandleAsync(GetUserRequest request, CancellationToken ct)
        => Task.FromResult(new User(request.UserId, $"User_{request.UserId}"));
}

public class UserCreatedNotification : INotification
{
    public int UserId { get; set; }
}

public class EmailHandler : INotificationHandler<UserCreatedNotification>
{
    public Task Handle(UserCreatedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"Sending email for user {notification.UserId}");
        return Task.CompletedTask;
    }
}
```

### 2. Register

```csharp
using Routya.Generated;

services.AddGeneratedRoutya(); // source generator wires everything up at compile-time
```

### 3. Dispatch

```csharp
var routya = provider.GetRequiredService<IGeneratedRoutya>();

var user = await routya.SendAsync(new GetUserRequest(123));
await routya.PublishAsync(new UserCreatedNotification { UserId = 456 });
```

---

## Example Projects

See working demos in the [GitHub repository](https://github.com/HBartosch/Routya):

| Project | Description |
|---|---|
| [Routya.WebApi.SourceGen.Demo](https://github.com/HBartosch/Routya/tree/main/Routya.WebApi.SourceGen.Demo) | ASP.NET Core minimal API — CRUD, pipeline behaviors, notification fan-out, `IAsyncEnumerable<T>` streaming |
| [Routya.SourceGen.Demo](https://github.com/HBartosch/Routya/tree/main/Routya.SourceGen.Demo) | Console app — basic request/response and notification dispatch |

---

## Full Documentation

- **[Routya.Core docs](https://www.nuget.org/packages/Routya.Core)** — runtime dispatcher, pipeline behaviors, scoped dispatch, notification strategies
- **[Routya.SourceGenerators docs](https://www.nuget.org/packages/Routya.SourceGenerators)** — how the source generator works, generated code, performance benchmarks, migration guide

---

## Links

- **GitHub**: [github.com/HBartosch/Routya](https://github.com/HBartosch/Routya)
- **Issues**: [github.com/HBartosch/Routya/issues](https://github.com/HBartosch/Routya/issues)
- **License**: [MIT](https://github.com/HBartosch/Routya/blob/main/LICENSE)
