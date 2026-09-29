using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Assembly scanning must tolerate an assembly whose types cannot all be loaded. This happens in
/// real applications when a scanned assembly references something that is not deployed, for
/// example an optional provider package, and it is fatal at startup if it is not handled.
/// </summary>
public class AssemblyScanningTests
{
    [Fact]
    public void Scanning_An_Assembly_With_Unloadable_Types_Registers_The_Loadable_Ones()
    {
        // Arrange - mimics Assembly.GetTypes() on an assembly with a missing reference: it throws,
        // but the exception carries the types that did load, with nulls for the ones that did not.
        var assembly = new PartiallyLoadableAssembly(
            typeof(ScannedAsyncHandler),
            null,
            typeof(ScannedNotificationHandler),
            null);

        var services = new ServiceCollection();

        // Act
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, assembly);

        // Assert - the loadable handlers are registered rather than the whole call failing
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IAsyncRequestHandler<ScannedRequest, string>>());
        Assert.NotNull(scope.ServiceProvider.GetService<INotificationHandler<ScannedNotification>>());
    }

    [Fact]
    public async Task Handlers_From_A_Partially_Loadable_Assembly_Dispatch_Correctly()
    {
        var assembly = new PartiallyLoadableAssembly(typeof(ScannedAsyncHandler), null);

        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, assembly);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        var result = await routya.SendAsync<ScannedRequest, string>(new ScannedRequest("x"));

        Assert.Equal("scanned:x", result);
    }

    /// <summary>
    /// Stands in for an assembly with a missing reference. Assembly.GetTypes is virtual, so the
    /// failure can be reproduced without building a broken assembly on disk.
    /// </summary>
    private sealed class PartiallyLoadableAssembly : Assembly
    {
        private readonly Type?[] _types;

        public PartiallyLoadableAssembly(params Type?[] types) => _types = types;

        public override Type[] GetTypes()
            => throw new ReflectionTypeLoadException(_types, new Exception?[_types.Length]);
    }
}

// Test models
public record ScannedRequest(string Value) : IRequest<string>;
public record ScannedNotification : INotification;

public class ScannedAsyncHandler : IAsyncRequestHandler<ScannedRequest, string>
{
    public Task<string> HandleAsync(ScannedRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"scanned:{request.Value}");
}

public class ScannedNotificationHandler : INotificationHandler<ScannedNotification>
{
    public Task Handle(ScannedNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
