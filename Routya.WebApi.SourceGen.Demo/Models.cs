namespace Routya.WebApi.SourceGen.Demo;

public record Product(int Id, string Name, decimal Price, int Stock);

public record OrderShippedNotification(int OrderId, int CustomerId) : Routya.Core.Abstractions.INotification;
