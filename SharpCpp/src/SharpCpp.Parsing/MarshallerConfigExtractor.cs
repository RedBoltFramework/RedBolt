using ClangSharp;
using ClangSharp.Interop;
using Microsoft.Extensions.Logging;
using SharpCpp.Model.Logging;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SharpCpp.Parsing;

/// <summary>
/// Replaces regex-only <c>LoadManagedTypeOverrides</c> and the regex-heavy portion of
/// <c>MarshallerMetadataPhase2</c> with a ClangSharp-backed pass that
/// walks the AST for Marshaller&lt;T&gt; / ReturnMarshaller&lt;T&gt; declarations and extracts
/// <c>[[sharpcpp::managed_type(...)]]</c> and <c>[[sharpcpp::expose_member(...)]]</c>
/// attributes from cursor properties, not from source-text regex.
///
/// The C++ compiler handles template resolution; regex is reduced to parsing
/// attribute body key=value pairs only.
/// </summary>
internal static class MarshallerConfigExtractor
{
    private static readonly MethodInfo GetOrCreateCursorMethod = typeof(TranslationUnit)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Single(static method => method.Name == "GetOrCreate" &&
            method.IsGenericMethodDefinition &&
            method.GetParameters() is [{ ParameterType: var parameterType }] &&
            parameterType == typeof(CXCursor))
        .MakeGenericMethod(typeof(Decl));

