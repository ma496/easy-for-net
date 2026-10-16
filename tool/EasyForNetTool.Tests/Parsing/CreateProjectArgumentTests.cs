using EasyForNetTool.Parsing;

namespace EasyForNetTool.Tests.Parsing;

/// <summary>
/// Unit tests for the <see cref="CreateProjectArgument"/> class, focusing on name validation and normalization.
/// </summary>
public class CreateProjectArgumentTests
{
    /// <summary>
    /// Tests that valid project names are correctly normalized to PascalCase.
    /// </summary>
    [Fact]
    public void CreateProjectArgument_Name_Valid()
    {
        var argument = new CreateProjectArgument
        {
            Name = "validName"
        };
        Assert.Equal("ValidName", argument.Name);

        argument.Name = "valid-name";
        Assert.Equal("ValidName", argument.Name);

        argument.Name = "valid_name";
        Assert.Equal("ValidName", argument.Name);
    }

    /// <summary>
    /// Tests that invalid project names throw a <see cref="UserFriendlyException"/>.
    /// </summary>
    [Fact]
    public void CreateProjectArgument_Name_Invalid()
    {
        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "invalid name"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "-invalid-name"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "invalid-name-"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "invalid-name_"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "_invalid-name"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "1invalid-name"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "invalid-name1"
        });

        Assert.Throws<UserFriendlyException>(() => new CreateProjectArgument
        {
            Name = "invalid*name"
        });
    }
}
