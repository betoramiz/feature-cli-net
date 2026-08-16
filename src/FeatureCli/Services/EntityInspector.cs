using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public record PropertyInfoModel(
    string Name,
    string Type,
    bool IsNullable,
    bool IsScalar,
    bool IsCollection,
    bool IsNavigationOrComplex
);

public static class EntityInspector
{
    private static readonly HashSet<string> KnownScalarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "String",
        "int", "Int32", "uint", "UInt32",
        "long", "Int64", "ulong", "UInt64",
        "short", "Int16", "ushort", "UInt16",
        "byte", "Byte", "sbyte", "SByte",
        "bool", "Boolean",
        "decimal", "Decimal",
        "double", "Double",
        "float", "Single",
        "char", "Char",
        "Guid", "Guid?",
        "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan",
        "byte[]", "Byte[]",
        "Uri", "object"
    };

    private static readonly HashSet<string> CollectionTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ICollection", "Collection",
        "IList", "List",
        "IEnumerable",
        "IReadOnlyList", "IReadOnlyCollection",
        "ISet", "HashSet"
    };

    public static List<PropertyInfoModel> ExtractProperties(string csharpSourceCode, bool excludeComplexAndNavigations = true)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpSourceCode);
        var root = tree.GetCompilationUnitRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return [];

        var properties = new List<PropertyInfoModel>();

        foreach (var p in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var isPublic = p.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            var hasGetter = p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false;
            if (!isPublic || !hasGetter) continue;

            var isVirtual = p.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword));
            var rawType = p.Type.ToString();
            var (isScalar, isCollection) = AnalyzeType(p.Type);

            var isNavigationOrComplex = isVirtual || isCollection || !isScalar;

            var propModel = new PropertyInfoModel(
                Name: p.Identifier.Text,
                Type: rawType,
                IsNullable: p.Type is NullableTypeSyntax || rawType.EndsWith("?"),
                IsScalar: isScalar,
                IsCollection: isCollection,
                IsNavigationOrComplex: isNavigationOrComplex
            );

            if (excludeComplexAndNavigations && propModel.IsNavigationOrComplex)
            {
                continue;
            }

            properties.Add(propModel);
        }

        return properties;
    }

    private static (bool IsScalar, bool IsCollection) AnalyzeType(TypeSyntax typeSyntax)
    {
        // 1. Manejar arrays (ej. byte[] vs OrderItem[])
        if (typeSyntax is ArrayTypeSyntax arrayType)
        {
            var elementTypeName = arrayType.ElementType.ToString();
            if (elementTypeName.Equals("byte", StringComparison.OrdinalIgnoreCase))
            {
                return (IsScalar: true, IsCollection: false); // byte[] se considera escalar
            }
            return (IsScalar: false, IsCollection: true);
        }

        // 2. Manejar tipos genéricos como List<OrderItem>, ICollection<Tag>, Nullable<int>
        if (typeSyntax is GenericNameSyntax genericName)
        {
            var genericTypeName = genericName.Identifier.Text;

            // Si es Nullable<T> (ej. Nullable<DateTime>)
            if (genericTypeName.Equals("Nullable", StringComparison.OrdinalIgnoreCase))
            {
                var innerType = genericName.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();
                var isInnerScalar = innerType != null && KnownScalarTypes.Contains(innerType);
                return (IsScalar: isInnerScalar, IsCollection: false);
            }

            // Si es una colección genérica (ej. List<T>, ICollection<T>)
            if (CollectionTypeNames.Contains(genericTypeName))
            {
                return (IsScalar: false, IsCollection: true);
            }
        }

        // 3. Obtener el nombre del tipo base eliminando el '?' de nulabilidad (ej: "int?" -> "int")
        var normalizedTypeName = typeSyntax.ToString().TrimEnd('?').Trim();

        // 4. Verificar si está en la lista de tipos escalares conocidos
        if (KnownScalarTypes.Contains(normalizedTypeName))
        {
            return (IsScalar: true, IsCollection: false);
        }

        // Si no es un tipo escalar conocido ni colección reconocida, es una entidad/objeto complejo
        // (Ej: Customer, OrderStatus, Address, etc.)
        return (IsScalar: false, IsCollection: false);
    }
}