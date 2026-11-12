using System.Text.RegularExpressions;
using EasyForNetTool.Generator;

namespace EasyForNetTool.Parsing;

/// <summary>
/// Represents the parsed arguments for the "createproject" CLI command.
/// </summary>
public class CreateProjectArgument : Argument
{
    private string _name = null!;

    public string Name
    {
        get => _name;
        set
        {
            // start and end with alphabet
            // only allowed alphabets, -, _
            if (!Regex.IsMatch(value, @"^[a-zA-Z][a-zA-Z_\-]*[a-zA-Z]$"))
                throw new UserFriendlyException("Project name can only contain alphabets, -, _, must start and end with alphabet");
            // spilt by - and _, then capitalize first letter of each word
            _name = value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => char.ToUpper(x[0]) + x.Substring(1))
                .Aggregate((x, y) => x + y);
        }
    }
    public string Output { get; set; } = null!;
    public bool MultiLanguage { get; set; }
    public bool IncludeExamples { get; set; }

    private ProjectTemplate _template = ProjectTemplate.MultiTenant;

    /// <summary>
    /// The application template the project is generated from — <c>multi-tenant</c> (the default) or
    /// <c>user-facing</c>. Setting an unknown name throws.
    /// </summary>
    public string Template
    {
        get => _template.Name;
        set => _template = ProjectTemplate.Find(value);
    }

    /// <summary>The template <see cref="Template"/> names.</summary>
    public ProjectTemplate SelectedTemplate => _template;
}
