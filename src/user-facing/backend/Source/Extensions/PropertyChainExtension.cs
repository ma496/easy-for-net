using System.Linq.Expressions;
using System.Reflection;

namespace Backend.Extensions;

/// <summary>
/// Extension methods that turn a LINQ <see cref="Expression"/> into a
/// dotted/indexed property path string (for example
/// <c>Address.Lines[0].Value</c>) for use in validation error messages.
/// </summary>
public static class PropertyChainExtension
{
    /// <summary>
    /// Builds a dotted/indexed path string representing the property
    /// chain described by <paramref name="expression"/>.
    /// </summary>
    /// <param name="expression">The expression whose path should be extracted.</param>
    /// <returns>A property path such as <c>Address.Lines[0].Value</c>.</returns>
    /// <exception cref="NotSupportedException">Thrown when the expression shape is not supported.</exception>
    public static string GetPropertyChain(this Expression expression)
    {
        return expression switch
        {
            MemberExpression m => BuildMemberChain(m),
            BinaryExpression { NodeType: ExpressionType.ArrayIndex } be => FormatIndexerExpression(be.Left, be.Right),
            MethodCallExpression { Object: not null } mce when IsItemAccessor(mce) => FormatIndexerExpression(mce.Object, mce.Arguments[0]),
            UnaryExpression ue => GetPropertyChain(ue.Operand),
            _ => throw new NotSupportedException($"[{expression}] is not a supported expression type!")
        };
    }

    static bool IsItemAccessor(MethodCallExpression mce)
        => mce.Method.Name == "get_Item" && mce.Arguments.Count == 1;

    static string FormatIndexerExpression(Expression objectExpression, Expression indexExpression)
    {
        var indexValueText = GetValue(indexExpression) switch
        {
            string s => $"\"{s}\"",
            var v => v?.ToString()
        };

        return $"{GetPropertyChain(objectExpression)}[{indexValueText}]";
    }

    static string BuildMemberChain(MemberExpression memberExpression)
        => memberExpression.Expression is null or ParameterExpression
               ? memberExpression.Member.Name
               : $"{GetPropertyChain(memberExpression.Expression)}.{memberExpression.Member.Name}";

    static object? GetValue(Expression? expression)
        => expression switch
        {
            null => throw new ArgumentNullException(nameof(expression), "Expression cannot be null."),
            ConstantExpression ce => ce.Value,
            MemberExpression me => GetValue(me),
            MethodCallExpression mce => GetValue(mce),
            BinaryExpression be => GetValue(be),
            _ => GetValueCompiled(expression)
        };

    static object? GetValue(MemberExpression expression)
    {
        var value = GetValue(expression.Expression);

        return expression.Member switch
        {
            FieldInfo fi => fi.GetValue(value),
            PropertyInfo pi => pi.GetValue(value),
            _ => throw new NotSupportedException($"[{expression}] is not a supported member expression!")
        };
    }

    static object? GetValue(BinaryExpression expression)
    {
        if (expression.NodeType != ExpressionType.ArrayIndex)
            return GetValueCompiled(expression);

        if (GetValue(expression.Left) is not Array array)
            throw new NullReferenceException($"[{expression.Left}] is null!");

        if (GetValue(expression.Right) is not int index)
            throw new NullReferenceException($"[{expression.Right}] is null or unsupported value!");

        return array.GetValue(index);
    }

    static object? GetValue(MethodCallExpression expression)
    {
        var args = expression.Arguments.Select(GetValue).ToArray();
        var obj = GetValue(expression.Object);

        return expression.Method.Invoke(obj, args);
    }

    static object? GetValueCompiled(Expression expression)
        => Expression.Lambda(expression).Compile().DynamicInvoke();
}
