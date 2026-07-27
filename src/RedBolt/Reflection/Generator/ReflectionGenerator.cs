using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace RedBolt.Reflection.Generator;

[Generator]
public sealed class ReflectionGenerator : IIncrementalGenerator
{
    private const string ReflectedAttributeName = "RedBolt.Core.ReflectedAttribute";
    private const string SerializedAttributeName = "RedBolt.Core.SerializedAttribute";
    private const string RemoveSerializedAttributeName = "RedBolt.Core.RemoveSerializedAttribute";

    private static readonly DiagnosticDescriptor PartialTypeRequired = new(
        "RBREF001",
        "Reflected type must be partial",
        "Reflected type '{0}' and all containing types must be partial",
        "RedBolt.Reflection",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidReflectedType = new(
        "RBREF002",
        "Invalid reflected type",
        "Reflected type '{0}' must be a non-generic class derived from RedObject",
        "RedBolt.Reflection",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingDefaultSource = new(
        "RBREF003",
        "Serialized attribute requires a default source",
        "Serialized attribute '{0}' on '{1}' requires an initialized member or DefaultSource",
        "RedBolt.Reflection",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var reflectedTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
                ReflectedAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .WithComparer(SymbolEqualityComparer.Default)
            .Collect();

        context.RegisterSourceOutput(reflectedTypes, Execute);
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<INamedTypeSymbol> types)
    {
        var uniqueTypes = types.Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .OrderBy(static type => type.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(static type => type.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToArray();
        var generatedTypes = new List<INamedTypeSymbol>();

        foreach (var type in uniqueTypes)
        {
            if (!IsValidType(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidReflectedType,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
                continue;
            }

            if (!IsPartial(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PartialTypeRequired,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
                continue;
            }

            var source = GenerateType(context, type);
            if (source is null)
                continue;
            context.AddSource(GetHintName(type), SourceText.From(source, Encoding.UTF8));
            generatedTypes.Add(type);
        }

        if (generatedTypes.Count != 0)
            context.AddSource(
                "RedBolt.Reflection.ModuleInitializer.g.cs",
                SourceText.From(GenerateModuleInitializer(generatedTypes), Encoding.UTF8));
    }

    private static string? GenerateType(SourceProductionContext context, INamedTypeSymbol type)
    {
        var reflectedAttribute = GetAttribute(type, ReflectedAttributeName)!;
        var category = GetConstructorString(reflectedAttribute, 0) ?? string.Empty;
        var factory = GetNamedString(reflectedAttribute, "Factory");
        var inheritAttributes = GetNamedBool(reflectedAttribute, "InheritAttributes", true);
        var attributes = GetSerializedMembers(type);
        var builder = new StringBuilder();
        AppendHeader(builder, type);
        builder.Append("    internal ");
        if (type.BaseType is not null && GetAttribute(type.BaseType, ReflectedAttributeName) is not null)
            builder.Append("new ");
        builder.AppendLine("static void __RegisterGeneratedReflection(global::RedBolt.Core.Context context)");
        builder.AppendLine("    {");

        var baseType = type.BaseType;
        if (inheritAttributes && baseType is not null && GetAttribute(baseType, ReflectedAttributeName) is not null)
        {
            var baseTypeName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        context.RegisterAttributedType(typeof(").Append(baseTypeName).AppendLine("));");
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        builder.Append("        var __typeId = new global::RedBolt.Container.StringHash(global::RedBolt.Core.ObjectExtensionMethods.GetClassName(typeof(")
            .Append(typeName)
            .AppendLine(")));");
        builder.AppendLine("        var __refresh = context.IsReflected(__typeId);");
        builder.Append("        var reflection = __refresh ? context.GetReflection(__typeId) : context.AddGeneratedReflection(typeof(")
            .Append(typeName)
            .Append("), ")
            .Append(ToLiteral(category))
            .Append(", ");
        if (type.IsAbstract)
            builder.Append("null");
        else if (!string.IsNullOrEmpty(factory))
            builder.Append("static () => ").Append(factory).Append("()");
        else
            builder.Append("static () => new ").Append(type.Name).Append("()");
        builder.AppendLine(");");
        builder.AppendLine("        if (__refresh)");
        builder.AppendLine("            reflection.RemoveAllAttributes();");

        if (inheritAttributes && baseType is not null)
        {
            builder.Append("        var __baseReflection = global::RedBolt.Core.GeneratedReflectionRegistration.FindBaseReflection(context, typeof(")
                .Append(baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .AppendLine("));");
            builder.AppendLine("        if (__baseReflection is not null)");
            builder.AppendLine("            reflection.CopyAttributesFrom(__baseReflection);");
        }

        foreach (var removeAttribute in type.GetAttributes()
                     .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == RemoveSerializedAttributeName))
        {
            var name = GetConstructorString(removeAttribute, 0);
            if (name is not null)
                builder.Append("        reflection.RemoveAttribute(new global::RedBolt.Container.StringHash(")
                    .Append(ToLiteral(name))
                    .AppendLine("));");
        }

        for (var index = 0; index < attributes.Count; ++index)
        {
            if (!AppendSerializedAttribute(context, builder, type, attributes[index], index))
                return null;
        }

        builder.AppendLine("    }");
        AppendFooter(builder, type);
        return builder.ToString();
    }

    private static bool AppendSerializedAttribute(
        SourceProductionContext context,
        StringBuilder builder,
        INamedTypeSymbol type,
        SerializedMember serialized,
        int index)
    {
        var attribute = serialized.Attribute;
        var name = GetConstructorString(attribute, 0)!;
        var valueType = GetValueType(attribute, serialized.Symbol);
        var valueTypeName = valueType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (valueTypeName is null)
            return false;

        var defaultExpression = GetDefaultExpression(type, serialized.Symbol, attribute, valueTypeName);
        if (defaultExpression is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDefaultSource,
                serialized.Location,
                name,
                serialized.Symbol?.Name ?? type.Name));
            return false;
        }

        var mode = GetNamedEnum(attribute, "Mode", 65);
        var scopeHint = GetNamedEnum(attribute, "ScopeHint", 0);
        var enumNames = GetNamedStringArray(attribute, "EnumNames");
        var accessorType = GetNamedType(attribute, "AccessorType");
        var getter = GetNamedString(attribute, "Getter");
        var setter = GetNamedString(attribute, "Setter");
        var configure = GetNamedString(attribute, "Configure");

        builder.Append("        using var __default").Append(index)
            .Append(" = global::RedBolt.Core.Variant.FromObject(")
            .Append(defaultExpression)
            .AppendLine(");");
        builder.Append("        using var __attribute").Append(index)
            .Append(" = new global::RedBolt.Core.AttributeInfo(")
            .Append(ToLiteral(name)).Append(", typeof(").Append(valueTypeName).Append("), __default")
            .Append(index).Append(", (global::RedBolt.Core.AttributeMode)").Append(mode).Append(", ")
            .Append(enumNames).Append(", ");

        if (accessorType is not null)
        {
            builder.Append("new ")
                .Append(accessorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("()");
        }
        else
        {
            builder.Append("accessor: null, getter: ");
            builder.Append(GetGetter(type, serialized.Symbol, getter));
            builder.Append(", setter: ");
            builder.Append(GetSetter(type, serialized.Symbol, setter, valueTypeName));
        }
        builder.AppendLine(");");
        builder.Append("        var __handle").Append(index)
            .Append(" = reflection.AddAttribute(__attribute").Append(index).AppendLine(");");
        if (scopeHint != 0)
            builder.Append("        __handle").Append(index)
                .Append(".SetScopeHint((global::RedBolt.Core.AttributeScopeHint)").Append(scopeHint).AppendLine(");");
        if (!string.IsNullOrEmpty(configure))
            builder.Append("        ").Append(configure).Append("(__handle").Append(index).AppendLine(");");
        return true;
    }

    private static string GetGetter(INamedTypeSymbol type, ISymbol? symbol, string? getter)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!string.IsNullOrEmpty(getter))
            return "static self => ((" + typeName + ")self)." + getter + "()";
        return symbol switch
        {
            IFieldSymbol field => "static self => ((" + typeName + ")self)." + field.Name,
            IPropertySymbol property => "static self => ((" + typeName + ")self)." + property.Name,
            IMethodSymbol method => "static self => ((" + typeName + ")self)." + method.Name + "()",
            _ => "null"
        };
    }

    private static string GetSetter(INamedTypeSymbol type, ISymbol? symbol, string? setter, string valueTypeName)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!string.IsNullOrEmpty(setter))
            return "static (self, value) => ((" + typeName + ")self)." + setter + "((" + valueTypeName + ")value!)";
        return symbol switch
        {
            IFieldSymbol field when !field.IsReadOnly =>
                "static (self, value) => ((" + typeName + ")self)." + field.Name + " = (" + valueTypeName + ")value!",
            IPropertySymbol property when property.SetMethod is not null =>
                "static (self, value) => ((" + typeName + ")self)." + property.Name + " = (" + valueTypeName + ")value!",
            _ => "null"
        };
    }

    private static string? GetDefaultExpression(
        INamedTypeSymbol type,
        ISymbol? symbol,
        AttributeData attribute,
        string valueTypeName)
    {
        var defaultSource = GetNamedString(attribute, "DefaultSource");
        if (!string.IsNullOrEmpty(defaultSource))
        {
            var source = type.GetMembers(defaultSource!).FirstOrDefault();
            return source switch
            {
                IFieldSymbol field when field.IsConst || field.IsStatic => field.Name,
                IFieldSymbol field => GetInitializer(field) ?? "default(" + valueTypeName + ")",
                IPropertySymbol property when property.IsStatic => property.Name,
                IPropertySymbol property => GetInitializer(property),
                IMethodSymbol method when method.IsStatic && method.Parameters.Length == 0 => method.Name + "()",
                _ => null
            };
        }

        return symbol switch
        {
            IFieldSymbol field => GetInitializer(field) ?? "default(" + valueTypeName + ")",
            IPropertySymbol property => GetInitializer(property) ?? "default(" + valueTypeName + ")",
            _ => null
        };
    }

    private static string? GetInitializer(ISymbol symbol)
    {
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var syntax = syntaxReference.GetSyntax();
            if (syntax is VariableDeclaratorSyntax variable)
                return variable.Initializer?.Value.ToString();
            if (syntax is PropertyDeclarationSyntax property)
                return property.Initializer?.Value.ToString();
        }
        return null;
    }

    private static List<SerializedMember> GetSerializedMembers(INamedTypeSymbol type)
    {
        var result = new List<SerializedMember>();
        foreach (var attribute in type.GetAttributes()
                     .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == SerializedAttributeName))
            result.Add(new SerializedMember(null, attribute, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));

        foreach (var member in type.GetMembers())
        {
            foreach (var attribute in member.GetAttributes()
                         .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == SerializedAttributeName))
                result.Add(new SerializedMember(member, attribute, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
        }

        return result.OrderBy(static item => item.Location?.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(static item => item.Location?.SourceSpan.Start ?? 0)
            .ToList();
    }

    private static ITypeSymbol? GetValueType(AttributeData attribute, ISymbol? symbol)
    {
        if (attribute.ConstructorArguments.Length > 1
            && attribute.ConstructorArguments[1].Value is ITypeSymbol explicitType)
            return explicitType;
        return symbol switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IMethodSymbol method => method.ReturnType,
            _ => null
        };
    }

    private static string GenerateModuleInitializer(IReadOnlyList<INamedTypeSymbol> types)
    {
        var builder = new StringBuilder();
        builder.AppendLine("namespace RedBolt.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class ReflectionModuleInitializer");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void Initialize()");
        builder.AppendLine("    {");
        foreach (var type in types)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        global::RedBolt.Core.GeneratedReflectionRegistration.Add(typeof(")
                .Append(typeName).Append("), static context => ")
                .Append(typeName).AppendLine(".__RegisterGeneratedReflection(context));");
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, INamedTypeSymbol type)
    {
        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).AppendLine(";");
            builder.AppendLine();
        }
        builder.Append(GetAccessibility(type)).Append(" partial class ").Append(type.Name).AppendLine();
        builder.AppendLine("{");
    }

    private static void AppendFooter(StringBuilder builder, INamedTypeSymbol type)
    {
        builder.AppendLine("}");
    }

    private static string GetAccessibility(INamedTypeSymbol type) =>
        type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => string.Empty
        };

