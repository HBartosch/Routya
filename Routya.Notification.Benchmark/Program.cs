using BenchmarkDotNet.Running;

namespace Routya.Notification.Benchmark;

internal class Program
{
    static void Main(string[] args) => BenchmarkRunner.Run<BenchmarkNotificationDispatch>(args: args);
}
