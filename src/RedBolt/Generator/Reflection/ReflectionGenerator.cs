// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

[Generator]
public sealed class ReflectionGenerator : IIncrementalGenerator
{
    private const string ReflectedAttributeName = "RedBolt.Core.ReflectedAttribute";
    private const string SerializedAttributeName = "RedBolt.Core.SerializedAttribute";
    private const string CallableAttributeName = "RedBolt.Core.CallableAttribute";
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

    private static readonly DiagnosticDescriptor InvalidSerializedMember = new(
        "RBREF004",
        "Invalid serialized member",
        "Serialized attribute '{0}' must target an instance field, property, getter, or setter",
        "RedBolt.Reflection",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidCallableMember = new(
        "RBREF005",
        "Invalid callable member",
        "Callable attribute '{0}' must target a parameterless instance method returning void and use a valid string label source",
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
        var factory = GetNamedBool(reflectedAttribute, "Factory", true);
        var inheritAttributes = GetNamedBool(reflectedAttribute, "InheritAttributes", true);
        var inheritAttributesFrom = GetNamedType(reflectedAttribute, "InheritAttributesFrom") as INamedTypeSymbol;
        var inheritAttributesOrder = GetNamedInt(reflectedAttribute, "InheritAttributesOrder", 0);
        var attributeSourceType = inheritAttributesFrom ?? type.BaseType;
        var attributes = GetReflectedMembers(type);
        var builder = new StringBuilder();
        AppendHeader(builder, type);
        builder.Append("    internal ");
        if (HasAccessibleGeneratedBase(type))
            builder.Append("new ");
        builder.AppendLine("static void __RegisterGeneratedReflection(global::RedBolt.Core.Context context)");
        builder.AppendLine("    {");

        if (inheritAttributes && attributeSourceType is not null
            && GetAttribute(attributeSourceType, ReflectedAttributeName) is not null)
        {
            var attributeSourceTypeName = attributeSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        context.RegisterAttributedType(typeof(").Append(attributeSourceTypeName).AppendLine("));");
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
        if (type.IsAbstract || !factory)
            builder.Append("null");
        else
            builder.Append("static () => new ").Append(type.Name).Append("()");
        builder.AppendLine(");");
        builder.AppendLine("        if (__refresh)");
        builder.AppendLine("            reflection.RemoveAllAttributes();");

        var inheritanceWritten = !inheritAttributes || attributeSourceType is null;
        if (inheritanceWritten)
            AppendRemovedAttributes(builder, type);

        for (var index = 0; index < attributes.Count; ++index)
        {
            if (!inheritanceWritten && index >= inheritAttributesOrder)
            {
                AppendInheritedAttributes(builder, attributeSourceType!);
                AppendRemovedAttributes(builder, type);
                inheritanceWritten = true;
            }

            var succeeded = attributes[index].IsCallable
                ? AppendCallableAttribute(context, builder, type, attributes[index], index)
                : AppendSerializedAttribute(context, builder, type, attributes[index], index);
            if (!succeeded)
                return null;
        }

        if (!inheritanceWritten)
        {
            AppendInheritedAttributes(builder, attributeSourceType!);
            AppendRemovedAttributes(builder, type);
        }

        builder.AppendLine("    }");
        AppendFooter(builder, type);
        return builder.ToString();
    }

    private static void AppendInheritedAttributes(StringBuilder builder, INamedTypeSymbol attributeSourceType)
    {
        builder.Append("        var __baseReflection = global::RedBolt.Core.GeneratedReflectionRegistration.FindBaseReflection(context, typeof(")
            .Append(attributeSourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine("));");
        builder.AppendLine("        if (__baseReflection is not null)");
        builder.AppendLine("            reflection.CopyAttributesFrom(__baseReflection);");
    }

    private static void AppendRemovedAttributes(StringBuilder builder, INamedTypeSymbol type)
    {
        foreach (var removeAttribute in type.GetAttributes()
                     .Where(static attribute => attribute.AttributeClass?.ToDisplayString() == RemoveSerializedAttributeName))
        {
            var name = GetConstructorString(removeAttribute, 0);
            if (name is not null)
                builder.Append("        reflection.RemoveAttribute(new global::RedBolt.Container.StringHash(")
                    .Append(ToLiteral(name))
                    .AppendLine("));");
        }
    }

    private static bool AppendSerializedAttribute(
        SourceProductionContext context,
        StringBuilder builder,
        INamedTypeSymbol type,
        ReflectedMember serialized,
        int index)
    {
        var attribute = serialized.Attribute;
        var name = GetConstructorString(attribute, 0)!;
        var valueType = GetValueType(attribute, serialized.Symbol);
        var valueTypeName = valueType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (valueTypeName is null || !IsValidSerializedMember(serialized.Symbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSerializedMember,
                serialized.Location,
                name));
            return false;
        }

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
        var enumNames = GetNamedStringArray(type, attribute, "EnumNames", "EnumNamesSource");
        var accessorFactory = GetNamedString(attribute, "AccessorFactory");
        var accessorType = GetNamedType(attribute, "AccessorType");
        var getter = GetNamedString(attribute, "Getter");
        var setter = GetNamedString(attribute, "Setter");
        var configure = GetNamedString(attribute, "Configure");

        builder.Append("        using var __default").Append(index)
            .Append(" = global::RedBolt.Core.Variant.FromObject((")
            .Append(valueTypeName)
            .Append(")(")
            .Append(defaultExpression)
            .AppendLine("));");
        builder.Append("        using var __attribute").Append(index)
            .Append(" = new global::RedBolt.Core.AttributeInfo(")
            .Append(ToLiteral(name)).Append(", typeof(").Append(valueTypeName).Append("), __default")
            .Append(index).Append(", (global::RedBolt.Core.AttributeMode)").Append(mode).Append(", ")
            .Append(enumNames).Append(", ");

        if (!string.IsNullOrEmpty(accessorFactory))
        {
            builder.Append(accessorFactory).Append("()");
        }
        else if (accessorType is not null)
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

    private static bool AppendCallableAttribute(
        SourceProductionContext context,
        StringBuilder builder,
        INamedTypeSymbol type,
        ReflectedMember callable,
        int index)
    {
        var attribute = callable.Attribute;
        var name = GetConstructorString(attribute, 0)!;
        var labelGetter = GetCallableLabelGetter(type, attribute);
        if (callable.Symbol is not IMethodSymbol
            {
                IsStatic: false,
                MethodKind: MethodKind.Ordinary,
                Parameters.Length: 0,
                ReturnsVoid: true,
                Arity: 0
            } method || labelGetter is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidCallableMember,
                callable.Location,
                name));
            return false;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        builder.Append("        using var __default").Append(index)
            .AppendLine(" = global::RedBolt.Core.Variant.FromObject(false);");
        builder.Append("        using var __attribute").Append(index)
            .Append(" = new global::RedBolt.Core.AttributeInfo(")
            .Append(ToLiteral(name))
            .Append(", typeof(bool), __default").Append(index)
            .Append(", global::RedBolt.Core.AttributeMode.Edit, enumNames: null, accessor: null, getter: ")
            .Append(labelGetter)
            .Append(", setter: static (self, value) => { if (value is true) ((")
            .Append(typeName).Append(")self).")
            .Append(method.Name).AppendLine("(); });");
        builder.Append("        var __handle").Append(index)
            .Append(" = reflection.AddAttribute(__attribute").Append(index).AppendLine(");");
        builder.Append("        using var __isAction").Append(index)
            .AppendLine(" = global::RedBolt.Core.Variant.FromObject(true);");
        builder.Append("        __handle").Append(index)
            .Append(".SetMetadata(global::RedBolt.Core.AttributeMetadata.IsAction, __isAction")
            .Append(index).AppendLine(");");
        return true;
    }

    private static string? GetCallableLabelGetter(INamedTypeSymbol type, AttributeData attribute)
    {
        var labelSource = GetNamedString(attribute, "LabelSource");
        if (string.IsNullOrEmpty(labelSource))
            return "static self => " + ToLiteral(GetNamedString(attribute, "Label") ?? string.Empty);

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return FindMember(type, labelSource!) switch
        {
            IFieldSymbol { Type.SpecialType: SpecialType.System_String } field when field.IsStatic =>
                "static self => " + typeName + "." + field.Name,
            IFieldSymbol { Type.SpecialType: SpecialType.System_String } field =>
                "static self => ((" + typeName + ")self)." + field.Name,
            IPropertySymbol { Type.SpecialType: SpecialType.System_String, GetMethod: not null } property
                when property.IsStatic => "static self => " + typeName + "." + property.Name,
            IPropertySymbol { Type.SpecialType: SpecialType.System_String, GetMethod: not null } property =>
                "static self => ((" + typeName + ")self)." + property.Name,
            IMethodSymbol
            {
                ReturnType.SpecialType: SpecialType.System_String,
                Parameters.Length: 0,
                Arity: 0,
                IsStatic: true
            } method => "static self => " + typeName + "." + method.Name + "()",
            IMethodSymbol
            {
                ReturnType.SpecialType: SpecialType.System_String,
                Parameters.Length: 0,
                Arity: 0,
                IsStatic: false
            } method => "static self => ((" + typeName + ")self)." + method.Name + "()",
            _ => null
        };
    }

    private static string GetGetter(INamedTypeSymbol type, ISymbol? symbol, string? getter)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!string.IsNullOrEmpty(getter))
        {
            return FindMember(type, getter!) switch
            {
                IFieldSymbol field => "static self => ((" + typeName + ")self)." + field.Name,
                IPropertySymbol property => "static self => ((" + typeName + ")self)." + property.Name,
                _ => "static self => ((" + typeName + ")self)." + getter + "()"
            };
        }
        return symbol switch
        {
            IFieldSymbol field => "static self => ((" + typeName + ")self)." + field.Name,
            IPropertySymbol property when property.GetMethod is not null =>
                "static self => ((" + typeName + ")self)." + property.Name,
            IMethodSymbol method when method.MethodKind == MethodKind.PropertyGet
                && method.AssociatedSymbol is IPropertySymbol property =>
                "static self => ((" + typeName + ")self)." + property.Name,
            IMethodSymbol method when IsGetter(method) =>
                "static self => ((" + typeName + ")self)." + method.Name + "()",
            _ => "null"
        };
    }

