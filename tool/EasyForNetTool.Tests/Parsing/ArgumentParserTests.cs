namespace EasyForNetTool.Tests.Parsing;

using EasyForNetTool.Parsing;

/// <summary>
/// Unit tests for <see cref="ArgumentParser"/> parsing the createproject options.
/// </summary>
public class ArgumentParserTests
{
    /// <summary>
    /// Tests that <c>-m</c> / <c>--multilanguage</c> sets <see cref="CreateProjectArgument.MultiLanguage"/>.
    /// </summary>
    [Theory]
    [InlineData("-m", "true", true)]
    [InlineData("--multilanguage", "True", true)]
    [InlineData("-m", "false", false)]
    public void Should_Parse_MultiLanguage(string option, string value, bool expected)
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo", option, value]);

        Assert.Equal(expected, argument.MultiLanguage);
    }

    /// <summary>
    /// Tests that multi-language defaults to off when the option is not given.
    /// </summary>
    [Fact]
    public void Should_Default_MultiLanguage_To_False()
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo"]);

        Assert.False(argument.MultiLanguage);
        Assert.Equal("Demo", argument.Name);
    }

    /// <summary>
    /// Tests that a value that is not a boolean is refused with a readable message.
    /// </summary>
    [Fact]
    public void Should_Refuse_A_Non_Boolean_MultiLanguage()
    {
        Assert.Throws<UserFriendlyException>(() => new ArgumentParser().Parse(["cp", "-n", "demo", "-m", "nope"]));
    }

    /// <summary>
    /// Tests that <c>-e</c> / <c>--examples</c> sets <see cref="CreateProjectArgument.IncludeExamples"/>.
    /// </summary>
    [Theory]
    [InlineData("-e", "true", true)]
    [InlineData("--examples", "True", true)]
    [InlineData("-e", "false", false)]
    public void Should_Parse_IncludeExamples(string option, string value, bool expected)
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo", option, value]);

        Assert.Equal(expected, argument.IncludeExamples);
    }

    /// <summary>
    /// Tests that the component example pages are left out when the option is not given.
    /// </summary>
    [Fact]
    public void Should_Default_IncludeExamples_To_False()
    {
        var argument = (CreateProjectArgument)new ArgumentParser().Parse(["cp", "-n", "demo", "-m", "true"]);

        Assert.False(argument.IncludeExamples);
        Assert.True(argument.MultiLanguage);
    }

    /// <summary>
    /// Tests that a value that is not a boolean is refused with a readable message.
    /// </summary>
    [Fact]
    public void Should_Refuse_A_Non_Boolean_IncludeExamples()
    {
        Assert.Throws<UserFriendlyException>(() => new ArgumentParser().Parse(["cp", "-n", "demo", "-e", "nope"]));
    }
}
