using System.ComponentModel;
using FeatureCli.Services;
using FeatureCli.Templates;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FeatureCli.Commands;

public class CreateFeatureCommand : Command<CreateFeatureCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Nombre del feature (ej. Orders, Invoices, Customers)")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("-p|--project-path <PATH>")]
        [Description("Ruta al directorio del proyecto (ej. /src/<projectName>) sin indicar el .csproj (opcional, detección automática por defecto)")]
        public string? ProjectPath { get; set; }


        [CommandOption("--entity <ENTITY>")]
        [Description("Nombre de la entidad de dominio (por defecto: singular del nombre del feature)")]
        public string? Entity { get; set; }

        [CommandOption("--force")]
        [Description("Sobrescribir archivos si ya existen")]
        public bool Force { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Error("El nombre del feature (-n|--name) es obligatorio.");

            if (!string.IsNullOrWhiteSpace(ProjectPath))
            {
                var trimmedPath = ProjectPath.Trim();

                if (trimmedPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Error("La ruta del proyecto (-p|--project-path) debe ser el directorio donde está el proyecto (ej. /src/<projectName>), sin indicar el archivo .csproj.");

                if (!Directory.Exists(trimmedPath))
                    return ValidationResult.Error($"El directorio especificado en (-p|--project-path) no existe: '{trimmedPath}'.");

                var csprojs = Directory.GetFiles(trimmedPath, "*.csproj", SearchOption.AllDirectories)
                    .Where(f => !ProjectLocator.IsCliOrTestProject(Path.GetFileName(f)))
                    .ToArray();

                if (csprojs.Length == 0)
                    return ValidationResult.Error($"No se encontró ningún archivo .csproj en el directorio especificado: '{trimmedPath}'.");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            var projectInfo = ProjectLocator.Locate(settings.ProjectPath);
            var featureName = NamingHelper.ToPascalCase(settings.Name);
            var entityName = !string.IsNullOrWhiteSpace(settings.Entity)
                ? NamingHelper.ToPascalCase(settings.Entity)
                : NamingHelper.ToSingular(featureName);

            var featureDir = Path.Combine(projectInfo.FeaturesDirectory, featureName);
            var domainDir = Path.Combine(featureDir, "Domain");

            if (Directory.Exists(featureDir) && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]Aviso:[/] La carpeta del feature ya existe: [bold]{featureDir}[/]");
                if (!AnsiConsole.Confirm("¿Deseas continuar y sobrescribir/crear archivos faltantes?"))
                {
                    return 0;
                }
            }

            Directory.CreateDirectory(featureDir);
            Directory.CreateDirectory(domainDir);

            var entityPath = Path.Combine(domainDir, $"{entityName}.cs");
            var endpointsPath = Path.Combine(featureDir, "Endpoints.cs");
            var useCasePath = Path.Combine(featureDir, $"Create{entityName}.cs");

            var entityContent = TemplateEngine.GenerateDomainEntity(projectInfo.RootNamespace, featureName, entityName);
            var contractContent = TemplateEngine.GenerateContractEvent(projectInfo.RootNamespace, featureName, entityName);
            var endpointsContent = TemplateEngine.GenerateEndpoints(
                projectInfo.RootNamespace,
                featureName,
                entityName,
                $"Create{entityName}",
                "POST");
            var useCaseContent = TemplateEngine.GenerateInitialCreateUseCase(
                projectInfo.RootNamespace,
                featureName,
                entityName);

            File.WriteAllText(entityPath, entityContent);
            File.WriteAllText(endpointsPath, endpointsContent);
            File.WriteAllText(useCasePath, useCaseContent);

            var tree = new Tree($"[green bold]Feature '{featureName}' creado exitosamente[/]");
            var fNode = tree.AddNode($"[blue]Features/{featureName}[/]");
            var dNode = fNode.AddNode("[yellow]Domain[/]");
            dNode.AddNode($"[white]{entityName}.cs[/]");
            var cNode = fNode.AddNode("[yellow]Contracts[/]");
            cNode.AddNode($"[white]{entityName}Created.cs[/]");
            fNode.AddNode("[white]Endpoints.cs[/]");
            fNode.AddNode($"[white]Create{entityName}.cs[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.Write(tree);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[green]✔[/] Proyecto destino: [dim]{projectInfo.ProjectDirectory}[/]");
            AnsiConsole.MarkupLine($"[green]✔[/] Namespace base: [dim]{projectInfo.RootNamespace}[/]");
            AnsiConsole.MarkupLine($"[green]✔[/] Ruta Minimal API: [cyan]/{NamingHelper.ToKebabCase(featureName)}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red bold]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
