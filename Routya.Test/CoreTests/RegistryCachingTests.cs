using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers;
using Routya.Core.Extensions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Routya.Test.CoreTests
{
    public class RegistryCachingTests
    {
        [Fact]
        public async Task Request_FallbackHandler_ShouldBeAddedToRegistry_AfterFirstCall()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register Routya WITHOUT assembly scanning (empty registry)
            services.AddRoutya();
            
            // Register handler using traditional DI (not in registry initially)
            services.AddScoped<IAsyncRequestHandler<TestRequest, string>, TestRequestHandler>();
            
            var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IRoutya>();
            
            // Act - First call uses fallback
            var result1 = await dispatcher.SendAsync<TestRequest, string>(new TestRequest { Value = "First" });
            
            // Act - Second call should use registry (handler was cached after first call)
            var result2 = await dispatcher.SendAsync<TestRequest, string>(new TestRequest { Value = "Second" });
            
            // Assert
            Assert.Equal("Handler processed: First", result1);
            Assert.Equal("Handler processed: Second", result2);
        }
        
        [Fact]
        public async Task Notification_FallbackHandler_ShouldBeInvoked_OnEveryPublish()
        {
            // Arrange
            var services = new ServiceCollection();

            // Register Routya WITHOUT assembly scanning (empty registry)
            services.AddRoutya();

            // Register handler using traditional DI (not in registry)
            services.AddScoped<INotificationHandler<TestNotification>, TestNotificationHandler>();

            var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var dispatcher = provider.GetRequiredService<IRoutya>();

            TestNotificationHandler.MessagesReceived.Clear();

            // Act - handlers outside the registry are resolved from the dispatch scope every publish
            await dispatcher.PublishAsync(new TestNotification { Message = "First" });
            await dispatcher.PublishAsync(new TestNotification { Message = "Second" });

            // Assert - the handler runs on both publishes, not just the first
            Assert.Equal(new[] { "First", "Second" }, TestNotificationHandler.MessagesReceived);
        }
        
        // Test types
        public class TestRequest : IRequest<string>
        {
            public string Value { get; set; } = null!;
        }
        
        public class TestRequestHandler : IAsyncRequestHandler<TestRequest, string>
        {
            public Task<string> HandleAsync(TestRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult($"Handler processed: {request.Value}");
            }
        }
        
        public class TestNotification : INotification
        {
            public string Message { get; set; } = null!;
        }
        
        public class TestNotificationHandler : INotificationHandler<TestNotification>
        {
            public static List<string> MessagesReceived { get; } = new List<string>();

            public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
            {
                MessagesReceived.Add(notification.Message);
                return Task.CompletedTask;
            }
        }
    }
}
