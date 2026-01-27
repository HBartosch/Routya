using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

public class ScopeManagementTests
{
    [Fact]
    public async Task Root_Scope_Should_Use_Root_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Root;
            cfg.HandlerLifetime = ServiceLifetime.Singleton;
        }, typeof(ScopeManagementTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        var routya = provider.GetRequiredService<IRoutya>();
        
        // Act
        SingletonScopeHandler.InstanceCount = 0;
        
        await routya.SendAsync<ScopeTestRequest, int>(new ScopeTestRequest());
        await routya.SendAsync<ScopeTestRequest, int>(new ScopeTestRequest());
        await routya.SendAsync<ScopeTestRequest, int>(new ScopeTestRequest());
        
        // Assert - Should use same instance (singleton with root scope)
        Assert.Equal(1, SingletonScopeHandler.InstanceCount);
    }
    
    [Fact]
    public async Task Scoped_Scope_Should_Create_New_Scope_Per_Request()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, typeof(ScopeManagementTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        var routya = provider.GetRequiredService<IRoutya>();
        
        // Act
        ScopedScopeHandler.InstanceIds.Clear();
        
        await routya.SendAsync<ScopedTestRequest, int>(new ScopedTestRequest());
        await routya.SendAsync<ScopedTestRequest, int>(new ScopedTestRequest());
        await routya.SendAsync<ScopedTestRequest, int>(new ScopedTestRequest());
        
        // Assert - Should have 3 different instances
        Assert.Equal(3, ScopedScopeHandler.InstanceIds.Distinct().Count());
    }
    
    [Fact]
    public async Task Transient_Handlers_Should_Create_New_Instance_Each_Time()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Transient;
        }, typeof(ScopeManagementTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        var routya = provider.GetRequiredService<IRoutya>();
        
        // Act
        TransientScopeHandler.InstanceIds.Clear();
        
        await routya.SendAsync<TransientTestRequest, int>(new TransientTestRequest());
        await routya.SendAsync<TransientTestRequest, int>(new TransientTestRequest());
        await routya.SendAsync<TransientTestRequest, int>(new TransientTestRequest());
        
        // Assert - Should have 3 different instances
        Assert.Equal(3, TransientScopeHandler.InstanceIds.Distinct().Count());
    }
    
    [Fact]
    public async Task Scoped_With_DbContext_Pattern_Should_Work()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<FakeDbContext>(); // Scoped dependency
        services.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, typeof(ScopeManagementTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        var routya = provider.GetRequiredService<IRoutya>();
        
        // Act
        var result1 = await routya.SendAsync<DbContextRequest, string>(new DbContextRequest());
        var result2 = await routya.SendAsync<DbContextRequest, string>(new DbContextRequest());
        
        // Assert - Each request should get its own DbContext instance
        Assert.NotEqual(result1, result2);
    }
    
    [Fact]
    public async Task Notification_Scope_Should_Be_Shared_Across_All_Handlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SharedScopeService>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ScopeManagementTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        var routya = provider.GetRequiredService<IRoutya>();
        
        // Act
        await routya.PublishAsync(new SharedScopeNotification());
        
        // Assert - Both handlers should see the same service instance
        Assert.Equal(SharedScopeNotificationHandler1.ServiceId, SharedScopeNotificationHandler2.ServiceId);
    }
}

// Test Models
public record ScopeTestRequest : IRequest<int>;
public record ScopedTestRequest : IRequest<int>;
public record TransientTestRequest : IRequest<int>;
public record DbContextRequest : IRequest<string>;
public record SharedScopeNotification : INotification;

public class SingletonScopeHandler : IAsyncRequestHandler<ScopeTestRequest, int>
{
    public static int InstanceCount { get; set; }
    
    public SingletonScopeHandler()
    {
        InstanceCount++;
    }
    
    public Task<int> HandleAsync(ScopeTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(InstanceCount);
    }
}

public class ScopedScopeHandler : IAsyncRequestHandler<ScopedTestRequest, int>
{
    public static List<Guid> InstanceIds { get; } = new();
    private readonly Guid _instanceId;
    
    public ScopedScopeHandler()
    {
        _instanceId = Guid.NewGuid();
        InstanceIds.Add(_instanceId);
    }
    
    public Task<int> HandleAsync(ScopedTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(InstanceIds.Count);
    }
}

public class TransientScopeHandler : IAsyncRequestHandler<TransientTestRequest, int>
{
    public static List<Guid> InstanceIds { get; } = new();
    private readonly Guid _instanceId;
    
    public TransientScopeHandler()
    {
        _instanceId = Guid.NewGuid();
        InstanceIds.Add(_instanceId);
    }
    
    public Task<int> HandleAsync(TransientTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(InstanceIds.Count);
    }
}

public class FakeDbContext
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class DbContextRequestHandler : IAsyncRequestHandler<DbContextRequest, string>
{
    private readonly FakeDbContext _dbContext;
    
    public DbContextRequestHandler(FakeDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public Task<string> HandleAsync(DbContextRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_dbContext.InstanceId.ToString());
    }
}

public class SharedScopeService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class SharedScopeNotificationHandler1 : INotificationHandler<SharedScopeNotification>
{
    public static Guid ServiceId { get; set; }
    
    public SharedScopeNotificationHandler1(SharedScopeService service)
    {
        ServiceId = service.Id;
    }
    
    public Task Handle(SharedScopeNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class SharedScopeNotificationHandler2 : INotificationHandler<SharedScopeNotification>
{
    public static Guid ServiceId { get; set; }
    
    public SharedScopeNotificationHandler2(SharedScopeService service)
    {
        ServiceId = service.Id;
    }
    
    public Task Handle(SharedScopeNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
