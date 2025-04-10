using BenchmarkDotNet.Running;

namespace Routya.Notification.Benchmark;

internal class Program
{
    static void Main() => BenchmarkRunner.Run<BenchmarkNotificationDispatch>();
}
