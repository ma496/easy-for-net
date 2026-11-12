using EasyForNetTool.Generator;
using EasyForNetTool.Parsing;

namespace EasyForNetTool.Tests.Parsing;

/// <summary>
/// Unit tests for the <c>-t</c> / <c>--template</c> option of the createproject command.
/// </summary>
public class TemplateArgumentTests
{
    /// <summary>
    /// Tests that a project is multi-tenant when no template is named.
    /// </summary>
    [Fact]
    public void Should_Default_To_MultiTenant()
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo"]);

        Assert.Equal("multi-tenant", argument.Template);
        Assert.Same(ProjectTemplate.MultiTenant, argument.SelectedTemplate);
    }

    /// <summary>
    /// Tests that either spelling of the option selects a template, ignoring case.
    /// </summary>
    [Theory]
    [InlineData("-t", "user-facing", "user-facing")]
    [InlineData("--template", "User-Facing", "user-facing")]
    [InlineData("-t", "multi-tenant", "multi-tenant")]
    public void Should_Parse_Template(string option, string value, string expected)
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo", option, value]);

        Assert.Equal(expected, argument.Template);
    }

    /// <summary>
    /// Tests that an unknown template is refused with a message naming the known ones.
    /// </summary>
    [Fact]
    public void Should_Refuse_An_Unknown_Template()
    {
        var ex = Assert.Throws<UserFriendlyException>(() => new ArgumentParser().Parse(["cp", "-n", "demo", "-t", "single"]));

        Assert.Contains("user-facing", ex.Message);
    }

    /// <summary>
    /// Tests that the user-facing template leaves out the skills about tenancy and plans.
    /// </summary>
    [Fact]
    public void UserFacing_Excludes_Tenancy_Skills()
    {
        Assert.Contains("multi-tenancy", ProjectTemplate.UserFacing.ExcludedSkills);
        Assert.Contains("feature-management", ProjectTemplate.UserFacing.ExcludedSkills);
        Assert.Empty(ProjectTemplate.MultiTenant.ExcludedSkills);
    }
}
