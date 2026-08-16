using FeatureCli.Services;
using FeatureCli.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FeatureCli.Tests;

public class FeatureCliTests
{
    [Theory]
    [InlineData("orders", "Orders")]
    [InlineData("invoices", "Invoices")]
    [InlineData("order-items", "OrderItems")]
    [InlineData("place_order", "PlaceOrder")]
    [InlineData("UserManagement", "UserManagement")]
    [InlineData("POST", "Post")]
    [InlineData("GET", "Get")]
    public void NamingHelper_ToPascalCase_formats_correctly(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPascalCase(input));
    }

    [Theory]
    [InlineData("Orders", "Order")]
    [InlineData("Invoices", "Invoice")]
    [InlineData("Categories", "Category")]
    [InlineData("Cities", "City")]
    [InlineData("Users", "User")]
    [InlineData("Order", "Order")]
    public void NamingHelper_ToSingular_extracts_singular(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToSingular(input));
    }

    [Theory]
    [InlineData("Order", "Orders")]
    [InlineData("Invoice", "Invoices")]
    [InlineData("Category", "Categories")]
    [InlineData("City", "Cities")]
    [InlineData("User", "Users")]
    public void NamingHelper_ToPlural_extracts_plural(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPlural(input));
    }

    [Theory]
    [InlineData("OrderProcessing", "order-processing")]
    [InlineData("Invoices", "invoices")]
    [InlineData("Users", "users")]
    public void NamingHelper_ToKebabCase_formats_correctly(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToKebabCase(input));
    }

    [Fact]
    public void TemplateEngine_GenerateDomainEntity_produces_valid_entity()
    {
        var code = TemplateEngine.GenerateDomainEntity("VsaTemplate", "Invoices", "Invoice");
        Assert.Contains("namespace VsaTemplate.Features.Invoices.Domain;", code);
        Assert.Contains("public class Invoice", code);
        Assert.Contains("public Guid Id { get; set; }", code);
        Assert.Contains("public string Name { get; set; }", code);
    }

    [Fact]
    public void TemplateEngine_GenerateContractEvent_produces_valid_contract()
    {
        var code = TemplateEngine.GenerateContractEvent("VsaTemplate", "Invoices", "Invoice");
        Assert.Contains("namespace VsaTemplate.Features.Invoices.Contracts;", code);
        Assert.Contains("public record InvoiceCreated(Guid InvoiceId, string Name);", code);
    }

    [Fact]
    public void TemplateEngine_GenerateEndpoints_implements_IFeatureEndpoints()
    {
        var code = TemplateEngine.GenerateEndpoints("VsaTemplate", "Invoices", "Invoice", "CreateInvoice", "POST");
        Assert.Contains("public sealed class Endpoints : IFeatureEndpoints", code);
        Assert.Contains("app.MapGroup(\"/invoices\").WithTags(\"Invoices\")", code);
        Assert.Contains("group.MapPost(\"/create-invoice\", (CreateInvoice.Request request, CreateInvoiceHandler handler, CancellationToken ct)", code);
        Assert.Contains(".WithName(nameof(CreateInvoice))", code);
        Assert.Contains(".WithValidation<CreateInvoice.Request>()", code);
    }

    [Fact]
    public void TemplateEngine_GenerateEndpointMappingLine_without_id_generates_valid_csharp()
    {
        var line = TemplateEngine.GenerateEndpointMappingLine("CreateOrder", "POST", hasValidation: true);
        Assert.Contains("group.MapPost(\"/create-order\", (CreateOrder.Request request, CreateOrderHandler handler, CancellationToken ct)", line);
        Assert.Contains("=> handler.Handle(request, ct)", line);
        Assert.Contains(".WithName(nameof(CreateOrder))", line);
        Assert.Contains(".WithValidation<CreateOrder.Request>();", line);
    }

    [Fact]
    public void TemplateEngine_GenerateEndpointMappingLine_with_id_param_generates_guid_parameter()
    {
        var line = TemplateEngine.GenerateEndpointMappingLine("GetOrderById", "GET", hasValidation: false);
        Assert.Contains("group.MapGet(\"/get-order-by-id\", (GetOrderById.Request request, GetOrderByIdHandler handler, CancellationToken ct)", line);
        Assert.Contains("=> handler.Handle(request, ct)", line);
        Assert.Contains(".WithName(nameof(GetOrderById));", line);
        Assert.DoesNotContain(".WithValidation", line);
    }

    [Fact]
    public void TemplateEngine_GenerateEndpointMappingStatement_returns_valid_ast_expression_statement()
    {
        var statement = TemplateEngine.GenerateEndpointMappingStatement("CancelOrder", "PUT", hasValidation: true);
        Assert.NotNull(statement);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax>(statement);
        var formatted = statement.NormalizeWhitespace().ToFullString();
        Assert.Contains("group.MapPut(\"/cancel-order\"", formatted);
        Assert.Contains("CancelOrder.Request", formatted);
    }

    [Fact]
    public void TemplateEngine_GenerateInitialCreateUseCase_style_b_contains_handler_and_contracts()
    {
        var code = TemplateEngine.GenerateInitialCreateUseCase("VsaTemplate", "Invoices", "Invoice", "instance");
        Assert.Contains("public static class CreateInvoice", code);
        Assert.Contains("public record Request(string Name);", code);
        Assert.Contains("public record Response(Guid Id, string Name, DateTimeOffset CreatedAt);", code);
        Assert.Contains("public class Validator : AbstractValidator<Request>", code);
        Assert.Contains("public class CreateInvoiceHandler(AppDbContext db, TimeProvider clock, IEventDispatcher events)", code);
        Assert.Contains("Task<Results<Created<CreateInvoice.Response>, Conflict<string>>> Handle", code);
        Assert.Contains("throw new NotImplementedException();", code);
    }

    [Fact]
    public void EndpointModifier_AddEndpointMapping_inserts_endpoint_cleanly()
    {
        var initialEndpoints = """
using VsaTemplate.Common.Endpoints;

namespace VsaTemplate.Features.Invoices;

public sealed class Endpoints : IFeatureEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/invoices").WithTags("Invoices");

        group.MapPost("/create-invoice", (CreateInvoice.Request request, CreateInvoiceHandler handler, CancellationToken ct)
            => handler.Handle(request, ct))
            .WithName(nameof(CreateInvoice))
            .WithValidation<CreateInvoice.Request>();
    }
}
""";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, initialEndpoints);

            var added = EndpointModifier.AddEndpointMapping(tempFile, "GetInvoiceById", "GET", hasValidation: false);
            Assert.True(added);

            var updated = File.ReadAllText(tempFile);
            Assert.Contains("group.MapGet(\"/get-invoice-by-id\", (GetInvoiceById.Request request, GetInvoiceByIdHandler handler, CancellationToken ct)", updated);
            Assert.Contains(".WithName(nameof(GetInvoiceById));", updated);
            Assert.Contains("public sealed class Endpoints : IFeatureEndpoints", updated);

            // Verify blank line separation between statements
            var normalizedEol = updated.Replace("\r\n", "\n");
            Assert.Contains("var group = app.MapGroup(\"/invoices\").WithTags(\"Invoices\");\n\n        group.MapPost", normalizedEol);
            Assert.Contains(".WithValidation<CreateInvoice.Request>();\n\n        group.MapGet", normalizedEol);

            // Adding same endpoint again returns false (idempotent)
            var addedAgain = EndpointModifier.AddEndpointMapping(tempFile, "GetInvoiceById", "GET", hasValidation: false);
            Assert.False(addedAgain);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void EndpointModifier_AddEndpointMapping_with_validation_adds_WithValidation_clause()
    {
        var initialEndpoints = """
using VsaTemplate.Common.Endpoints;

namespace VsaTemplate.Features.Invoices;

public sealed class Endpoints : IFeatureEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/invoices").WithTags("Invoices");
    }
}
""";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, initialEndpoints);

            var added = EndpointModifier.AddEndpointMapping(tempFile, "CreateInvoice", "POST", hasValidation: true);
            Assert.True(added);

            var updated = File.ReadAllText(tempFile);
            Assert.Contains(".WithValidation<CreateInvoice.Request>()", updated);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void EndpointModifier_AddEndpointMapping_throws_if_file_not_found()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cs");
        Assert.Throws<FileNotFoundException>(() =>
            EndpointModifier.AddEndpointMapping(nonExistent, "CreateInvoice", "POST"));
    }

    [Fact]
    public void EndpointModifier_AddEndpointMapping_throws_if_map_method_missing()
    {
        var invalidEndpoints = """
namespace VsaTemplate.Features.Invoices;

public sealed class Endpoints
{
    public static void OtherMethod() { }
}
""";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, invalidEndpoints);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                EndpointModifier.AddEndpointMapping(tempFile, "CreateInvoice", "POST"));
            Assert.Contains("No se encontró el método 'public static void Map", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ProjectLocator_Locates_project_in_explicit_directory_without_features_folder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var csproj = Path.Combine(tempDir, "LuchaApp.csproj");
            File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><RootNamespace>LuchaApp</RootNamespace></PropertyGroup></Project>");

            var projectInfo = ProjectLocator.Locate(tempDir);
            Assert.NotNull(projectInfo);
            Assert.Equal("LuchaApp", projectInfo.RootNamespace);
            Assert.Equal(tempDir, projectInfo.ProjectDirectory);
            Assert.Equal(Path.Combine(tempDir, "Features"), projectInfo.FeaturesDirectory);
            Assert.Equal(csproj, projectInfo.CsprojPath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectLocator_Locates_project_in_subfolder_src_from_solution_root()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var srcDir = Path.Combine(tempDir, "src", "LuchaApp");
        Directory.CreateDirectory(srcDir);
        try
        {
            var csproj = Path.Combine(srcDir, "LuchaApp.csproj");
            File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><RootNamespace>LuchaApp</RootNamespace></PropertyGroup></Project>");

            var projectInfo = ProjectLocator.Locate(tempDir);
            Assert.NotNull(projectInfo);
            Assert.Equal("LuchaApp", projectInfo.RootNamespace);
            Assert.Equal(srcDir, projectInfo.ProjectDirectory);
            Assert.Equal(Path.Combine(srcDir, "Features"), projectInfo.FeaturesDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectLocator_Locates_project_from_nested_subfolder_upward()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var nestedDir = Path.Combine(tempDir, "Features", "Luchadores");
        Directory.CreateDirectory(nestedDir);
        var prevCwd = Directory.GetCurrentDirectory();
        try
        {
            var csproj = Path.Combine(tempDir, "LuchaApp.csproj");
            File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            Directory.SetCurrentDirectory(nestedDir);
            var projectInfo = ProjectLocator.Locate();

            Assert.NotNull(projectInfo);
            Assert.Equal("LuchaApp", projectInfo.RootNamespace);
            Assert.Equal(tempDir, projectInfo.ProjectDirectory);
        }
        finally
        {
            Directory.SetCurrentDirectory(prevCwd);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ProjectLocator_Does_not_escape_solution_boundary_to_sibling_folders()
    {
        var parentTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var siblingProjectDir = Path.Combine(parentTempDir, "CleanArchitecture", "Backend.Application");
        var isolatedSolutionDir = Path.Combine(parentTempDir, "IsolatedApp");

        Directory.CreateDirectory(Path.Combine(siblingProjectDir, "Features"));
        Directory.CreateDirectory(isolatedSolutionDir);

        var prevCwd = Directory.GetCurrentDirectory();
        try
        {
            // Sibling has csproj and Features/
            File.WriteAllText(Path.Combine(siblingProjectDir, "Backend.Application.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            // Isolated solution has a solution file but NO valid csproj in it
            File.WriteAllText(Path.Combine(isolatedSolutionDir, "IsolatedApp.sln"), "Microsoft Visual Studio Solution File");

            Directory.SetCurrentDirectory(isolatedSolutionDir);

            // It should NOT jump to CleanArchitecture/Backend.Application
            var ex = Assert.Throws<InvalidOperationException>(() => ProjectLocator.Locate());
            Assert.Contains("No se encontró un proyecto .NET válido", ex.Message);
        }
        finally
        {
            Directory.SetCurrentDirectory(prevCwd);
            if (Directory.Exists(parentTempDir)) Directory.Delete(parentTempDir, true);
        }
    }

    [Fact]
    public void CreateFeatureCommand_Settings_Validate_fails_if_project_path_ends_with_csproj()
    {
        var settings = new FeatureCli.Commands.CreateFeatureCommand.Settings
        {
            Name = "Orders",
            ProjectPath = "src/VsaTemplate/VsaTemplate.csproj"
        };

        var result = settings.Validate();
        Assert.False(result.Successful);
        Assert.Contains("sin indicar el archivo .csproj", result.Message);
    }

    [Fact]
    public void CreateFeatureCommand_Settings_Validate_fails_if_project_path_directory_does_not_exist()
    {
        var settings = new FeatureCli.Commands.CreateFeatureCommand.Settings
        {
            Name = "Orders",
            ProjectPath = "src/NonExistentFolder12345"
        };

        var result = settings.Validate();
        Assert.False(result.Successful);
        Assert.Contains("no existe", result.Message);
    }

    [Fact]
    public void CreateFeatureCommand_Settings_Validate_fails_if_project_path_has_no_csproj()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var settings = new FeatureCli.Commands.CreateFeatureCommand.Settings
            {
                Name = "Orders",
                ProjectPath = tempDir
            };

            var result = settings.Validate();
            Assert.False(result.Successful);
            Assert.Contains("No se encontró ningún archivo .csproj", result.Message);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateFeatureCommand_Settings_Validate_succeeds_with_valid_project_directory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "MyApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var settings = new FeatureCli.Commands.CreateFeatureCommand.Settings
            {
                Name = "Orders",
                ProjectPath = tempDir
            };

            var result = settings.Validate();
            Assert.True(result.Successful);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CodeValidatorAndFormatter_throws_on_invalid_csharp_syntax()
    {
        var invalidCode = "public class IncompleteClass { public int MissingSemicolon }";
        var ex = Assert.Throws<InvalidOperationException>(() => CodeValidatorAndFormatter.ValidateAndFormat(invalidCode));
        Assert.Contains("Se detectaron errores de sintaxis", ex.Message);
    }

    [Fact]
    public void CodeValidatorAndFormatter_formats_valid_code_cleanly()
    {
        var unformattedCode = "namespace MyNs;public class   Foo{public int Bar{get;set;}}";
        var formatted = CodeValidatorAndFormatter.ValidateAndFormat(unformattedCode);
        Assert.Contains("namespace MyNs;", formatted);
        Assert.Contains("public class Foo", formatted);
        Assert.Contains("public int Bar { get; set; }", formatted);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void TemplateEngine_GenerateUseCase_all_variants_produce_valid_csharp(string method)
    {
        var code = TemplateEngine.GenerateUseCase(
            "VsaTemplate", "Orders", "Order", "ManageOrder", method);

        Assert.NotNull(code);
        Assert.Contains("namespace VsaTemplate.Features.Orders;", code);
        Assert.Contains("public static class ManageOrder", code);
        Assert.Contains("public record Request();", code);
        Assert.Contains("public record Response();", code);
        Assert.Contains("public class ManageOrderHandler(AppDbContext db)", code);
        Assert.Contains("throw new NotImplementedException();", code);
    }

    [Fact]
    public void TemplateEngine_GenerateUseCase_with_selected_properties_generates_typed_request_and_empty_response()
    {
        var props = new List<PropertyInfoModel>
        {
            new("Title", "string", false, true, false, false),
            new("Amount", "decimal", false, true, false, false),
            new("IsActive", "bool?", true, true, false, false)
        };

        var code = TemplateEngine.GenerateUseCase(
            "VsaTemplate", "Invoices", "Invoice", "UpdateInvoice", "PUT", "instance", props, withValidation: true);

        Assert.NotNull(code);
        Assert.Contains("public record Request(string Title, decimal Amount, bool? IsActive);", code);
        Assert.Contains("public record Response();", code);
        Assert.Contains("public class Validator : AbstractValidator<Request>", code);
        Assert.Contains("RuleFor(x => x.Title).NotEmpty();", code);
        Assert.Contains("throw new NotImplementedException();", code);
    }

    [Fact]
    public void TemplateEngine_GenerateUseCase_without_validation_omits_validator_class()
    {
        var props = new List<PropertyInfoModel>
        {
            new("Title", "string", false, true, false, false)
        };

        var code = TemplateEngine.GenerateUseCase(
            "VsaTemplate", "Invoices", "Invoice", "GetInvoice", "GET", "instance", props, withValidation: false);

        Assert.NotNull(code);
        Assert.Contains("public record Request(string Title);", code);
        Assert.DoesNotContain("public class Validator", code);
        Assert.DoesNotContain("using FluentValidation;", code);
    }

    [Fact]
    public void UseCaseScaffolder_Execute_creates_use_case_and_endpoints_cleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var featuresDir = Path.Combine(tempDir, "Features");
        var featureDir = Path.Combine(featuresDir, "Invoices");
        Directory.CreateDirectory(featureDir);

        try
        {
            var projectInfo = new ProjectInfo(
                CsprojPath: Path.Combine(tempDir, "TestApp.csproj"),
                ProjectDirectory: tempDir,
                RootNamespace: "TestApp",
                FeaturesDirectory: featuresDir);

            var props = new List<PropertyInfoModel>
            {
                new("Description", "string", false, true, false, false),
                new("Total", "decimal", false, true, false, false)
            };

            var request = new ScaffoldUseCaseRequest(
                ProjectInfo: projectInfo,
                FeatureName: "Invoices",
                UseCaseName: "CreateInvoice",
                HttpMethod: "POST",
                EntityName: "Invoice",
                SelectedProperties: props,
                WithValidation: true);

            var result = UseCaseScaffolder.Execute(request);

            Assert.True(result.UseCaseCreated);
            Assert.True(result.EndpointAdded);
            Assert.True(File.Exists(result.UseCaseFilePath));
            Assert.True(File.Exists(result.EndpointsFilePath));
            Assert.Equal("/invoices/create-invoice", result.FormattedRoute);

            var useCaseContent = File.ReadAllText(result.UseCaseFilePath);
            Assert.Contains("public record Request(string Description, decimal Total);", useCaseContent);
            Assert.Contains("public class Validator : AbstractValidator<Request>", useCaseContent);
            Assert.Contains("throw new NotImplementedException();", useCaseContent);

            var endpointsContent = File.ReadAllText(result.EndpointsFilePath);
            Assert.Contains("group.MapPost(\"/create-invoice\", (CreateInvoice.Request request, CreateInvoiceHandler handler, CancellationToken ct)", endpointsContent);
            Assert.Contains(".WithValidation<CreateInvoice.Request>();", endpointsContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UseCaseScaffolder_Execute_without_validation_omits_validator_and_endpoint_validation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var featuresDir = Path.Combine(tempDir, "Features");
        var featureDir = Path.Combine(featuresDir, "Invoices");
        Directory.CreateDirectory(featureDir);

        try
        {
            var projectInfo = new ProjectInfo(
                CsprojPath: Path.Combine(tempDir, "TestApp.csproj"),
                ProjectDirectory: tempDir,
                RootNamespace: "TestApp",
                FeaturesDirectory: featuresDir);

            var props = new List<PropertyInfoModel>
            {
                new("Description", "string", false, true, false, false)
            };

            var request = new ScaffoldUseCaseRequest(
                ProjectInfo: projectInfo,
                FeatureName: "Invoices",
                UseCaseName: "GetInvoice",
                HttpMethod: "GET",
                EntityName: "Invoice",
                SelectedProperties: props,
                WithValidation: false);

            var result = UseCaseScaffolder.Execute(request);

            Assert.True(result.UseCaseCreated);
            Assert.True(result.EndpointAdded);

            var useCaseContent = File.ReadAllText(result.UseCaseFilePath);
            Assert.Contains("public record Request(string Description);", useCaseContent);
            Assert.DoesNotContain("public class Validator", useCaseContent);
            Assert.DoesNotContain("using FluentValidation;", useCaseContent);

            var endpointsContent = File.ReadAllText(result.EndpointsFilePath);
            Assert.Contains("group.MapGet(\"/get-invoice\", (GetInvoice.Request request, GetInvoiceHandler handler, CancellationToken ct)", endpointsContent);
            Assert.DoesNotContain(".WithValidation", endpointsContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}



