using EasyForNetTool.Parsing;

namespace EasyForNetTool.Generator;

/// <summary>
/// Base class for code generators that operate on a specific argument type.
/// </summary>
/// <typeparam name="TArgument">The type of argument this generator handles.</typeparam>
public abstract class CodeGeneratorBase<TArgument>
    where TArgument : Argument
{
    /// <summary>
    /// Generates code or project files based on the given argument.
    /// </summary>
    /// <param name="argument">The typed argument with generation parameters.</param>
    public abstract Task Generate(TArgument argument);
}

