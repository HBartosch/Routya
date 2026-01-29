using Routya.Core.Abstractions;

namespace Routya.SourceGen.DatabaseDemo.Notifications;

public class ProductCreatedNotification : INotification
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
}
