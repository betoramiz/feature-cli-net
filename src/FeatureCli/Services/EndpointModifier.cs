using FeatureCli.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FeatureCli.Services;

public static class EndpointModifier
{
    public static bool AddEndpointMapping(
        string endpointsFilePath,
        string useCaseName,
        string httpMethod,
        bool hasValidation = true)
    {
        if (!File.Exists(endpointsFilePath))
        {
            throw new FileNotFoundException($"El archivo de endpoints no existe: {endpointsFilePath}");
        }

        var content = File.ReadAllText(endpointsFilePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(content);
        var root = syntaxTree.GetCompilationUnitRoot();

        // Locate public static void Map(IEndpointRouteBuilder ...)
        var mapMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m =>
                m.Identifier.Text == "Map" &&
                m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)) &&
                m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.StaticKeyword)) &&
                m.ParameterList.Parameters.Any(p => p.Type?.ToString().Contains("IEndpointRouteBuilder") == true));

        if (mapMethod?.Body is null)
        {
            throw new InvalidOperationException("No se encontró el método 'public static void Map(IEndpointRouteBuilder ...)' en Endpoints.cs");
        }

        // Check if endpoint is already registered in AST
        var alreadyExists = mapMethod.DescendantNodes()
            .Any(node =>
                (node is IdentifierNameSyntax id && id.Identifier.Text == useCaseName) ||
                (node is LiteralExpressionSyntax lit && lit.Token.ValueText == useCaseName));

        if (alreadyExists)
        {
            return false; // already exists
        }

        var statementSyntax = TemplateEngine.GenerateEndpointMappingStatement(useCaseName, httpMethod, hasValidation);

        var updatedBody = mapMethod.Body.AddStatements(statementSyntax);
        var updatedMapMethod = mapMethod.WithBody(updatedBody);
        var updatedRoot = root.ReplaceNode(mapMethod, updatedMapMethod);

        var formattedContent = CodeValidatorAndFormatter.ValidateAndFormat(updatedRoot.ToFullString());
        File.WriteAllText(endpointsFilePath, formattedContent);
        return true;
    }
}

