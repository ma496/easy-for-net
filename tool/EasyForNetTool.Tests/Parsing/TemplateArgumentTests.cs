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
    /// Tests that each template leaves out the skills about what only the other one has: tenancy and
    /// plans for the user-facing template, public SEO pages for the multi-tenant one.
    /// </summary>
    [Fact]
    public void Each_Template_Excludes_The_Other_Templates_Skills()
    {
        Assert.Equal(["multi-tenancy", "feature-management"], ProjectTemplate.UserFacing.ExcludedSkills);
        Assert.Equal(["seo"], ProjectTemplate.MultiTenant.ExcludedSkills);
    }

    /// <summary>
    /// Tests that every excluded skill names a real directory under <c>.claude/skills</c>, so a rename
    /// fails here rather than shipping the skill silently to the template that should not have it.
    /// </summary>
    [Fact]
    public void Excluded_Skills_Exist()
    {
        var root = Path.GetDirectoryName(Generator.CustomizeAppSettingsTests.FindTemplateFile("agentic.config.json"))!;

        foreach (var skill in ProjectTemplate.All.SelectMany(t => t.ExcludedSkills))
            Assert.True(File.Exists(Path.Combine(root, ".claude", "skills", skill, "SKILL.md")), $"{skill} has no SKILL.md.");
    }
}
