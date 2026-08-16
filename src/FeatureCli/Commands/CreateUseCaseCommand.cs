using System.ComponentModel;
using FeatureCli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FeatureCli.Commands;

public class CreateUseCaseCommand : Command<CreateUseCaseCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Nombre del caso de uso (ej. CancelOrder, GetOrderById, UpdateOrderStatus)")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("-f|--feature <FEATURE>")]
        [Description("Nombre del feature al que pertenecerá el caso de uso (ej. Orders, Invoices)")]
        public string Feature { get; set; } = string.Empty;

        [CommandOption("-m|--method <METHOD>")]
        [Description("Método HTTP del endpoint (GET, POST, PUT, DELETE, PATCH)")]
        public string Method { get; set; } = string.Empty;

        [CommandOption("-p|--project-path <PATH>")]
        [Description("Ruta al proyecto o directorio raíz (opcional, detección automática por defecto)")]
        public string? ProjectPath { get; set; }

        [CommandOption("-e |--entity <ENTITY>")]
        [Description("Nombre de la entidad de dominio asociada (opcional, inferida automáticamente)")]
        public string? Entity { get; set; }

        [CommandOption("--force")]
        [Description("Sobrescribir archivo si ya existe")]
        public bool Force { get; set; }

        [CommandOption("--wv|--withValidation")]
        [Description("Incluir validador FluentValidation en el caso de uso")]
        public bool WithValidation { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("El nombre del caso de uso (-n|--name) es obligatorio.");

            if (string.IsNullOrWhiteSpace(Feature))
                return ValidationResult.Error("El nombre del feature (-f|--feature) es obligatorio.");

            if (string.IsNullOrWhiteSpace(Method))
                return ValidationResult.Error("El método HTTP (-m|--method) es obligatorio. (Valores: GET, POST, PUT, DELETE, PATCH).");

            var methodUpper = Method.Trim().ToUpperInvariant();
            string[] validMethods = ["GET", "POST", "PUT", "DELETE", "PATCH"];
            if (!validMethods.Contains(methodUpper))
            {
                return ValidationResult.Error($"Método HTTP inválido: '{Method}'. Debe ser uno de: GET, POST, PUT, DELETE, PATCH.");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            var projectInfo = ProjectLocator.Locate(settings.ProjectPath);
            var featureName = NamingHelper.ToPascalCase(settings.Feature);
            var useCaseName = NamingHelper.ToPascalCase(settings.Name);
            var methodUpper = settings.Method.Trim().ToUpperInvariant();

            var featureDir = Path.Combine(projectInfo.FeaturesDirectory, featureName);
            if (!Directory.Exists(featureDir))
            {
                AnsiConsole.MarkupLine($"[red bold]Error:[/] El feature [yellow]'{featureName}'[/] no existe en [dim]{projectInfo.FeaturesDirectory}[/].");
                AnsiConsole.MarkupLine($"[dim]Tip: Puedes crearlo primero con:[/] [green]feature create -n {featureName}[/]");
                return 1;
            }

            var entityName = !string.IsNullOrWhiteSpace(settings.Entity)
                ? NamingHelper.ToPascalCase(settings.Entity)
                : NamingHelper.ToSingular(featureName);

            List<PropertyInfoModel> domainProperties = [];

            if (!string.IsNullOrWhiteSpace(settings.Entity))
            {
                var domainFilePath = Path.Combine(featureDir, "Domain", $"{entityName}.cs");
                if (!File.Exists(domainFilePath))
                {
                    AnsiConsole.MarkupLine($"[yellow]Aviso:[/] La entidad de dominio [cyan]'{entityName}'[/] no existe en [dim]{domainFilePath}[/].");
                    return 1;
                }

                var sourceCode = File.ReadAllText(domainFilePath);
                domainProperties = EntityInspector.ExtractProperties(sourceCode);
            }

            List<PropertyInfoModel> selectedRequestProperties = [];
            if (domainProperties.Count > 0)
            {
                var prompt = new MultiSelectionPrompt<PropertyInfoModel>()
                    .Title($"Selecciona las propiedades de [yellow]{entityName}[/] para incluir en el [green]Request[/]:")
                    .NotRequired()
                    .PageSize(10)
                    .InstructionsText("[grey](Presiona [blue]<espacio>[/] para seleccionar, [green]<enter>[/] para confirmar)[/]")
                    .UseConverter(p => $"{p.Name} [grey]({p.Type})[/]")
                    .AddChoices(domainProperties);

                selectedRequestProperties = AnsiConsole.Prompt(prompt);
            }

            var useCaseFilePath = Path.Combine(featureDir, $"{useCaseName}.cs");
            if (File.Exists(useCaseFilePath) && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]Aviso:[/] El caso de uso ya existe: [bold]{useCaseFilePath}[/]");
                if (!AnsiConsole.Confirm("¿Deseas sobrescribir el archivo?"))
                {
                    return 0;
                }
            }

            var request = new ScaffoldUseCaseRequest(
                projectInfo,
                featureName,
                useCaseName,
                methodUpper,
                entityName,
                selectedRequestProperties,
                settings.Force,
                settings.WithValidation);

            var result = UseCaseScaffolder.Execute(request);

            var tree = new Tree($"[green bold]Caso de uso '{useCaseName}' agregado a '{featureName}'[/]");
            var fNode = tree.AddNode($"[blue]Features/{featureName}[/]");
            fNode.AddNode($"[green]+ {useCaseName}.cs[/] [dim](Caso de uso y Handler)[/]");

            if (result.EndpointAdded)
            {
                fNode.AddNode($"[yellow]~ Endpoints.cs[/] [dim](Ruta: {result.HttpMethod} {result.FormattedRoute})[/]");
            }
            else
            {
                fNode.AddNode("[dim]~ Endpoints.cs (ya contenía el endpoint)[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(tree);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[green]✔[/] Verbo HTTP: [cyan]{result.HttpMethod}[/]");
            AnsiConsole.MarkupLine($"[green]✔[/] Ruta: [cyan]{result.FormattedRoute}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red bold]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
