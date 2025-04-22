namespace Routya.Core.Dispatchers.Configurations
{
    internal static class CompiledConstant
    {
        public static string ServiceProviderParameterName => "sp";
        public static string CancellationTokenParameterName => "ct";
        public static string NotificationParameterName => "notification";
        public static string RequestParameterName => "request";
        public static string HandlerParameterName => "handler";
        public static string SelectedHandlerParameterName => "matched";
        public static string LoopBreakName => "LoopBreak";
    }
}
