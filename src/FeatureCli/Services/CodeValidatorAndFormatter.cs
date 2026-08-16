using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FeatureCli.Services;

public static class CodeValidatorAndFormatter
{
    public static string ValidateAndFormat(string csharpCode)
    {
        if (string.IsNullOrWhiteSpace(csharpCode))
        {
            return string.Empty;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var errorDetails = string.Join(
                Environment.NewLine,
                errors.Select(e =>
                {
                    var lineSpan = e.Location.GetLineSpan();
                    return $"  - Línea {lineSpan.StartLinePosition.Line + 1}, Col {lineSpan.StartLinePosition.Character + 1}: {e.GetMessage()} ({e.Id})";
                }));

            throw new InvalidOperationException(
                $"Se detectaron errores de sintaxis en el código C# generado:{Environment.NewLine}{errorDetails}{Environment.NewLine}{Environment.NewLine}Código generado:{Environment.NewLine}{csharpCode}");
        }

        var root = syntaxTree.GetRoot().NormalizeWhitespace();
        var formattedRoot = new EndpointsFormatRewriter().Visit(root);
        return formattedRoot.ToFullString();
    }

    public sealed class EndpointsFormatRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var visitedBlock = (BlockSyntax)base.VisitBlock(node)!;

            if (node.Parent is MethodDeclarationSyntax method &&
                method.Identifier.Text == "Map" &&
                method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
                method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            {
                var newStatements = new List<StatementSyntax>();
                for (int i = 0; i < visitedBlock.Statements.Count; i++)
                {
                    var stmt = visitedBlock.Statements[i];
                    if (i > 0)
                    {
                        // Add exactly one blank line before subsequent statements (between var group and map methods, and between map methods)
                        var leadingTrivia = TriviaList(CarriageReturnLineFeed, Whitespace("        "));
                        stmt = stmt.WithLeadingTrivia(leadingTrivia);
                    }
                    else
                    {
                        var leadingTrivia = TriviaList(Whitespace("        "));
                        stmt = stmt.WithLeadingTrivia(leadingTrivia);
                    }
                    newStatements.Add(stmt);
                }

                var closeBraceTrivia = TriviaList(Whitespace("    "));
                return visitedBlock
                    .WithStatements(List(newStatements))
                    .WithCloseBraceToken(visitedBlock.CloseBraceToken.WithLeadingTrivia(closeBraceTrivia));
            }

            return visitedBlock;
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (node.Expression is InvocationExpressionSyntax)
            {
                var memberName = node.Name.Identifier.Text;
                if (memberName is "WithName" or "WithValidation")
                {
                    var leadingTrivia = TriviaList(CarriageReturnLineFeed, Whitespace("            "));
                    return visited.WithOperatorToken(visited.OperatorToken.WithLeadingTrivia(leadingTrivia));
                }
            }

            return visited;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var visited = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;

            if (node.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax parentInv } })
            {
                if (parentInv.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text.StartsWith("Map"))
                {
                    var leadingTrivia = TriviaList(CarriageReturnLineFeed, Whitespace("            "));
                    return visited.WithArrowToken(visited.ArrowToken.WithLeadingTrivia(leadingTrivia));
                }
            }

            return visited;
        }
    }
}
