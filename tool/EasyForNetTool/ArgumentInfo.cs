namespace EasyForNetTool;

using EasyForNetTool.Parsing;

/// <summary>
/// Defines metadata about a CLI argument, including its type, names, description and available options.
/// </summary>
public class ArgumentInfo
{
    public ArgumentType Type { get; init; }
    public string Name { get; init; } = null!;
    public string ShortName { get; init; } = null!;
    public string Description { get; init; } = null!;
    public IList<ArgumentOption> Options { get; init; } = null!;

    /// <summary>
    /// Returns the list of all supported CLI arguments with their options.
    /// </summary>
    /// <returns>A list of <see cref="ArgumentInfo"/> instances describing each command.</returns>
    public static IList<ArgumentInfo> Arguments()
    {
        return
        [
            new ArgumentInfo
            {
                Type = ArgumentType.CreateProject,
                Name = "createproject",
                ShortName = "cp",
                Description = "Create a new project.",
                Options =
                [
                    new ArgumentOption
                    {
                        Name = "--name",
                        ShortName = "-n",
                        Description = "Name of project.",
                        Required = true,
                    },
                    new ArgumentOption
                    {
                        Name = "--output",
                        ShortName = "-o",
                        Description = "Path of project.",
                        Required = false,
                    },
                    new ArgumentOption
                    {
                        Name = "--multilanguage",
                        ShortName = "-m",
                        PropertyName = nameof(CreateProjectArgument.MultiLanguage),
                        Description = "Enable multi-language support.",
                        Required = false,
                        Default = "false",
                    },
                ]
            },
        ];
    }
}

/// <summary>
/// Represents a single option (flag) for a CLI argument, including its names, description and constraints.
/// </summary>
public class ArgumentOption
{
    public string Name { get; init; } = null!;
    public string ShortName { get; init; } = null!;
    public string Description { get; init; } = null!;
    public bool Required { get; init; }
    public string Default { get; set; } = null!;
    public bool IsInternal { get; set; }
    public Func<string, string>? NormalizeMethod { get; set; } = null!;
    /// <summary>
    /// The argument property the option sets, when it is not the PascalCase form of <see cref="Name"/>.
    /// </summary>
    public string? PropertyName { get; init; }
}