    private static bool IsValidType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.Arity != 0)
            return false;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == "RedBolt.Core.RedObject")
                return true;
        return false;
    }

    private static bool IsPartial(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!current.DeclaringSyntaxReferences
                    .Select(static reference => reference.GetSyntax())
                    .OfType<TypeDeclarationSyntax>()
                    .All(static declaration => declaration.Modifiers.Any(
                        static modifier => modifier.Text == "partial")))
                return false;
        }
        return true;
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

    private static string? GetConstructorString(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as string : null;

    private static string? GetNamedString(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;

    private static bool GetNamedBool(AttributeData attribute, string name, bool defaultValue)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        return value.Key is null ? defaultValue : value.Value.Value is true;
    }

    private static int GetNamedEnum(AttributeData attribute, string name, int defaultValue)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        return value.Key is null ? defaultValue : Convert.ToInt32(value.Value.Value);
    }

    private static ITypeSymbol? GetNamedType(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as ITypeSymbol;

    private static string GetNamedStringArray(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        if (value.Key is null || value.Value.IsNull)
            return "null";
        return "new string[] { "
            + string.Join(", ", value.Value.Values.Select(static item => ToLiteral((string)item.Value!)))
            + " }";
    }

    private static string GetHintName(INamedTypeSymbol type) =>
        type.ToDisplayString().Replace('.', '_').Replace('+', '_') + ".Reflection.g.cs";

    private static string ToLiteral(string value) =>
        SymbolDisplay.FormatLiteral(value, true);

    private readonly struct SerializedMember
    {
        public SerializedMember(ISymbol? symbol, AttributeData attribute, Location? location)
        {
            Symbol = symbol;
            Attribute = attribute;
            Location = location;
        }

        public ISymbol? Symbol { get; }

        public AttributeData Attribute { get; }

        public Location? Location { get; }
    }
}
