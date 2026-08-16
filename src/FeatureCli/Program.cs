using FeatureCli.Commands;
using Spectre.Console.Cli;

namespace FeatureCli;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            config.SetApplicationName("feature");
            config.SetApplicationVersion(version);

            config.AddCommand<CreateFeatureCommand>("create")
                .WithAlias("feature-create")
                .WithDescription("Crea un nuevo feature con la estructura Vertical Slice Architecture (VSA).")
                .WithExample(["create", "-n", "Orders"]);

            config.AddCommand<CreateUseCaseCommand>("usecase")
                .WithAlias("feature-usecase")
                .WithDescription("Agrega un nuevo caso de uso a un feature existente y actualiza sus Endpoints.")
                .WithExample(["usecase", "-n", "GetOrderById", "-f", "Orders", "-m", "GET"])
                .WithExample(["usecase", "-n", "CancelOrder", "-f", "Orders", "-m", "POST"]);

            config.ValidateExamples();
        });

        // Pre-process arguments if called as 'feature-create' or 'feature-usecase'
        var processedArgs = ProcessArgs(args);

        return app.Run(processedArgs);
    }

    private static string[] ProcessArgs(string[] args)
    {
        if (args.Length == 0) return args;

        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (i == 0 && arg.Equals("feature-create", StringComparison.OrdinalIgnoreCase))
            {
                result.Add("create");
            }
            else if (i == 0 && arg.Equals("feature-usecase", StringComparison.OrdinalIgnoreCase))
            {
                result.Add("usecase");
            }
            else if (arg.Equals("-wv", StringComparison.OrdinalIgnoreCase))
            {
                result.Add("--withValidation");
            }
            else
            {
                result.Add(arg);
            }
        }

        return [.. result];
    }
}
