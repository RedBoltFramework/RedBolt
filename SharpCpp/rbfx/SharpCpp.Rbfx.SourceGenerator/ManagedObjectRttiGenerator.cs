using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpCpp.Rbfx.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class ManagedObjectRttiGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor PartialRequiredDiagnostic = new(
        id: "RBFXSG001",
        title: "Managed rbfx object types must be partial",
        messageFormat: "Type '{0}' derives from Urho3D.Core.Object and needs generated RTTI overrides; mark it partial or implement the RTTI members manually",
        category: "SharpCpp.Rbfx.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TypeNameCollisionDiagnostic = new(
        id: "RBFXSG002",
        title: "Managed rbfx RTTI names must be unique",
        messageFormat: "Type '{0}' cannot use generated rbfx RTTI because the type name '{1}' already belongs to another Object-derived type",
        category: "SharpCpp.Rbfx.SourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
            static (syntaxContext, cancellationToken) => syntaxContext.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)syntaxContext.Node, cancellationToken) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        var inputs = context.CompilationProvider.Combine(typeDeclarations.Collect());
        context.RegisterSourceOutput(inputs, static (productionContext, input) =>
        {
            Execute(productionContext, input.Left, input.Right);
        });
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<INamedTypeSymbol?> discoveredSymbols)
    {
        var objectSymbol = compilation.GetTypeByMetadataName("Urho3D.Core.Object");
        var stringHashSymbol = compilation.GetTypeByMetadataName("Urho3D.Math.StringHash");
        var nativeObjectRegistrationSymbol = compilation.GetTypeByMetadataName("SharpCpp.Support.INativeObjectRegistration`1");
        if (objectSymbol is null || stringHashSymbol is null || nativeObjectRegistrationSymbol is null)
        {
            return;
        }

        var uniqueSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var discoveredSymbol in discoveredSymbols)
        {
            if (discoveredSymbol is not null)
            {
                uniqueSymbols.Add(discoveredSymbol);
            }
        }

        foreach (var typeSymbol in uniqueSymbols)
        {
            if (!TryCreateCandidate(context, typeSymbol, objectSymbol, stringHashSymbol, nativeObjectRegistrationSymbol, out var candidate) ||
                candidate is null)
            {
                continue;
            }

            if (HasTypeNameCollision(compilation, candidate.Symbol, candidate.TypeName, objectSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TypeNameCollisionDiagnostic,
                    candidate.Location,
                    candidate.Symbol.ToDisplayString(),
                    candidate.TypeName));
                continue;
            }

            context.AddSource(GetHintName(candidate), SourceText.From(Render(candidate), Encoding.UTF8));
        }
    }

    private static bool TryCreateCandidate(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol objectSymbol,
        INamedTypeSymbol stringHashSymbol,
        INamedTypeSymbol nativeObjectRegistrationSymbol,
        out Candidate? candidate)
    {
        candidate = null;

        if (typeSymbol.TypeKind != TypeKind.Class ||
            typeSymbol.ContainingType is not null ||
            typeSymbol.TypeParameters.Length > 0)
        {
            return false;
        }

        if (!DerivesFrom(typeSymbol, objectSymbol) ||
            ImplementsDirectNativeRegistration(typeSymbol, nativeObjectRegistrationSymbol) ||
            HasManualRttiImplementation(typeSymbol, stringHashSymbol))
        {
            return false;
        }

        if (!IsPartial(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(PartialRequiredDiagnostic, GetLocation(typeSymbol), typeSymbol.Name));
            return false;
        }

        if (typeSymbol.BaseType is null)
        {
            return false;
        }

        candidate = new Candidate(
            typeSymbol,
            GetAccessibilityText(typeSymbol),
            GetNamespace(typeSymbol),
            typeSymbol.Name,
            typeSymbol.BaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            hideStaticTypeNameMembers: !SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, objectSymbol),
            GetLocation(typeSymbol));
        return true;
    }

    private static bool DerivesFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
    {
        for (var current = typeSymbol.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsDirectNativeRegistration(INamedTypeSymbol typeSymbol, INamedTypeSymbol nativeObjectRegistrationSymbol)
    {
        var inheritedInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        if (typeSymbol.BaseType is not null)
        {
            foreach (var interfaceSymbol in typeSymbol.BaseType.AllInterfaces)
            {
                inheritedInterfaces.Add(interfaceSymbol);
            }
        }

        foreach (var interfaceSymbol in typeSymbol.Interfaces)
        {
            if (!inheritedInterfaces.Contains(interfaceSymbol) &&
                SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, nativeObjectRegistrationSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasManualRttiImplementation(INamedTypeSymbol typeSymbol, INamedTypeSymbol stringHashSymbol)
    {
        return HasInstanceMethod(typeSymbol, "GetType") ||
            HasInstanceMethod(typeSymbol, "GetTypeName") ||
            HasInstanceMethod(typeSymbol, "GetTypeInfo") ||
            HasIsInstanceOfMethod(typeSymbol, stringHashSymbol) ||
            HasStaticMethod(typeSymbol, "GetTypeStatic") ||
            HasStaticMethod(typeSymbol, "GetTypeNameStatic") ||
            HasStaticMethod(typeSymbol, "GetTypeInfoStatic");
    }

    private static bool HasInstanceMethod(INamedTypeSymbol typeSymbol, string methodName)
    {
        foreach (var member in typeSymbol.GetMembers(methodName))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, typeSymbol) &&
                method.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIsInstanceOfMethod(INamedTypeSymbol typeSymbol, INamedTypeSymbol stringHashSymbol)
    {
        foreach (var member in typeSymbol.GetMembers("IsInstanceOf"))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, typeSymbol) &&
                method.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stringHashSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStaticMethod(INamedTypeSymbol typeSymbol, string methodName)
    {
        foreach (var member in typeSymbol.GetMembers(methodName))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: true } method &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, typeSymbol) &&
                method.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol)
    {
        foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTypeNameCollision(Compilation compilation, INamedTypeSymbol typeSymbol, string typeName, INamedTypeSymbol objectSymbol)
    {
        foreach (var candidate in compilation.GetSymbolsWithName(typeName, SymbolFilter.Type))
        {
            if (candidate is not INamedTypeSymbol otherType ||
                !DerivesFromOrIs(otherType, objectSymbol) ||
                SymbolEqualityComparer.Default.Equals(otherType, typeSymbol))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool DerivesFromOrIs(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
    {
        return SymbolEqualityComparer.Default.Equals(typeSymbol, baseType) || DerivesFrom(typeSymbol, baseType);
    }

    private static Location GetLocation(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : Location.None;
    }

    private static string GetAccessibilityText(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            _ => "internal"
        };
    }

    private static string? GetNamespace(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : null;
    }

    private static string GetHintName(Candidate candidate)
    {
        var builder = new StringBuilder();
        foreach (var character in candidate.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        builder.Append(".ManagedObjectRtti.g.cs");
        return builder.ToString();
    }

    private static string Render(Candidate candidate)
    {
        var builder = new StringBuilder();
        var indent = candidate.Namespace is null ? string.Empty : "    ";
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (candidate.Namespace is not null)
        {
            builder.Append("namespace ").Append(candidate.Namespace).AppendLine();
            builder.AppendLine("{");
        }

        builder.Append(indent).Append(candidate.Accessibility).Append(" partial class ").Append(candidate.Symbol.Name).AppendLine();
        builder.Append(indent).AppendLine("{");

        builder.Append(memberIndent).AppendLine("public override global::Urho3D.Math.StringHash GetType()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeStatic();");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).AppendLine("public override global::Ea.String GetTypeName()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeNameStatic();");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).AppendLine("public override global::Urho3D.Core.TypeInfo GetTypeInfo()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeInfoStatic();");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).AppendLine("public override bool IsInstanceOf(global::Urho3D.Math.StringHash type)");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeInfoStatic().IsTypeOf(type);");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).Append("public ");
        if (candidate.HideStaticTypeNameMembers)
        {
            builder.Append("new ");
        }

        builder.AppendLine("static global::Urho3D.Math.StringHash GetTypeStatic()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeInfoStatic().GetType();");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).Append("public ");
        if (candidate.HideStaticTypeNameMembers)
        {
            builder.Append("new ");
        }

        builder.AppendLine("static global::Ea.String GetTypeNameStatic()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent).AppendLine("return GetTypeInfoStatic().GetTypeName();");
        builder.Append(memberIndent).AppendLine("}");
        builder.AppendLine();

        builder.Append(memberIndent).AppendLine("public new static global::Urho3D.Core.TypeInfo GetTypeInfoStatic()");
        builder.Append(memberIndent).AppendLine("{");
        builder.Append(bodyIndent)
            .Append("return global::SharpCpp.Rbfx.Bindings.Interop.ManagedTypeInfoRegistry.GetOrCreate(")
            .Append(EscapeStringLiteral(candidate.TypeName))
            .Append(", ")
            .Append(candidate.BaseTypeDisplay)
            .AppendLine(".GetTypeInfoStatic());");
        builder.Append(memberIndent).AppendLine("}");

        builder.Append(indent).AppendLine("}");

        if (candidate.Namespace is not null)
        {
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string EscapeStringLiteral(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private sealed class Candidate
    {
        public Candidate(
            INamedTypeSymbol symbol,
            string accessibility,
            string? @namespace,
            string typeName,
            string baseTypeDisplay,
            bool hideStaticTypeNameMembers,
            Location location)
        {
            Symbol = symbol;
            Accessibility = accessibility;
            Namespace = @namespace;
            TypeName = typeName;
            BaseTypeDisplay = baseTypeDisplay;
            HideStaticTypeNameMembers = hideStaticTypeNameMembers;
            Location = location;
        }

        public INamedTypeSymbol Symbol { get; }

        public string Accessibility { get; }

        public string? Namespace { get; }

        public string TypeName { get; }

        public string BaseTypeDisplay { get; }

        public bool HideStaticTypeNameMembers { get; }

        public Location Location { get; }
    }
}
