using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Test;

public class RequestDispatcherTests
{
    private readonly IServiceProvider _provider;

    public RequestDispatcherTests()
    {
        var services = new ServiceCollection();

        services.AddScoped<IRequestHandler<PingRequest, string>, PingHandler>();
        services.AddScoped<IAsyncRequestHandler<PingRequest, string>, PingHandler>();
        services.AddSingleton<IRoutyaRequestDispatcher, CompiledRequestInvokerDispatcher>();

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void Should_Send_Sync_Request()
    {
        var dispatcher = _provider.GetRequiredService<IRoutyaRequestDispatcher>();
        var result = dispatcher.Send<PingRequest, string>(new PingRequest("Sync"));
        Assert.Equal("Pong Sync", result);
    }

    [Fact]
    public async Task Should_Send_Async_Request()
    {
        var dispatcher = _provider.GetRequiredService<IRoutyaRequestDispatcher>();
        var result = await dispatcher.SendAsync<PingRequest, string>(new PingRequest("Async"));
        Assert.Equal("Pong Async", result);
    }

    public record PingRequest(string Message) : IRequest<string>;

    public class PingHandler : IRequestHandler<PingRequest, string>, IAsyncRequestHandler<PingRequest, string>
    {
        public string Handle(PingRequest request) => $"Pong {request.Message}";

        public Task<string> HandleAsync(PingRequest request, CancellationToken cancellationToken) =>
            Task.FromResult($"Pong {request.Message}");
    }
}