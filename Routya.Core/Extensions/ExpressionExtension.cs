using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Routya.Core.Extensions
{
    public static class ExpressionExtension
    {
        public static Expression ForEach(
            Type elementType,
            Expression collection,
            ParameterExpression loopVar,
            Expression loopContent)
        {
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);
            var getEnumeratorCall = Expression.Call(collection, "GetEnumerator", Type.EmptyTypes);
            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");

            var moveNextCall = Expression.Call(
                enumeratorVar,
                typeof(System.Collections.IEnumerator).GetMethod(nameof(System.Collections.IEnumerator.MoveNext))!
            );

            var breakLabel = Expression.Label("LoopBreak");

            return Expression.Block(
                new[] { enumeratorVar },
                Expression.Assign(enumeratorVar, getEnumeratorCall),
                Expression.TryFinally(
                    Expression.Loop(
                        Expression.IfThenElse(
                            Expression.IsFalse(moveNextCall),
                            Expression.Break(breakLabel),
                            Expression.Block(
                                new[] { loopVar },
                                Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                                loopContent
                            )
                        ),
                        breakLabel
                    ),
                    Expression.Call(enumeratorVar, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!)
                )
            );
        }
    }
}