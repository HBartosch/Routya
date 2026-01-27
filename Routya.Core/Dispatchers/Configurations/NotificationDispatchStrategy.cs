namespace Routya.Core.Dispatchers.Configurations
{
    /// <summary>
    /// Defines the strategy for dispatching notifications to multiple handlers.
    /// </summary>
    public enum NotificationDispatchStrategy
    {
        /// <summary>
        /// Handlers are executed sequentially in registration order.
        /// Each handler completes before the next one starts.
        /// </summary>
        Sequential,
        
        /// <summary>
        /// Handlers are executed concurrently in parallel.
        /// All handlers start execution simultaneously.
        /// </summary>
        Parallel
    }
}