    // Key=value extractors (attribute body parsing only — NOT C++ code parsing).
    private static readonly Regex PublicTypeExpression = new(
        @"public_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UndefaultableExpression = new(
        @"undefaultable\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MarshallerTypeInAttributeExpression = new(
        @"marshaller_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ManagedMarshallerInAttributeExpression = new(
        @"managed_marshaller\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ManagedNamespaceInAttributeExpression = new(
        @"managed_namespace\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ProjectionExpression = new(
        @"projection\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CallbackExpression = new(
        @"callback\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FixedArrayExpression = new(
        @"fixed_array\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NativeArrayExpression = new(
        @"native_array\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DynamicArrayExpression = new(
        @"dynamic_array\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReturnOwnershipExpression = new(
        @"return_ownership\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ManagedInstanceCacheFactoryExpression = new(
        @"managed_instance_cache_factory\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ManagedInstanceCacheTypeExpression = new(
        @"managed_instance_cache_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReleaseStrategyExpression = new(
        @"release_strategy\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ShapeExpression = new(
        @"shape\s*=\s*\""(?<value>[^\""]+)\""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExposedExpression = new(
        @"exposed\s*=\s*(?<value>true|false)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);


    // ManagedTypeOverride record (same shape as ClangSharpHeaderParser's private record).
    internal sealed record ManagedTypeOverride(string CppTypeName, string PublicTypeName)
    {
        public bool Undefaultable { get; init; }
        public string? MarshallerTypeName { get; init; }
        public string? ManagedMarshaller { get; init; }
        public string? ManagedNamespace { get; init; }
        public bool HasManagedBaseType { get; init; }
        public bool HasExposedMembers { get; init; }
        public IReadOnlyList<string> TemplateParameterNames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<int> VisibleTemplateParameterIndices { get; init; } = Array.Empty<int>();
        public bool Projection { get; init; }
        public bool Callback { get; init; }
        public bool FixedArray { get; init; }
        public bool NativeArray { get; init; }
        public bool DynamicArray { get; init; }
        public string? ReturnOwnership { get; init; }
        public string? ManagedInstanceCacheFactoryExpression { get; init; }
        public string? ManagedInstanceCacheTypeName { get; init; }
        public bool EmitDefaultNativeReleaseOverride { get; init; } = true;
        public bool HasPublicTypeMapping { get; init; } = true;
    }

    /// <summary>
    /// Lightweight representation of a <c>[[sharpcpp::expose_member(...)]]</c> member function
    /// extracted from the ClangSharp AST cursor, not from source-text regex.
    /// </summary>
    internal sealed record ExposedMemberInfo(
        string MethodName,
        string CppReturnType,
        bool IsConst,
        string Shape,
        bool Exposed,
        IReadOnlyList<(string Name, string CppType)> Parameters);

    /// <summary>
    /// Full attribute body + extracted exposed member info for a Marshaller specialization.
    /// Used by <c>MarshallerMetadataPhase2</c> for record-level back-patching.
    /// No struct body source text — member info comes from AST cursors.
    /// </summary>
    internal sealed record MarshallerBodyConfig(
        string AttributeBody,
        IReadOnlyList<ExposedMemberInfo> ExposedMembers,
        IReadOnlyList<string> TemplateParameterNames,
        string? ManagedInstanceCacheFactoryExpression = null,
        string? ManagedInstanceCacheTypeName = null,
        bool EmitDefaultNativeReleaseOverride = true);

    /// <summary>
    /// Parse marshaller configuration headers via ClangSharp and extract
    /// <c>[[sharpcpp::managed_type(...)]]</c> overrides from Marshaller&lt;T&gt;
    /// and ReturnMarshaller&lt;T&gt; specializations.
    /// </summary>
    public static IReadOnlyDictionary<string, ManagedTypeOverride> ExtractManagedTypeOverrides(
        IReadOnlyList<string> marshallerFiles,
        IReadOnlyList<string> clangArgs)
    {
        var result = new Dictionary<string, ManagedTypeOverride>(StringComparer.Ordinal);

        PerformManagedClangParse(marshallerFiles, clangArgs, rootDecl =>
        {
            var declIndex = BuildRecordLikeDeclIndex(rootDecl);
            WalkForMarshallerDeclarations(rootDecl, declIndex, result);
        });

        return result;
    }

    /// <summary>
    /// Extract Marshaller specialization attribute bodies and exposed member info
    /// from marshaller configuration headers. All method metadata comes from the
    /// ClangSharp AST cursors — no C++ source-text regex.
    /// </summary>
    public static IReadOnlyDictionary<string, MarshallerBodyConfig> ExtractMarshallerBodies(
        IReadOnlyList<string> marshallerFiles,
        IReadOnlyList<string> clangArgs)
    {
        var result = new Dictionary<string, MarshallerBodyConfig>(StringComparer.Ordinal);

        PerformManagedClangParse(marshallerFiles, clangArgs, rootDecl =>
        {
            var declIndex = BuildRecordLikeDeclIndex(rootDecl);
            WalkForMarshallerBodies(rootDecl, declIndex, result);
        });

        return result;
    }

    private static void PerformManagedClangParse(
        IReadOnlyList<string> marshallerFiles,
        IReadOnlyList<string> clangArgs,
        Action<Decl> action)
    {
        var tempCppPath = GenerateTempCpp(marshallerFiles);
        try
        {
            using var index = CXIndex.Create();

            // NOTE: No `using` on translationUnit — TranslationUnit.GetOrCreate() below
            // takes ownership of the native handle. Disposing the interop struct would
            // double-free: the managed TU's finalizer also calls clang_disposeTranslationUnit.
            var translationUnit = CXTranslationUnit.Parse(
                index,
                tempCppPath,
                clangArgs.ToArray(),
                Array.Empty<CXUnsavedFile>(),
                CXTranslationUnit_Flags.CXTranslationUnit_None);

            if (translationUnit.NumDiagnostics > 0)
            {
                for (uint diagIndex = 0; diagIndex < translationUnit.NumDiagnostics; diagIndex++)
                {
                    using var diagnostic = translationUnit.GetDiagnostic(diagIndex);
                    if (diagnostic.Severity == CXDiagnosticSeverity.CXDiagnostic_Error ||
                        diagnostic.Severity == CXDiagnosticSeverity.CXDiagnostic_Fatal)
                    {
                        Logger.LogWarning("Marshaller config parse error: {Error}",
                            diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
                    }
                }
            }

            // Get the managed TranslationUnit and its root Decl
            var managedTu = TranslationUnit.GetOrCreate(translationUnit);
            action(managedTu.TranslationUnitDecl);
        }
        finally
        {
            TryDeleteTempFile(tempCppPath);
        }
    }

    private static string GenerateTempCpp(IReadOnlyList<string> marshallerFiles)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sharpcpp_marshaller_config_{Guid.NewGuid():N}.cpp");
        using (var writer = new StreamWriter(tempPath))
        {
            writer.WriteLine("// Auto-generated by SharpCpp MarshallerConfigExtractor");
            foreach (var file in marshallerFiles)
            {
                var fullPath = Path.GetFullPath(file);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                var includePath = fullPath.Replace('\\', '/');
                writer.WriteLine($"#include \"{includePath}\"");
            }
        }

        return tempPath;
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    // ─── Type override extraction (Marshaller<T> / ReturnMarshaller<T>) ─────────

    private static void WalkForMarshallerDeclarations(
        Decl decl,
        IReadOnlyDictionary<string, Decl> declIndex,
        Dictionary<string, ManagedTypeOverride> result)
    {
        foreach (var child in EnumerateDescendantDecls(decl))
        {
            if (child is RecordDecl or ClassTemplateDecl or ClassTemplatePartialSpecializationDecl &&
                IsPotentialMarshallerDeclaration(child))
            {
                if (TryGetMarshallerTemplateKind(child, out var isReturn))
                {
                    ProcessMarshallerDeclaration(child, declIndex, isReturn, result);
                }
            }
        }
    }

    private static void ProcessMarshallerDeclaration(
        Decl decl,
        IReadOnlyDictionary<string, Decl> declIndex,
        bool isReturn,
        Dictionary<string, ManagedTypeOverride> result)
    {
        var sourceText = GetSourceText(decl.Handle.TranslationUnit, decl.Extent);

        var attrMatch = SharpCppAttributeScanner.Scan(sourceText)
            .FirstOrDefault(static match => string.Equals(match.Name, "managed_type", StringComparison.Ordinal));
        if (attrMatch is null)
        {
            return;
        }

        var body = attrMatch.Body;
        var publicTypeMatch = PublicTypeExpression.Match(body);
        var returnOwnershipMatch = ReturnOwnershipExpression.Match(body);
        var callbackMatch = CallbackExpression.Match(body);
        var isCallback = callbackMatch.Success &&
            string.Equals(callbackMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase);
        if (!publicTypeMatch.Success && !returnOwnershipMatch.Success && !isCallback)
        {
            return;
        }

        // Extract the C++ template argument type from cursor-extent source text.
        // GetSourceText preserves spaces between tokens so "const char*" stays correct.
        var cppTypeName = ExtractTemplateArgFromCursorType(decl, isReturn);
        if (string.IsNullOrWhiteSpace(cppTypeName))
        {
            return;
        }

        cppTypeName = NormalizeTypeName(cppTypeName);

        var key = isReturn ? "return:" + cppTypeName : cppTypeName;

        var undefaultableMatch = UndefaultableExpression.Match(body);
        var marshallerTypeMatch = MarshallerTypeInAttributeExpression.Match(body);
        var managedMarshallerMatch = ManagedMarshallerInAttributeExpression.Match(body);
        var managedNamespaceMatch = ManagedNamespaceInAttributeExpression.Match(body);
        var projectionMatch = ProjectionExpression.Match(body);
        var fixedArrayMatch = FixedArrayExpression.Match(body);
        var nativeArrayMatch = NativeArrayExpression.Match(body);
        var dynamicArrayMatch = DynamicArrayExpression.Match(body);
        var managedInstanceCacheFactoryMatch = ManagedInstanceCacheFactoryExpression.Match(body);
        var managedInstanceCacheTypeMatch = ManagedInstanceCacheTypeExpression.Match(body);
        var releaseStrategyMatch = ReleaseStrategyExpression.Match(body);
        var hasManagedBaseType = body.Contains("managed_base_type", StringComparison.Ordinal);
        var hasExposedMembers = ExtractExposedMembersFromDecl(decl, declIndex).Count > 0;
        var templateParameterNames = ExtractTemplateParameterNames(decl, cppTypeName);
        var publicTypeName = publicTypeMatch.Success ? publicTypeMatch.Groups["value"].Value : cppTypeName;
        var visibleTemplateParameterIndices = publicTypeMatch.Success
            ? ExtractVisibleTemplateParameterIndices(publicTypeName, templateParameterNames.Count)
            : Enumerable.Range(0, templateParameterNames.Count).ToArray();

        result[key] = new ManagedTypeOverride(
            cppTypeName,
            publicTypeName)
        {
            Undefaultable = undefaultableMatch.Success &&
                string.Equals(undefaultableMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase),
            MarshallerTypeName = marshallerTypeMatch.Success ? marshallerTypeMatch.Groups["value"].Value : null,
            ManagedMarshaller = managedMarshallerMatch.Success ? managedMarshallerMatch.Groups["value"].Value : null,
            ManagedNamespace = managedNamespaceMatch.Success ? managedNamespaceMatch.Groups["value"].Value : null,
            HasManagedBaseType = hasManagedBaseType,
            HasExposedMembers = hasExposedMembers,
            TemplateParameterNames = templateParameterNames,
            VisibleTemplateParameterIndices = visibleTemplateParameterIndices,
            Projection = projectionMatch.Success &&
                string.Equals(projectionMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase),
            Callback = isCallback,
            FixedArray = fixedArrayMatch.Success &&
                string.Equals(fixedArrayMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase),
            NativeArray = nativeArrayMatch.Success &&
                string.Equals(nativeArrayMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase),
            DynamicArray = dynamicArrayMatch.Success &&
                string.Equals(dynamicArrayMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase),
            ReturnOwnership = returnOwnershipMatch.Success ? returnOwnershipMatch.Groups["value"].Value : null,
            ManagedInstanceCacheFactoryExpression = managedInstanceCacheFactoryMatch.Success ? managedInstanceCacheFactoryMatch.Groups["value"].Value : null,
            ManagedInstanceCacheTypeName = managedInstanceCacheTypeMatch.Success ? managedInstanceCacheTypeMatch.Groups["value"].Value : null,
            EmitDefaultNativeReleaseOverride = !releaseStrategyMatch.Success ||
                !string.Equals(releaseStrategyMatch.Groups["value"].Value, "base", StringComparison.OrdinalIgnoreCase),
            HasPublicTypeMapping = publicTypeMatch.Success || isCallback
        };
    }

    // ─── Body extraction (attribute + exposed members from AST cursors) ─────────

    private static void WalkForMarshallerBodies(
        Decl decl,
        IReadOnlyDictionary<string, Decl> declIndex,
        Dictionary<string, MarshallerBodyConfig> result)
    {
        foreach (var child in EnumerateDescendantDecls(decl))
        {
            if (child is RecordDecl or ClassTemplateDecl or ClassTemplatePartialSpecializationDecl &&
                IsPotentialMarshallerDeclaration(child))
            {
                if (TryGetMarshallerTemplateKind(child, out var isReturn) && !isReturn)
                {
                    ProcessMarshallerBody(child, declIndex, result);
                }
            }
        }
    }

    private static void ProcessMarshallerBody(
        Decl decl,
        IReadOnlyDictionary<string, Decl> declIndex,
        Dictionary<string, MarshallerBodyConfig> result)
    {
        var sourceText = GetSourceText(decl.Handle.TranslationUnit, decl.Extent);

        var attrMatch = SharpCppAttributeScanner.Scan(sourceText)
            .FirstOrDefault(static match => string.Equals(match.Name, "managed_type", StringComparison.Ordinal));
        if (attrMatch is null)
        {
            return;
        }

        var body = attrMatch.Body;
        var managedInstanceCacheFactoryMatch = ManagedInstanceCacheFactoryExpression.Match(body);
        var managedInstanceCacheTypeMatch = ManagedInstanceCacheTypeExpression.Match(body);
        var releaseStrategyMatch = ReleaseStrategyExpression.Match(body);

        var cppTypeName = ExtractTemplateArgFromCursorType(decl, isReturn: false);
        if (string.IsNullOrWhiteSpace(cppTypeName))
        {
            return;
        }

        cppTypeName = NormalizeTypeName(cppTypeName);

        // Walk struct children for [[sharpcpp::expose_member(...)]] on member functions.
        var exposedMembers = ExtractExposedMembersFromDecl(decl, declIndex);

        result[cppTypeName] = new MarshallerBodyConfig(
            AttributeBody: body,
            ExposedMembers: exposedMembers,
            TemplateParameterNames: ExtractTemplateParameterNames(decl, cppTypeName),
            ManagedInstanceCacheFactoryExpression: managedInstanceCacheFactoryMatch.Success ? managedInstanceCacheFactoryMatch.Groups["value"].Value : null,
            ManagedInstanceCacheTypeName: managedInstanceCacheTypeMatch.Success ? managedInstanceCacheTypeMatch.Groups["value"].Value : null,
            EmitDefaultNativeReleaseOverride: !releaseStrategyMatch.Success ||
                !string.Equals(releaseStrategyMatch.Groups["value"].Value, "base", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ExposedMemberInfo> ExtractExposedMembersFromDecl(
        Decl structDecl,
        IReadOnlyDictionary<string, Decl> declIndex)
    {
        var members = new Dictionary<string, ExposedMemberInfo>(StringComparer.Ordinal);
        var visitedDecls = new HashSet<string>(StringComparer.Ordinal);
        CollectExposedMembers(structDecl, declIndex, members, visitedDecls);
        return members.Values.ToList();
    }

    private static void CollectExposedMembers(
        Decl decl,
        IReadOnlyDictionary<string, Decl> declIndex,
        IDictionary<string, ExposedMemberInfo> members,
        ISet<string> visitedDecls)
    {
        var recordDecl = ResolveRecordDecl(decl);
        if (recordDecl is null)
        {
            return;
        }

        var visitedKey = GetDeclLocationKey(recordDecl.Handle);
        if (visitedKey is not null && !visitedDecls.Add(visitedKey))
        {
            return;
        }

        if (recordDecl is CXXRecordDecl cxxRecordDecl)
        {
            foreach (var baseSpec in cxxRecordDecl.Bases)
            {
                if (!TryResolveBaseDecl(baseSpec, declIndex, out var baseDecl))
                {
                    continue;
                }

                CollectExposedMembers(baseDecl, declIndex, members, visitedDecls);
            }
        }

        foreach (var child in recordDecl.Decls)
        {
            if (child is not CXXMethodDecl methodDecl || !TryExtractExposedMember(methodDecl, out var member))
            {
                continue;
            }

            members[GetExposedMemberSignature(member)] = member;
        }
    }

    private static string GetExposedMemberSignature(ExposedMemberInfo member)
    {
        var effectiveParameters = member.Parameters.Skip(1).Select(static parameter => parameter.CppType);
        return $"{member.MethodName}({string.Join(',', effectiveParameters)})::{member.IsConst}";
    }

    private static bool TryExtractExposedMember(CXXMethodDecl methodDecl, out ExposedMemberInfo member)
    {
        var exposeMatch = SharpCppAttributeScanner.Scan(GetMethodAttributeSearchText(methodDecl))
            .FirstOrDefault(static match => string.Equals(match.Name, "expose_member", StringComparison.Ordinal));
        if (exposeMatch is null)
        {
            member = null!;
            return false;
        }

        var attrBody = exposeMatch.Body;
        var shape = ShapeExpression.Match(attrBody) is { Success: true } shapeMatch
            ? shapeMatch.Groups["value"].Value
            : "method";
        var exposed = ExposedExpression.Match(attrBody) is { Success: true } exposedMatch
            ? string.Equals(exposedMatch.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase)
            : true;

        var methodName = methodDecl.Name ?? methodDecl.Spelling.ToString();
        var parameters = new List<(string Name, string CppType)>();
        foreach (var param in methodDecl.Parameters)
        {
            var paramName = param.Name ?? param.Spelling.ToString();
            parameters.Add((paramName, NormalizeTypeName(param.Type.AsString)));
        }

        member = new ExposedMemberInfo(
            MethodName: methodName,
            CppReturnType: NormalizeTypeName(methodDecl.ReturnType.AsString),
            IsConst: methodDecl.IsConst,
            Shape: shape,
            Exposed: exposed,
            Parameters: parameters.AsReadOnly());
        return true;
    }

    private static string GetMethodAttributeSearchText(CXXMethodDecl methodDecl)
    {
        var sourceText = GetSourceText(methodDecl.Handle.TranslationUnit, methodDecl.Extent);
        if (SharpCppAttributeScanner.Scan(sourceText).Any(static match => string.Equals(match.Name, "expose_member", StringComparison.Ordinal)))
        {
            return sourceText;
        }

        methodDecl.Location.GetFileLocation(out var file, out var line, out _, out _);
        var filePath = file.Name.CString;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return sourceText;
        }

        var lines = File.ReadAllLines(filePath);
        var currentLineIndex = Math.Max(0, (int)line - 1);
        if (currentLineIndex >= lines.Length)
        {
            return sourceText;
        }

        var collectedLines = new List<string>();
        var sawExposeAttribute = false;
        for (var index = currentLineIndex - 1; index >= 0; index--)
        {
            var trimmed = lines[index].Trim();
            if (!sawExposeAttribute)
            {
                if (trimmed.Contains("[[sharpcpp::", StringComparison.Ordinal) || trimmed.Contains("]]", StringComparison.Ordinal))
                {
                    collectedLines.Insert(0, lines[index]);
                    sawExposeAttribute = true;
                    if (trimmed.Contains("[[sharpcpp::", StringComparison.Ordinal))
                    {
                        break;
                    }

                    continue;
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            collectedLines.Insert(0, lines[index]);
            if (trimmed.Contains("[[sharpcpp::", StringComparison.Ordinal))
            {
                break;
            }
        }

        if (collectedLines.Count == 0)
        {
            return sourceText;
        }

        collectedLines.Add(sourceText);
        return string.Join(Environment.NewLine, collectedLines);
    }

    private static IReadOnlyDictionary<string, Decl> BuildRecordLikeDeclIndex(Decl rootDecl)
    {
        var declIndex = new Dictionary<string, Decl>(StringComparer.Ordinal);
        IndexRecordLikeDecls(rootDecl, declIndex);
        return declIndex;
    }

    private static void IndexRecordLikeDecls(Decl decl, IDictionary<string, Decl> declIndex)
    {
        foreach (var child in EnumerateDescendantDecls(decl))
        {
            switch (child)
            {
                case ClassTemplateDecl classTemplateDecl:
                    AddDeclIndexEntry(classTemplateDecl, declIndex);
                    if (classTemplateDecl.TemplatedDecl is not null)
                    {
                        AddDeclIndexEntry(classTemplateDecl.TemplatedDecl, declIndex);
                    }
                    break;

                case RecordDecl:
                    AddDeclIndexEntry(child, declIndex);
                    break;
            }
        }
    }

    private static unsafe IEnumerable<Decl> EnumerateDescendantDecls(Cursor root)
    {
        var result = new List<Decl>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var translationUnit = TranslationUnit.GetOrCreate(root.Handle.TranslationUnit);

        root.Handle.VisitChildren((cursor, _, _) =>
        {
            if (!IsRecordLikeCursor(cursor))
            {
                return CXChildVisitResult.CXChildVisit_Recurse;
            }

            if (!TryCreateDecl(translationUnit, cursor, out var decl))
            {
                return CXChildVisitResult.CXChildVisit_Recurse;
            }

            var key = GetDeclLocationKey(decl.Handle) ?? decl.Handle.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (seen.Add(key))
            {
                result.Add(decl);
            }

            return CXChildVisitResult.CXChildVisit_Recurse;
        }, default);

        return result;
    }

    private static bool IsRecordLikeCursor(CXCursor cursor)
    {
        return cursor.Kind is CXCursorKind.CXCursor_StructDecl or
            CXCursorKind.CXCursor_ClassDecl or
            CXCursorKind.CXCursor_ClassTemplate or
            CXCursorKind.CXCursor_ClassTemplatePartialSpecialization;
    }

    private static bool TryCreateDecl(TranslationUnit translationUnit, CXCursor cursor, out Decl decl)
    {
        try
        {
            decl = (Decl)GetOrCreateCursorMethod.Invoke(translationUnit, [cursor])!;
            return true;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ArgumentOutOfRangeException or InvalidOperationException)
        {
            decl = null!;
            return false;
        }
    }

    private static bool IsPotentialMarshallerDeclaration(Decl decl)
    {
        if (decl is not NamedDecl namedDecl)
        {
            return false;
        }

        var name = namedDecl.Name ?? namedDecl.Spelling.ToString();
        return string.Equals(name, "Marshaller", StringComparison.Ordinal) ||
            string.Equals(name, "ReturnMarshaller", StringComparison.Ordinal);
    }

    private static void AddDeclIndexEntry(Decl decl, IDictionary<string, Decl> declIndex)
    {
        var locationKey = GetDeclLocationKey(decl.Handle);
        if (locationKey is not null)
        {
            declIndex[locationKey] = decl;
        }
    }

    private static bool TryResolveBaseDecl(
        CXXBaseSpecifier baseSpec,
        IReadOnlyDictionary<string, Decl> declIndex,
        out Decl baseDecl)
    {
        var locationKey = GetDeclLocationKey(baseSpec.Type.Handle.Declaration);
        if (locationKey is not null && declIndex.TryGetValue(locationKey, out baseDecl!))
        {
            return true;
        }

        baseDecl = null!;
        return false;
    }

    private static RecordDecl? ResolveRecordDecl(Decl decl)
    {
        return decl switch
        {
            ClassTemplateDecl classTemplateDecl => classTemplateDecl.TemplatedDecl as RecordDecl,
            _ => decl as RecordDecl
        };
    }

    private static string? GetDeclLocationKey(CXCursor cursor)
    {
        cursor.Location.GetFileLocation(out var file, out var line, out var column, out _);
        var filePath = file.Name.CString;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return $"{Path.GetFullPath(filePath)}:{line}:{column}";
    }

    private static IReadOnlyList<string> ExtractTemplateParameterNames(Decl decl, string cppTypeName)
    {
        if (decl is TemplateDecl templateDecl)
        {
            var parameters = new List<string>();
            foreach (var parameter in templateDecl.TemplateParameters)
            {
                if (parameter is TemplateTypeParmDecl typeParameter)
                {
                    var name = typeParameter.Name ?? typeParameter.Spelling.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        parameters.Add(name);
                    }
                }
                else if (parameter is NonTypeTemplateParmDecl nonTypeParameter)
                {
                    var name = nonTypeParameter.Name ?? nonTypeParameter.Spelling.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        parameters.Add(name);
                    }
                }
            }

            if (parameters.Count > 0)
            {
                return parameters;
            }
        }

        var sourceText = GetSourceText(decl.Handle.TranslationUnit, decl.Extent);
        if (TryExtractTemplateParameterNamesFromSource(sourceText, out var sourceParameters))
        {
            return sourceParameters;
        }

        var patternParameters = ExtractTemplateParameterNamesFromTypePattern(cppTypeName);
        return patternParameters.Count > 0
            ? patternParameters
            : Array.Empty<string>();
    }

    private static IReadOnlyList<int> ExtractVisibleTemplateParameterIndices(string publicTypeName, int parameterCount)
    {
        if (parameterCount == 0)
        {
            return Array.Empty<int>();
        }

        var usedIndices = new HashSet<int>();
        for (var index = 0; index < publicTypeName.Length; index++)
        {
            if (publicTypeName[index] != '{')
            {
                continue;
            }

            var closeIndex = publicTypeName.IndexOf('}', index + 1);
            if (closeIndex <= index + 1)
            {
                continue;
            }

            if (int.TryParse(publicTypeName[(index + 1)..closeIndex], out var placeholderIndex) &&
                placeholderIndex >= 0 &&
                placeholderIndex < parameterCount)
            {
                usedIndices.Add(placeholderIndex);
            }

            index = closeIndex;
        }

        return usedIndices.Count == 0
            ? Enumerable.Range(0, parameterCount).ToArray()
            : usedIndices.OrderBy(static index => index).ToArray();
    }

    private static IReadOnlyList<string> ExtractTemplateParameterNamesFromTypePattern(string cppTypeName)
    {
        var genericStart = cppTypeName.IndexOf('<');
        var genericEnd = cppTypeName.LastIndexOf('>');
        if (genericStart < 0 || genericEnd <= genericStart)
        {
            return Array.Empty<string>();
        }

        var argumentText = cppTypeName[(genericStart + 1)..genericEnd];

        var builtinTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool", "char", "signed char", "unsigned char", "short", "unsigned short",
            "int", "unsigned int", "long", "unsigned long", "long long", "unsigned long long",
            "float", "double", "size_t", "std::size_t", "void"
        };

        return SplitTopLevelTypeArguments(argumentText)
            .Where(static token => !token.Contains("::", StringComparison.Ordinal))
            .Where(static token => !token.Contains('<', StringComparison.Ordinal))
            .Where(token => !builtinTokens.Contains(token))
            .Where(static token => Regex.IsMatch(token, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
            .ToArray();
    }

    private static bool TryExtractTemplateParameterNamesFromSource(string sourceText, out IReadOnlyList<string> parameterNames)
    {
        parameterNames = Array.Empty<string>();
        var templateIndex = sourceText.IndexOf("template", StringComparison.Ordinal);
        if (templateIndex < 0)
        {
            return false;
        }

        var genericStart = sourceText.IndexOf('<', templateIndex);
        if (genericStart < 0)
        {
            return false;
        }

        var depth = 0;
        for (var index = genericStart; index < sourceText.Length; index++)
        {
            switch (sourceText[index])
            {
                case '<':
                    depth++;
                    break;

                case '>':
                    depth--;
                    if (depth == 0)
                    {
                        var parameterText = sourceText[(genericStart + 1)..index];
                        parameterNames = SplitTemplateParameterNames(parameterText);
                        return parameterNames.Count > 0;
                    }
                    break;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> SplitTemplateParameterNames(string parameterText)
    {
        return parameterText
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Trim())
            .Select(static part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> SplitTopLevelTypeArguments(string parameterText)
    {
        var result = new List<string>();
        var segmentStart = 0;
        var angleDepth = 0;
        var parenDepth = 0;

        for (var index = 0; index < parameterText.Length; index++)
        {
            switch (parameterText[index])
            {
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    angleDepth = Math.Max(0, angleDepth - 1);
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth = Math.Max(0, parenDepth - 1);
                    break;
                case ',' when angleDepth == 0 && parenDepth == 0:
                    result.Add(NormalizeTypeName(parameterText[segmentStart..index]));
                    segmentStart = index + 1;
                    break;
            }
        }

        result.Add(NormalizeTypeName(parameterText[segmentStart..]));
        return result;
    }

    private static readonly ILogger Logger = SharpCppLog.For(nameof(MarshallerConfigExtractor));

    // ─── Utilities ───────────────────────────────────────────────────────────

    private static string GetSourceText(CXTranslationUnit translationUnit, CXSourceRange sourceRange)
    {
        var tokens = translationUnit.Tokenize(sourceRange);
        try
        {
            var builder = new System.Text.StringBuilder();
            var first = true;
            foreach (var token in tokens)
            {
                if (!first)
                {
                    builder.Append(' ');
                }
                builder.Append(token.GetSpelling(translationUnit).ToString());
                first = false;
            }

            return builder.ToString();
        }
        finally
        {
            translationUnit.DisposeTokens(tokens);
        }
    }

    /// <summary>
    /// Extracts the template argument type from a Marshaller/ReturnMarsharker struct
    /// declaration cursor using the cursor's AST extent source text.
    ///
    /// For <c>template&lt;&gt; struct Marshaller&lt;const char*&gt; { ... };</c>,
    /// this returns <c>"const char*"</c> by matching <c>Marshaller&lt;...&gt;</c>
    /// in the cursor extent text (with spaces preserved between tokens).
    /// The cursor extent IS the AST's definitive representation of the node.
    /// </summary>
    private static string? ExtractTemplateArgFromCursorType(Decl decl, bool isReturn)
    {
        var sourceText = GetSourceText(decl.Handle.TranslationUnit, decl.Extent);
        var hasSourceTypeName = TryExtractTemplateArgumentFromSource(sourceText, isReturn, out var sourceTypeName);

        // Prefer Clang's desugared template-argument type when available so
        // marshaller metadata is keyed on the canonical container surface
        // (e.g. eastl::hash_map instead of the unordered_map template alias).
        if (decl is ClassTemplateSpecializationDecl specDecl)
        {
            var templateArgs = specDecl.TemplateArgs;
            if (templateArgs.Count > 0)
            {
                var typeName = templateArgs[0].AsType.AsString;
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var normalizedTypeName = NormalizeTypeName(typeName);
                    return hasSourceTypeName
                        ? QualifyDesugaredTypeName(normalizedTypeName, NormalizeTypeName(sourceTypeName))
                        : normalizedTypeName;
                }
            }
        }

        if (hasSourceTypeName)
        {
            return NormalizeTypeName(sourceTypeName);
        }

        return null;
    }

    private static string QualifyDesugaredTypeName(string desugaredTypeName, string sourceTypeName)
    {
        if (string.IsNullOrWhiteSpace(desugaredTypeName) ||
            desugaredTypeName.Contains("::", StringComparison.Ordinal))
        {
            return desugaredTypeName;
        }

        var sourceHead = sourceTypeName;
        var sourceGenericStart = sourceHead.IndexOf('<');
        if (sourceGenericStart >= 0)
        {
            sourceHead = sourceHead[..sourceGenericStart];
        }

        var sourceNamespaceSeparator = sourceHead.LastIndexOf("::", StringComparison.Ordinal);
        if (sourceNamespaceSeparator < 0)
        {
            return desugaredTypeName;
        }

        var sourceNamespace = sourceHead[..sourceNamespaceSeparator];
        return $"{sourceNamespace}::{desugaredTypeName}";
    }

    private static bool TryGetMarshallerTemplateKind(Decl decl, out bool isReturn)
    {
        var sourceText = GetSourceText(decl.Handle.TranslationUnit, decl.Extent);
        if (TryFindTemplateArgumentStart(sourceText, "ReturnMarshaller", out _))
        {
            isReturn = true;
            return true;
        }

        if (TryFindTemplateArgumentStart(sourceText, "Marshaller", out _))
        {
            isReturn = false;
            return true;
        }

        isReturn = false;
        return false;
    }

    private static bool TryExtractTemplateArgumentFromSource(string sourceText, bool isReturn, out string typeName)
    {
        typeName = string.Empty;
        var templateName = isReturn ? "ReturnMarshaller" : "Marshaller";
        if (!TryFindTemplateArgumentStart(sourceText, templateName, out var genericStart))
        {
            return false;
        }

        var depth = 0;
        for (var index = genericStart; index < sourceText.Length; index++)
        {
            switch (sourceText[index])
            {
                case '<':
                    depth++;
                    break;

                case '>':
                    depth--;
                    if (depth == 0)
                    {
                        typeName = sourceText[(genericStart + 1)..index];
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private static bool TryFindTemplateArgumentStart(string sourceText, string templateName, out int genericStart)
    {
        var searchIndex = 0;
        while (searchIndex < sourceText.Length)
        {
            var templateNameIndex = sourceText.IndexOf(templateName, searchIndex, StringComparison.Ordinal);
            if (templateNameIndex < 0)
            {
                break;
            }

            var nextIndex = templateNameIndex + templateName.Length;
            while (nextIndex < sourceText.Length && char.IsWhiteSpace(sourceText[nextIndex]))
            {
                nextIndex++;
            }

            if (nextIndex < sourceText.Length && sourceText[nextIndex] == '<')
            {
                genericStart = nextIndex;
                return true;
            }

            searchIndex = templateNameIndex + templateName.Length;
        }

        genericStart = -1;
        return false;
    }

    internal static string NormalizeTypeName(string spelling)
    {
        return Regex.Replace(spelling.Trim(), @"\s+", " ")
            .Replace("__cxx11::", string.Empty, StringComparison.Ordinal)
            .Replace(" *", "*", StringComparison.Ordinal)
            .Replace(" &", "&", StringComparison.Ordinal)
            .Replace(":: ", "::", StringComparison.Ordinal)
            .Replace(" ::", "::", StringComparison.Ordinal)
            .Replace(" <", "<", StringComparison.Ordinal)
            .Replace("> ", ">", StringComparison.Ordinal);
    }
}
