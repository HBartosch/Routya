using Microsoft.AspNetCore.Mvc;
using Routya.Core.Abstractions;
using Routya.WebApi.Demo.Notifications;

namespace Routya.WebApi.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IRoutya _routya;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(IRoutya routya, ILogger<NotificationsController> logger)
    {
        _routya = routya;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a user created notification (sequential execution)
    /// </summary>
    [HttpPost("user-created/sequential")]
    public async Task<IActionResult> PublishUserCreatedSequential([FromBody] UserCreatedRequest request)
    {
        _logger.LogInformation("Publishing UserCreatedNotification (Sequential) for: {Name}", request.Name);
        
        var notification = new UserCreatedNotification(request.UserId, request.Name, request.Email);
        
        await _routya.PublishAsync(notification);
        
        return Ok(new { 
            Message = "Notification published sequentially", 
            UserId = request.UserId,
            HandlersExecuted = 3,
            ExecutionMode = "Sequential"
        });
    }

    /// <summary>
    /// Publishes a user created notification (parallel execution)
    /// </summary>
    [HttpPost("user-created/parallel")]
    public async Task<IActionResult> PublishUserCreatedParallel([FromBody] UserCreatedRequest request)
    {
        _logger.LogInformation("Publishing UserCreatedNotification (Parallel) for: {Name}", request.Name);
        
        var notification = new UserCreatedNotification(request.UserId, request.Name, request.Email);
        
        await _routya.PublishParallelAsync(notification);
        
        return Ok(new { 
            Message = "Notification published in parallel", 
            UserId = request.UserId,
            HandlersExecuted = 3,
            ExecutionMode = "Parallel"
        });
    }

    /// <summary>
    /// Test endpoint to demonstrate different handler lifetimes
    /// </summary>
    [HttpPost("test-lifetimes")]
    public async Task<IActionResult> TestLifetimes()
    {
        _logger.LogInformation("Testing handler lifetimes with multiple notifications...");
        
        // Publish 3 notifications to see handler lifetime behavior
        for (int i = 1; i <= 3; i++)
        {
            var notification = new UserCreatedNotification(
                UserId: i,
                Name: $"User {i}",
                Email: $"user{i}@example.com"
            );
            
            await _routya.PublishAsync(notification);
            _logger.LogInformation("--- Notification {Count} completed ---", i);
        }
        
        return Ok(new { 
            Message = "Published 3 notifications to test handler lifetimes",
            Note = "Check logs to see Singleton (same instance), Scoped (per-request), Transient (new each time)",
            HandlersPerNotification = 3,
            TotalExecutions = 9
        });
    }
}

public record UserCreatedRequest(int UserId, string Name, string Email);
