using FeatureCli.Templates;

namespace FeatureCli.Services;

public record ScaffoldUseCaseRequest(
    ProjectInfo ProjectInfo,
    string FeatureName,
    string UseCaseName,
    string HttpMethod,
    string EntityName,
    IReadOnlyList<PropertyInfoModel> SelectedProperties,
    bool Force = false,
    bool WithValidation = false);

public record ScaffoldUseCaseResult(
    string UseCaseFilePath,
    string EndpointsFilePath,
    bool UseCaseCreated,
    bool EndpointAdded,
    string HttpMethod,
    string FormattedRoute);

public static class UseCaseScaffolder
{
    private static void GenerateUseCase(ScaffoldUseCaseRequest request, string useCaseFilePath)
    {
        var useCaseContent = TemplateEngine.GenerateUseCase(
            request.ProjectInfo.RootNamespace,
            request.FeatureName,
            request.EntityName,
            request.UseCaseName,
            request.HttpMethod,
            requestProperties: request.SelectedProperties,
            withValidation: request.WithValidation);

        File.WriteAllText(useCaseFilePath, useCaseContent);
    }

    public static void UpdateOrCreateEndpoints(ScaffoldUseCaseRequest request, string endpointsFilePath, out bool endpointAdded)
    {
        var hasValidation = request.WithValidation;
        endpointAdded = false;
        if (File.Exists(endpointsFilePath))
        {
            endpointAdded = EndpointModifier.AddEndpointMapping(
                endpointsFilePath,
                request.UseCaseName,
                request.HttpMethod,
                hasValidation);
        }
        else
        {
            var endpointsContent = TemplateEngine.GenerateEndpoints(
                request.ProjectInfo.RootNamespace,
                request.FeatureName,
                request.EntityName,
                request.UseCaseName,
                request.HttpMethod,
                hasValidation: hasValidation);
            File.WriteAllText(endpointsFilePath, endpointsContent);
            endpointAdded = true;
        }
    }

    public static ScaffoldUseCaseResult Execute(ScaffoldUseCaseRequest request)
    {
        var featureDir = Path.Combine(request.ProjectInfo.FeaturesDirectory, request.FeatureName);
        var useCaseFilePath = Path.Combine(featureDir, $"{request.UseCaseName}.cs");
        var endpointsFilePath = Path.Combine(featureDir, "Endpoints.cs");
        
        GenerateUseCase(request, useCaseFilePath);
        UpdateOrCreateEndpoints(request, endpointsFilePath, out bool endpointAdded);
        var formattedRoute = $"/{NamingHelper.ToKebabCase(request.FeatureName)}/{NamingHelper.ToKebabCase(request.UseCaseName)}";

        return new ScaffoldUseCaseResult(
            UseCaseFilePath: useCaseFilePath,
            EndpointsFilePath: endpointsFilePath,
            UseCaseCreated: true,
            EndpointAdded: endpointAdded,
            HttpMethod: request.HttpMethod,
            FormattedRoute: formattedRoute);
    }
}