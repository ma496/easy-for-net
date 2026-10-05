namespace EasyForNetTool.Tests.Generator;

using EasyForNetTool.Generator;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Unit tests for the <see cref="NamespaceRewriter"/> syntax rewriter.
/// </summary>
public class NamespaceRewriterTests
{
    /// <summary>
    /// Tests that namespace declarations, using statements, and qualified names are rewritten,
    /// but local variables with the same name as the old root remain unchanged.
    /// </summary>
    [Fact]
    public void Should_Rewrite_Namespace_Declaration_And_Using_Statements()
    {
        // Arrange
        // Use single-part root to ensure IdentifierName visitor triggers on the first token in expressions
        var oldNs = "Old";
        var newNs = "New";
        var sourceCode = @"
using Old.Namespace.Helper;
using System;

namespace Old.Namespace.MyFeature;

public class MyClass
{
    public void Method()
    {
        var Old = ""Old"";
        Old.Namespace.Helper.DoSomething();
    }
}
";

        var rewriter = new NamespaceRewriter(oldNs, newNs);
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // Act
        var result = rewriter.Visit(root);
        var resultCode = result.ToFullString();

        // Assert
        // Namespace declaration SHOULD change
        Assert.Contains("namespace New.Namespace.MyFeature;", resultCode);

        // Using statement SHOULD change
        Assert.Contains("using New.Namespace.Helper;", resultCode);

        // Code body SHOULD change
        Assert.Contains("New.Namespace.Helper.DoSomething();", resultCode);

        // Old variable name SHOULD NOT change
        Assert.Contains("var Old = \"Old\";", resultCode);
    }

    /// <summary>
    /// Tests that namespaces named in comments are rewritten: <c>cref</c> attributes in XML
    /// documentation (which otherwise fail to resolve and raise CS1574), XML doc prose and
    /// ordinary comments, while words that merely start with the old root are left alone.
    /// </summary>
    [Fact]
    public void Should_Rewrite_Namespaces_In_Comments()
    {
        // Arrange
        var sourceCode = @"
namespace Backend.Features.Users;

/// <summary>
/// Seeded by <see cref=""Backend.ShareData.DataSeeder""/> and <see cref=""Backend.Permissions""/>;
/// see <c>Backend.Features.Identity</c>. Not BackendTools.
/// </summary>
/// <param name=""x"">From <see cref=""Backend.Permissions.Allow.Tenant_Detail""/>.</param>
public class MyClass
{
    // Mirrors Backend.Features.Tenancy, not MyBackend.Thing.
    /* Also Backend.ShareData. */
    public void Method(int x) { }
}
";

        var rewriter = new NamespaceRewriter("Backend", "Acme");
        var root = CSharpSyntaxTree.ParseText(sourceCode).GetRoot();

        // Act
        var resultCode = rewriter.Visit(root).ToFullString();

        // Assert
        Assert.Contains("namespace Acme.Features.Users;", resultCode);
        Assert.Contains(@"<see cref=""Acme.ShareData.DataSeeder""/>", resultCode);
        Assert.Contains(@"<see cref=""Acme.Permissions""/>", resultCode);
        Assert.Contains(@"<see cref=""Acme.Permissions.Allow.Tenant_Detail""/>", resultCode);
        Assert.Contains("<c>Acme.Features.Identity</c>. Not BackendTools.", resultCode);
        Assert.Contains("// Mirrors Acme.Features.Tenancy, not MyBackend.Thing.", resultCode);
        Assert.Contains("/* Also Acme.ShareData. */", resultCode);
        Assert.DoesNotContain(" Backend.", resultCode);
        Assert.DoesNotContain("\"Backend.", resultCode);
        Assert.DoesNotContain(">Backend.", resultCode);
    }
}