    private static string GetSetter(INamedTypeSymbol type, ISymbol? symbol, string? setter, string valueTypeName)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!string.IsNullOrEmpty(setter))
        {
            return FindMember(type, setter!) switch
            {
                IFieldSymbol field when !field.IsReadOnly =>
                    "static (self, value) => ((" + typeName + ")self)." + field.Name + " = (" + valueTypeName + ")value!",
                IPropertySymbol property when property.SetMethod is not null =>
                    "static (self, value) => ((" + typeName + ")self)." + property.Name + " = (" + valueTypeName + ")value!",
                IMethodSymbol method when !method.IsStatic && method.Parameters.Length == 1 =>
                    "static (self, value) => ((" + typeName + ")self)." + method.Name + "(("
                    + method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")value!)",
                _ => "static (self, value) => ((" + typeName + ")self)." + setter + "((" + valueTypeName + ")value!)"
            };
        }
        return symbol switch
        {
            IFieldSymbol field when !field.IsReadOnly =>
                "static (self, value) => ((" + typeName + ")self)." + field.Name + " = (" + valueTypeName + ")value!",
            IPropertySymbol property when property.SetMethod is not null =>
                "static (self, value) => ((" + typeName + ")self)." + property.Name + " = (" + valueTypeName + ")value!",
            IMethodSymbol method when method.MethodKind == MethodKind.PropertySet
                && method.AssociatedSymbol is IPropertySymbol property =>
                "static (self, value) => ((" + typeName + ")self)." + property.Name + " = (" + valueTypeName + ")value!",
            IMethodSymbol method when IsSetter(method) =>
                "static (self, value) => ((" + typeName + ")self)." + method.Name + "(("
                + method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")value!)",
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
            var source = FindMember(type, defaultSource!);
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
            IPropertySymbol property => GetInitializer(property)
                ?? GetBackingFieldInitializer(type, property)
                ?? "default(" + valueTypeName + ")",
            _ => null
        };
    }

    private static string? GetBackingFieldInitializer(INamedTypeSymbol type, IPropertySymbol property)
    {
        var fieldName = GetReturnedMemberName(property);
        if (fieldName is null || FindMember(type, fieldName) is not IFieldSymbol field)
            return null;
        return GetInitializer(field);
    }

    private static string? GetReturnedMemberName(IPropertySymbol property)
    {
        foreach (var syntaxReference in property.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not PropertyDeclarationSyntax declaration)
                continue;
            var expression = declaration.ExpressionBody?.Expression;
            if (expression is null && declaration.AccessorList is not null)
            {
                var getter = declaration.AccessorList.Accessors.FirstOrDefault(
                    static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
                expression = getter?.ExpressionBody?.Expression;
                if (expression is null && getter?.Body?.Statements.Count == 1
                    && getter.Body.Statements[0] is ReturnStatementSyntax returnStatement)
                    expression = returnStatement.Expression;
            }
            if (GetReferencedMemberName(expression) is { } memberName)
                return memberName;
        }
        return null;
    }

    private static string? GetReferencedMemberName(ExpressionSyntax? expression) =>
        expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess when memberAccess.Expression is ThisExpressionSyntax =>
                memberAccess.Name.Identifier.ValueText,
            ParenthesizedExpressionSyntax parenthesized => GetReferencedMemberName(parenthesized.Expression),
            CastExpressionSyntax cast => GetReferencedMemberName(cast.Expression),
            _ => null
        };

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

    private static List<ReflectedMember> GetReflectedMembers(INamedTypeSymbol type)
    {
        var result = new List<ReflectedMember>();
        foreach (var member in type.GetMembers())
        {
            AddReflectedAttributes(result, member);
            if (member is IPropertySymbol property)
            {
                if (property.GetMethod is not null)
                    AddReflectedAttributes(result, property.GetMethod);
                if (property.SetMethod is not null)
                    AddReflectedAttributes(result, property.SetMethod);
            }
        }

        return result.OrderBy(static item => item.Location?.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(static item => item.Location?.SourceSpan.Start ?? 0)
            .ToList();
    }

    private static void AddReflectedAttributes(List<ReflectedMember> result, ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            if (attributeName is not SerializedAttributeName and not CallableAttributeName)
                continue;
            result.Add(new ReflectedMember(
                symbol,
                attribute,
                attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                attributeName == CallableAttributeName));
        }
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
            IMethodSymbol method when IsGetter(method) => method.ReturnType,
            IMethodSymbol method when IsSetter(method) => method.Parameters[0].Type,
            _ => null
        };
    }

    private static bool IsValidSerializedMember(ISymbol? symbol) =>
        symbol switch
        {
            IFieldSymbol field => !field.IsStatic,
            IPropertySymbol property => !property.IsStatic && !property.IsIndexer
                && (property.GetMethod is not null || property.SetMethod is not null),
            IMethodSymbol method => !method.IsStatic && (IsGetter(method) || IsSetter(method)),
            _ => false
        };

    private static bool IsGetter(IMethodSymbol method) =>
        method.MethodKind == MethodKind.PropertyGet
        || method.MethodKind == MethodKind.Ordinary && method.Parameters.Length == 0 && !method.ReturnsVoid;

    private static bool IsSetter(IMethodSymbol method) =>
        method.MethodKind == MethodKind.PropertySet
        || method.MethodKind == MethodKind.Ordinary && method.Parameters.Length == 1 && method.ReturnsVoid;

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
        foreach (var type in types.OrderBy(GetInheritanceDepth))
        {
            if (type.ContainingType is null)
            {
                var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Append("        global::RedBolt.Core.GeneratedReflectionRegistration.Add(typeof(")
                    .Append(typeName).Append("), static context => ")
                    .Append(typeName).AppendLine(".__RegisterGeneratedReflection(context));");
            }
            else
            {
                var outermostType = type.ContainingType;
                while (outermostType.ContainingType is not null)
                    outermostType = outermostType.ContainingType;
                builder.Append("        ")
                    .Append(outermostType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('.')
                    .Append(GetRegistrationMethodName(type)).AppendLine("();");
            }
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static bool HasAccessibleGeneratedBase(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (GetAttribute(baseType, ReflectedAttributeName) is not null
                && HasInternalAccess(baseType.ContainingAssembly, type.ContainingAssembly))
                return true;
        }
        return false;
    }

    private static bool HasInternalAccess(IAssemblySymbol declaringAssembly, IAssemblySymbol accessingAssembly)
    {
        if (SymbolEqualityComparer.Default.Equals(declaringAssembly, accessingAssembly))
            return true;

        foreach (var attribute in declaringAssembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.CompilerServices.InternalsVisibleToAttribute"
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not string friendAssembly)
            {
                continue;
            }

            var separator = friendAssembly.IndexOf(',');
            var friendAssemblyName = separator >= 0 ? friendAssembly[..separator] : friendAssembly;
            if (string.Equals(friendAssemblyName.Trim(), accessingAssembly.Identity.Name, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static int GetInheritanceDepth(INamedTypeSymbol type)
    {
        var depth = 0;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            ++depth;
        return depth;
    }

    private static void AppendHeader(StringBuilder builder, INamedTypeSymbol type)
    {
        var usingDirectives = type.DeclaringSyntaxReferences
            .SelectMany(static reference =>
            {
                var declaration = reference.GetSyntax();
                var compilationUnit = (CompilationUnitSyntax)declaration.SyntaxTree.GetRoot();
                var namespaceDeclaration = declaration.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();
                return compilationUnit.Usings.Concat(namespaceDeclaration?.Usings ?? default);
            })
            .Select(static usingDirective => usingDirective.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).AppendLine(";");
            builder.AppendLine();
        }

        foreach (var usingDirective in usingDirectives)
            builder.AppendLine(usingDirective);
        if (usingDirectives.Length != 0)
            builder.AppendLine();

        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            containingTypes.Push(current);
        foreach (var containingType in containingTypes)
        {
            builder.Append(GetAccessibility(containingType)).Append(" partial ")
                .Append(GetTypeKeyword(containingType)).Append(' ')
                .Append(GetDeclarationName(containingType)).AppendLine();
            builder.AppendLine("{");
        }

        builder.Append(GetAccessibility(type)).Append(" partial ").Append(GetTypeKeyword(type)).Append(' ')
            .Append(GetDeclarationName(type)).AppendLine();
        builder.AppendLine("{");
    }

    private static void AppendFooter(StringBuilder builder, INamedTypeSymbol type)
    {
        builder.AppendLine("}");
        if (type.ContainingType is null)
            return;

        for (var current = type.ContainingType; current.ContainingType is not null; current = current.ContainingType)
            builder.AppendLine("}");

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        builder.AppendLine();
        builder.Append("    internal static void ").Append(GetRegistrationMethodName(type)).AppendLine("()");
        builder.AppendLine("    {");
        builder.Append("        global::RedBolt.Core.GeneratedReflectionRegistration.Add(typeof(")
            .Append(typeName).Append("), static context => ").Append(typeName)
            .AppendLine(".__RegisterGeneratedReflection(context));");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static string GetRegistrationMethodName(INamedTypeSymbol type) =>
        "__AddGeneratedReflection_" + GetHintName(type).Replace('.', '_');

    private static string GetDeclarationName(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
            return type.Name;
        return type.Name + "<" + string.Join(", ", type.TypeParameters.Select(static parameter => parameter.Name)) + ">";
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.IsRecord)
            return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
        return type.TypeKind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Struct => "struct",
            _ => "class"
        };
    }

    private static string GetAccessibility(INamedTypeSymbol type) =>
        type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => string.Empty
        };

    private static bool IsValidType(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.Arity != 0)
            return false;
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            if (current.Arity != 0)
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

    private static int GetNamedEnum(AttributeData attribute, string name, int defaultValue) =>
        GetNamedInt(attribute, name, defaultValue);

    private static int GetNamedInt(AttributeData attribute, string name, int defaultValue)
    {
        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        return value.Key is null ? defaultValue : Convert.ToInt32(value.Value.Value);
    }

    private static ITypeSymbol? GetNamedType(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as ITypeSymbol;

    private static string GetNamedStringArray(
        INamedTypeSymbol type,
        AttributeData attribute,
        string name,
        string sourceName)
    {
        var source = GetNamedString(attribute, sourceName);
        if (!string.IsNullOrEmpty(source))
        {
            return FindMember(type, source!) is IMethodSymbol
                ? source + "()"
                : source!;
        }

        var value = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name);
        if (value.Key is null || value.Value.IsNull)
            return "null";
        return "new string[] { "
            + string.Join(", ", value.Value.Values.Select(static item => ToLiteral((string)item.Value!)))
            + " }";
    }

    private static ISymbol? FindMember(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var member = current.GetMembers(name).FirstOrDefault();
            if (member is not null)
                return member;
        }
        return null;
    }

    private static string GetHintName(INamedTypeSymbol type) =>
        type.ToDisplayString().Replace('.', '_').Replace('+', '_') + ".Reflection.g.cs";

    private static string ToLiteral(string value) =>
        SymbolDisplay.FormatLiteral(value, true);

    private readonly struct ReflectedMember
    {
        public ReflectedMember(ISymbol? symbol, AttributeData attribute, Location? location, bool isCallable)
        {
            Symbol = symbol;
            Attribute = attribute;
            Location = location;
            IsCallable = isCallable;
        }

        public ISymbol? Symbol { get; }

        public AttributeData Attribute { get; }

        public Location? Location { get; }

        public bool IsCallable { get; }
    }
}
