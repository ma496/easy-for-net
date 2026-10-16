using EasyForNetTool.Parsing;

namespace EasyForNetTool.Generator;

/// <summary>
/// Top-level code generator that dispatches to the appropriate generator based on argument type.
/// </summary>
public class CodeGenerator
{
    /// <summary>
    /// Generates code or project files based on the provided argument.
    /// </summary>
    /// <param name="argument">The parsed argument specifying what to generate.</param>
    public async Task Generate(Argument argument)
    {
        if (argument is CreateProjectArgument createProjectArgument)
        {
            await new CreateProjectGenerator().Generate(createProjectArgument);
        }

        else
            throw new Exception($"Invalid argument type: {argument.Type}");
    }
}
