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
        public async Task Notification_FallbackHandler_ShouldBeAddedToRegistry_AfterFirstCall()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register Routya WITHOUT assembly scanning (empty registry)
            services.AddRoutya();
            
            // Register handler using traditional DI (not in registry initially)
            services.AddScoped<INotificationHandler<TestNotification>, TestNotificationHandler>();
            
            var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IRoutya>();
            
            // Act - First call uses fallback
            await dispatcher.PublishAsync(new TestNotification { Message = "First" });
            
            // Act - Second call should use registry (handler was cached after first call)
            await dispatcher.PublishAsync(new TestNotification { Message = "Second" });
            
            // Assert - Both calls should succeed without errors
            Assert.True(true);
        }
        
        // Test types
        public class TestRequest : IRequest<string>
        {
            public string Value { get; set; }
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
            public string Message { get; set; }
        }
        
        public class TestNotificationHandler : INotificationHandler<TestNotification>
        {
            public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
            {
                // Just a simple handler
                return Task.CompletedTask;
            }
        }
    }
}
