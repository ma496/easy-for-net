using EasyForNetTool.Extensions;

namespace EasyForNetTool.Tests.Extensions;

/// <summary>
/// Unit tests for the <see cref="StringExt"/> extension methods.
/// </summary>
public class StringExtTests
{
    /// <summary>
    /// Tests that <see cref="StringExt.ToCamelCase"/> correctly converts various inputs to camelCase.
    /// </summary>
    [Fact]
    public void ToCamelCase()
    {
        Assert.Equal("camelCase", "camelCase".ToCamelCase());
        Assert.Equal("camelCase", "CamelCase".ToCamelCase());
        Assert.Equal("camel-Case", "camel-Case".ToCamelCase());
        Assert.Equal("camel_Case", "camel_Case".ToCamelCase());
        Assert.Equal("camel Case", "camel Case".ToCamelCase());
    }

    /// <summary>
    /// Tests that <see cref="StringExt.ToPascalCase"/> correctly converts various inputs to PascalCase.
    /// </summary>
    [Fact]
    public void ToPascalCase()
    {
        Assert.Equal("PascalCase", "pascalCase".ToPascalCase());
        Assert.Equal("PascalCase", "PascalCase".ToPascalCase());
        Assert.Equal("Pascal-case", "pascal-case".ToPascalCase());
        Assert.Equal("Pascal_Case", "pascal_Case".ToPascalCase());
        Assert.Equal("Pascal Case", "pascal Case".ToPascalCase());
    }

    /// <summary>
    /// Tests that <see cref="StringExt.ToKebabCase"/> correctly converts various inputs to kebab-case.
    /// </summary>
    [Fact]
    public void ToKebabCase()
    {
        Assert.Equal("kebab-case", "kebab-case".ToKebabCase());
        Assert.Equal("kebab-case", "KebabCase".ToKebabCase());
        Assert.Equal("kebab-case", "kebabCase".ToKebabCase());
    }
}
