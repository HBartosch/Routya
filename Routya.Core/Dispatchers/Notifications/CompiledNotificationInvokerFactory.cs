using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Configurations;

namespace Routya.Core.Dispatchers.Notifications
{
    internal static class CompiledNotificationInvokerFactory
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>> _cache = 
            new ConcurrentDictionary<Type, Func<object, object, CancellationToken, Task>>();

        public static Func<object, object, CancellationToken, Task> GetOrAdd(Type handlerType, Type notificationType)
        {
            return _cache.GetOrAdd(handlerType, _ => CreateInvoker(notificationType));
        }

        private static Func<object, object, CancellationToken, Task> CreateInvoker(Type notificationType)
        {
            var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notificationType);
            var handleMethod = handlerInterface.GetMethod(nameof(INotificationHandler<INotification>.Handle))!;

            var handlerParam = Expression.Parameter(typeof(object), CompiledConstant.HandlerParameterName);
            var notificationParam = Expression.Parameter(typeof(object), CompiledConstant.NotificationParameterName);
            var ctParam = Expression.Parameter(typeof(CancellationToken), CompiledConstant.CancellationTokenParameterName);

            var call = Expression.Call(
                Expression.Convert(handlerParam, handlerInterface),
                handleMethod,
                Expression.Convert(notificationParam, notificationType),
                ctParam
            );

            var lambda = Expression.Lambda<Func<object, object, CancellationToken, Task>>(call, handlerParam, notificationParam, ctParam);
            return lambda.Compile();
        }
    }
}
