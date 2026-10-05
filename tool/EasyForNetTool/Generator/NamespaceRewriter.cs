namespace EasyForNetTool.Generator;

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// A Roslyn syntax rewriter that replaces occurrences of an old root namespace with a new one
/// in qualified names and identifier names within C# syntax trees, including the <c>cref</c>
/// attributes of XML documentation comments and namespace mentions in comment text.
/// </summary>
public class NamespaceRewriter(string oldRoot, string newRoot) : CSharpSyntaxRewriter(visitIntoStructuredTrivia: true)
{
    private readonly string _old = oldRoot;
    private readonly string _new = newRoot;

    /// <summary>
    /// Matches the old root namespace as a whole word followed by a dot, the shape a qualified
    /// mention takes in comment text (<c>Backend.Features</c>, but not <c>BackendTests</c>).
    /// </summary>
    private readonly Regex _commentPattern = new($@"(?<![\w.]){Regex.Escape(oldRoot)}(?=\.)");

    /// <summary>
    /// Replaces the old root namespace prefix with the new one if the string starts with the old root.
    /// </summary>
    private string ReplaceNs(string ns)
    {
        return ns.StartsWith(_old) ? ns.Replace(_old, _new) : ns;
    }

    /// <summary>
    /// Replaces every qualified mention of the old root namespace in free comment text.
    /// </summary>
    private string ReplaceInText(string text)
    {
        return _commentPattern.Replace(text, _new);
    }

    /// <summary>
    /// Visits a qualified name node and rewrites it if it starts with the old root namespace.
    /// </summary>
    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        var full = node.ToString();
        if (full.StartsWith(_old))
            return SyntaxFactory.ParseName(ReplaceNs(full)).WithTriviaFrom(node);

        return base.VisitQualifiedName(node);
    }

    /// <summary>
    /// Visits an identifier name node and rewrites it if it starts with the old root namespace.
    /// </summary>
    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var text = node.ToString();
        if (text.StartsWith(_old))
            return SyntaxFactory.ParseName(ReplaceNs(text)).WithTriviaFrom(node);

        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Rewrites namespace mentions in <c>//</c> and <c>/* */</c> comments.
    /// </summary>
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
    {
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            var text = trivia.ToFullString();
            var replaced = ReplaceInText(text);
            if (replaced != text)
                return SyntaxFactory.Comment(replaced);
        }

        return base.VisitTrivia(trivia);
    }

    /// <summary>
    /// Rewrites namespace mentions in the prose of XML documentation comments
    /// (for example inside <c>&lt;c&gt;</c> elements).
    /// </summary>
    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
        {
            var replaced = ReplaceInText(token.Text);
            if (replaced != token.Text)
            {
                token = SyntaxFactory.XmlTextLiteral(
                    token.LeadingTrivia,
                    replaced,
                    ReplaceInText(token.ValueText),
                    token.TrailingTrivia);
            }
        }

        return base.VisitToken(token);
    }
}
