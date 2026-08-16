using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using FeatureCli.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace FeatureCli.Templates;

public static class TemplateEngine
{
    private static readonly ConcurrentDictionary<string, Template> TemplateCache = new();
    private static readonly Assembly CurrentAssembly = typeof(TemplateEngine).Assembly;

    public static string GenerateDomainEntity(string rootNamespace, string featureName, string entityName)
    {
        var template = GetTemplate("DomainEntity");
        var rendered = template.Render(new
        {
            RootNamespace = rootNamespace,
            FeatureName = featureName,
            EntityName = entityName
        });

        return CodeValidatorAndFormatter.ValidateAndFormat(rendered);
    }

    public static string GenerateContractEvent(string rootNamespace, string featureName, string entityName, string? eventName = null)
    {
        eventName ??= $"{entityName}Created";
        var template = GetTemplate("ContractEvent");
        var rendered = template.Render(new
        {
            RootNamespace = rootNamespace,
            FeatureName = featureName,
            EntityName = entityName,
            EventName = eventName
        });

        return CodeValidatorAndFormatter.ValidateAndFormat(rendered);
    }

    public static string GenerateEndpoints(
        string rootNamespace,
        string featureName,
        string entityName,
        string initialUseCase,
        string initialMethod = "POST",
        bool hasValidation = true)
    {
        var routePrefix = "/" + NamingHelper.ToKebabCase(featureName);
        var mappingLine = GenerateEndpointMappingLine(initialUseCase, initialMethod, hasValidation: hasValidation);

        var template = GetTemplate("Endpoints");
        var rendered = template.Render(new
        {
            RootNamespace = rootNamespace,
            FeatureName = featureName,
            RoutePrefix = routePrefix,
            MappingLine = mappingLine
        });

        return CodeValidatorAndFormatter.ValidateAndFormat(rendered);
    }

    public static string GenerateInitialCreateUseCase(
        string rootNamespace,
        string featureName,
        string entityName,
        string style = "instance")
    {
        var useCaseName = $"Create{entityName}";
        var eventName = $"{entityName}Created";
        var routeFeatureKebab = NamingHelper.ToKebabCase(featureName);

        var templateName = "InitialCreateUseCaseInstance";
        var template = GetTemplate(templateName);
        var rendered = template.Render(new
        {
            RootNamespace = rootNamespace,
            FeatureName = featureName,
            EntityName = entityName,
            UseCaseName = useCaseName,
            EventName = eventName,
            RouteFeatureKebab = routeFeatureKebab
        });

        return CodeValidatorAndFormatter.ValidateAndFormat(rendered);
    }

    public static string GenerateUseCase(
        string rootNamespace,
        string featureName,
        string entityName,
        string useCaseName,
        string httpMethod,
        string? style = null,
        IEnumerable<PropertyInfoModel>? requestProperties = null,
        bool withValidation = false)
    {
        var methodUpper = httpMethod.ToUpperInvariant();

        var reqProps = requestProperties?
            .Select(p => new { Name = p.Name, Type = p.Type, IsNullable = p.IsNullable })
            .ToList() ?? [];

        var templateName = "UseCaseInstance";
        var template = GetTemplate(templateName);
        var rendered = template.Render(new
        {
            RootNamespace = rootNamespace,
            FeatureName = featureName,
            EntityName = entityName,
            UseCaseName = useCaseName,
            MethodUpper = methodUpper,
            RequestProperties = reqProps,
            WithValidation = withValidation
        });

        return CodeValidatorAndFormatter.ValidateAndFormat(rendered);
    }

    public static ExpressionStatementSyntax GenerateEndpointMappingStatement(
        string useCaseName,
        string httpMethod,
        bool hasValidation)
    {
        var methodPascal = NamingHelper.FormatHttpMethod(httpMethod);
        var route = "/" + NamingHelper.ToKebabCase(useCaseName);
        var handlerName = $"{useCaseName}Handler";
        var requestType = QualifiedName(IdentifierName(useCaseName), IdentifierName("Request"));

        ParameterSyntax[] parameters =
        [
            Parameter(Identifier("request")).WithType(requestType),
            Parameter(Identifier("handler")).WithType(IdentifierName(handlerName)),
            Parameter(Identifier("ct")).WithType(IdentifierName("CancellationToken"))
        ];

        ArgumentSyntax[] handleArguments =
        [
            Argument(IdentifierName("request")),
            Argument(IdentifierName("ct"))
        ];

        var handleInvocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("handler"),
                IdentifierName("Handle")),
            ArgumentList(SeparatedList(handleArguments)));

        var lambda = ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(handleInvocation);

        var mapMemberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("group"),
            IdentifierName($"Map{methodPascal}"));

        var mapInvocation = InvocationExpression(
            mapMemberAccess,
            ArgumentList(SeparatedList(new[]
            {
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(route))),
                Argument(lambda)
            })));

        var nameofInvocation = InvocationExpression(
            IdentifierName("nameof"),
            ArgumentList(SingletonSeparatedList(Argument(IdentifierName(useCaseName)))));

        var withNameMemberAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            mapInvocation,
            IdentifierName("WithName"));

        var withNameInvocation = InvocationExpression(
            withNameMemberAccess,
            ArgumentList(SingletonSeparatedList(Argument(nameofInvocation))));

        ExpressionSyntax currentExpression = withNameInvocation;

        if (hasValidation)
        {
            var withValidationMemberAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                currentExpression,
                GenericName(Identifier("WithValidation"))
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(requestType))));

            currentExpression = InvocationExpression(
                withValidationMemberAccess,
                ArgumentList());
        }

        var normalized = ExpressionStatement(currentExpression).NormalizeWhitespace();
        return (ExpressionStatementSyntax)new CodeValidatorAndFormatter.EndpointsFormatRewriter().Visit(normalized);
    }

    public static string GenerateEndpointMappingLine(
        string useCaseName,
        string httpMethod,
        bool hasValidation)
    {
        return GenerateEndpointMappingStatement(useCaseName, httpMethod, hasValidation)
            .ToFullString();
    }

    private static Template GetTemplate(string templateName)
    {
        return TemplateCache.GetOrAdd(templateName, name =>
        {
            var content = LoadTemplateContent(name);
            var template = Template.Parse(content);
            if (template.HasErrors)
            {
                var errors = string.Join(Environment.NewLine, template.Messages.Select(m => m.ToString()));
                throw new InvalidOperationException($"Error al parsear la plantilla Scriban '{name}':{Environment.NewLine}{errors}");
            }
            return template;
        });
    }

    private static string LoadTemplateContent(string templateName)
    {
        var resourceName = $"FeatureCli.Templates.Files.{templateName}.scriban";
        using var stream = CurrentAssembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var foundName = CurrentAssembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($"{templateName}.scriban", StringComparison.OrdinalIgnoreCase));

            if (foundName != null)
            {
                using var fallbackStream = CurrentAssembly.GetManifestResourceStream(foundName)!;
                using var reader = new StreamReader(fallbackStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }

            var available = string.Join(", ", CurrentAssembly.GetManifestResourceNames());
            throw new FileNotFoundException($"No se encontró el recurso embebido para la plantilla '{templateName}'. Recursos disponibles: [{available}]");
        }

        using var streamReader = new StreamReader(stream, Encoding.UTF8);
        return streamReader.ReadToEnd();
    }
}
