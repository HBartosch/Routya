namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers how the generated PublishAsync fans out to multiple notification handlers.
/// </summary>
public class GeneratorNotificationTests
{
    private static string DispatcherFor(int handlerCount)
    {
        var handlers = string.Join("\n", Enumerable.Range(0, handlerCount).Select(i => $@"
    public class Handler{i} : INotificationHandler<UserCreated>
    {{
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }}"));

        var result = GeneratorHarness.Run($@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{{
    public class UserCreated : INotification {{ }}
{handlers}
}}");

        result.AssertCompiles();

        return result.GeneratedSources.Single(kvp => kvp.Key.Contains("Dispatcher")).Value;
    }

    [Fact]
    public void Two_Handlers_Are_Awaited_Together_Rather_Than_One_After_The_Other()
    {
        var dispatcher = DispatcherFor(2);

        // Awaiting two started tasks in sequence drops the second task's exception when the first
        // faults, so the fan out must go through Task.WhenAll.
        Assert.Contains("Task.WhenAll", dispatcher);
        Assert.DoesNotContain("var task0", dispatcher);
        Assert.DoesNotContain("var task1", dispatcher);
    }

    [Fact]
    public void Three_Handlers_Are_Awaited_Together()
    {
        var dispatcher = DispatcherFor(3);

        Assert.Contains("Task.WhenAll", dispatcher);
    }

    [Fact]
    public void A_Single_Handler_Is_Awaited_Directly_Without_WhenAll()
    {
        var dispatcher = DispatcherFor(1);

        Assert.Contains("handler0.Handle(notification, cancellationToken)", dispatcher);
        Assert.DoesNotContain("Task.WhenAll", dispatcher);
    }
}
