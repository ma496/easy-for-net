using System;
using System.Linq.Expressions;
using System.Reflection;

namespace EasyForNet.Helpers
{
    public static class ObjectHelper
    {
        public static PropertyInfo PropertyInfo<T>(Expression<Func<T, object>> propertyExpression)
            where T : class
        {
            var reflectedType = typeof(T);

            MemberExpression memberExpression;

            if (propertyExpression.Body is MemberExpression)
            {
                memberExpression = (MemberExpression) propertyExpression.Body;
            }
            else if (propertyExpression.Body.NodeType == ExpressionType.Convert &&
                     propertyExpression.Body is UnaryExpression)
            {
                var unaryExpression = (UnaryExpression) propertyExpression.Body;
                if (unaryExpression.Operand is MemberExpression)
                    memberExpression = (MemberExpression) unaryExpression.Operand;
                else
                    throw new ArgumentException(WrongPropertyExpressionMessage(propertyExpression));
            }
            else
            {
                throw new ArgumentException(WrongPropertyExpressionMessage(propertyExpression));
            }

            var propInfo = memberExpression.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException(WrongPropertyExpressionMessage(propertyExpression));

            if (propInfo.ReflectedType == null)
                throw new AggregateException(WrongPropertyExpressionMessage(propertyExpression));

            if (reflectedType != propInfo.ReflectedType && !reflectedType.IsSubclassOf(propInfo.ReflectedType))
                throw new ArgumentException(
                    $"Expression '{propertyExpression}' refers to a property that is not from type {reflectedType}.");

            return propInfo;
        }

        private static string WrongPropertyExpressionMessage<T>(Expression<Func<T, object>> expression)
        {
            return $"Expression '{expression}' should be like 'x => x.propertyName'";
        }
    }
}