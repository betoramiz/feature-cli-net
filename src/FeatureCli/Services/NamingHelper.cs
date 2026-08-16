using System.Text;
using System.Text.RegularExpressions;

namespace FeatureCli.Services;

public static partial class NamingHelper
{
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        input = input.Trim();
        var words = Regex.Split(input, @"[\s_\-\.]+");
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;
            sb.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                var rest = word[1..];
                if (rest.All(char.IsUpper))
                {
                    sb.Append(rest.ToLowerInvariant());
                }
                else
                {
                    sb.Append(rest);
                }
            }
        }

        return sb.ToString();
    }

    public static string FormatHttpMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "Get";
        var m = method.Trim().ToUpperInvariant();
        return m switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "DELETE" => "Delete",
            "PATCH" => "Patch",
            _ => char.ToUpperInvariant(m[0]) + m[1..].ToLowerInvariant()
        };
    }

    public static string ToCamelCase(string input)
    {
        var pascal = ToPascalCase(input);
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    public static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = ToPascalCase(input);
        var result = Regex.Replace(normalized, "(?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z0-9])", "-$1", RegexOptions.Compiled).ToLowerInvariant();
        return result.Trim('-');
    }

    public static string ToSingular(string name)
    {
        var pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal)) return pascal;

        if (pascal.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && pascal.Length > 3)
            return pascal[..^3] + "y";

        if (pascal.EndsWith("ses", StringComparison.OrdinalIgnoreCase) && pascal.Length > 3)
            return pascal[..^2];

        if (pascal.EndsWith("s", StringComparison.OrdinalIgnoreCase) && !pascal.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && pascal.Length > 1)
            return pascal[..^1];

        return pascal;
    }

    public static string ToPlural(string name)
    {
        var pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal)) return pascal;

        if (pascal.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            pascal.Length > 1 &&
            !"aeiou".Contains(char.ToLowerInvariant(pascal[^2])))
        {
            return pascal[..^1] + "ies";
        }

        if (pascal.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            pascal.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            pascal.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
            pascal.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            pascal.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return pascal + "es";
        }

        return pascal + "s";
    }

    public static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "/";
        route = route.Trim();
        if (!route.StartsWith('/')) route = "/" + route;
        return route;
    }
}
