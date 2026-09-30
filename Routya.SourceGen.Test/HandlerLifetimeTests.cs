using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;

namespace Routya.SourceGen.Test;

/// <summary>
/// Proves the generated handler lifetime actually takes effect at runtime, rather than only
/// appearing in the generated text.
/// </summary>
/// <remarks>
/// The generated default is Transient, while the runtime AddRoutya defaults to Scoped. A project
/// moving from runtime dispatch to the generator therefore changes handler lifetime unless it
/// passes one explicitly. These tests pin both the default and the override.
/// </remarks>
public class HandlerLifetimeTests
{
    [Fact]
    public void Default_Is_Transient_So_Each_Resolution_Gets_A_New_Handler()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<LifetimeProbeHandler>();
        var second = scope.ServiceProvider.GetRequiredService<LifetimeProbeHandler>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Passing_Scoped_Gives_One_Handler_Per_Scope()
    {
        var provider = BuildProvider(ServiceLifetime.Scoped);

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<LifetimeProbeHandler>();
        var second = scope.ServiceProvider.GetRequiredService<LifetimeProbeHandler>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Passing_Scoped_Still_Gives_Different_Handlers_Across_Scopes()
    {
        var provider = BuildProvider(ServiceLifetime.Scoped);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<LifetimeProbeHandler>(),
            second.ServiceProvider.GetRequiredService<LifetimeProbeHandler>());
    }

    [Fact]
    public void Passing_Singleton_Gives_One_Handler_For_The_Application()
    {
        var provider = BuildProvider(ServiceLifetime.Singleton);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<LifetimeProbeHandler>(),
            second.ServiceProvider.GetRequiredService<LifetimeProbeHandler>());
    }

    [Fact]
    public void A_RoutyaHandler_Attribute_Wins_Over_The_Parameter()
    {
        // Registered Singleton by its attribute even though everything else is asked to be Scoped
        var provider = BuildProvider(ServiceLifetime.Scoped);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<PinnedSingletonHandler>(),
            second.ServiceProvider.GetRequiredService<PinnedSingletonHandler>());
    }

    [Fact]
    public async Task Dispatch_Still_Works_Whichever_Lifetime_Is_Chosen()
    {
        foreach (var lifetime in new[] { ServiceLifetime.Transient, ServiceLifetime.Scoped, ServiceLifetime.Singleton })
        {
            var provider = BuildProvider(lifetime);

            using var scope = provider.CreateScope();
            var routya = scope.ServiceProvider.GetRequiredService<IGeneratedRoutya>();

            var id = await routya.SendAsync(new LifetimeProbeRequest());

            Assert.NotEqual(Guid.Empty, id);
        }
    }

    private static ServiceProvider BuildProvider(ServiceLifetime? lifetime = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HandlerCallTracker());

        if (lifetime is null)
        {
            services.AddGeneratedRoutya();
        }
        else
        {
            services.AddGeneratedRoutya(lifetime.Value);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
