using System.Text.RegularExpressions;

using SharpCpp.Model.Ast;
using Microsoft.Extensions.Logging;
using SharpCpp.Model.Logging;

namespace SharpCpp.Parsing;

/// <summary>
/// Phase 2 post-parse pass that extracts record-level metadata from
/// <c>Marshaller&lt;T&gt;</c> / <c>ReturnMarshaller&lt;T&gt;</c> specializations
/// and back-patches the <c>BindingCompilation</c>.
///
/// All Marshaller declaration discovery and member function extraction comes
/// from the ClangSharp AST (via <c>MarshallerConfigExtractor</c>). Regex is
/// only used for attribute body key=value parsing — no C++ code structure regex.
/// </summary>
public static class MarshallerMetadataPhase2
{
    private static readonly Regex PublicTypeExpression = new(
        @"public_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MarshallerTypeExpression = new(
        @"marshaller_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly ILogger Logger = SharpCppLog.For(nameof(MarshallerMetadataPhase2));

    private static readonly Regex ManagedBaseTypeExpression = new(
        @"managed_base_type\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ManagedBaseTypeConstraintsExpression = new(
        @"managed_base_type_constraints\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ManagedStaticInterfacesExpression = new(
        @"managed_static_interfaces\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ManagedStaticInterfacesAbstractExpression = new(
        @"managed_static_interfaces_abstract\s*=\s*true",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuppressEmissionExpression = new(
        @"suppress_emission\s*=\s*true",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OwnedHandleExpression = new(
        @"owned_handle\s*=\s*true",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ManagedNamespaceExpression = new(
        @"managed_namespace\s*=\s*\""(?<value>[^\""\\]*(?:\\.[^\""\\]*)*)\""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Apply record-level metadata using ClangSharp AST extraction.
    /// </summary>
    public static void ApplyWithClang(
        BindingCompilation compilation,
        IEnumerable<string> marshallerFiles,
        IReadOnlyList<string> clangArgs)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(marshallerFiles);
        ArgumentNullException.ThrowIfNull(clangArgs);

        var recordsByNativeName = BuildRecordLookup(compilation.Records);

        var marshallerFilesList = marshallerFiles.Where(f => File.Exists(Path.GetFullPath(f))).ToArray();
        if (marshallerFilesList.Length == 0)
        {
            return;
        }

        var configs = MarshallerConfigExtractor.ExtractMarshallerBodies(marshallerFilesList, clangArgs);
        ApplyRecordMetadata(compilation, configs, recordsByNativeName);

        SyncPlaceholderRecords(compilation, recordsByNativeName);
        DeduplicateRecords(compilation, recordsByNativeName);
        ApplyManagedTypeFormats(compilation, recordsByNativeName);
        ApplyAliasSurfaces(compilation);
    }

    private static void ApplyRecordMetadata(
        BindingCompilation compilation,
        IReadOnlyDictionary<string, MarshallerConfigExtractor.MarshallerBodyConfig> configs,
        IDictionary<string, RecordBinding> recordsByNativeName)
    {
        foreach (var (cppTypeName, config) in configs)
        {
            var recordName = GetTargetRecordName(cppTypeName);
            if (string.IsNullOrWhiteSpace(recordName))
            {
                continue;
            }

            var record = FindRecordByName(recordsByNativeName, recordName);

            // When the record found is a generic template (e.g., BaseVector2<T>)
            // and the Marshaller config targets a concrete typedef (e.g.,
            // DoubleVector2 = BaseVector2<double>), Clang resolves the template
            // argument to the canonical type.  Skip the generic record and
            // create a synthetic one for the concrete type instead.
            if (record is not null && record.IsGeneric)
            {
                // Check cppTypeName from config — if it's a concrete (non-generic)
                // type, create a synthetic record for it.
                var configIsGeneric = cppTypeName.Contains('<', StringComparison.Ordinal);
                if (!configIsGeneric)
                {
                    record = null;
                }
            }
            if (record is null)
            {
                // Create a synthetic record when the Marshaller config
                // references a type with no RecordBinding in the compilation.
                // Gate on managed_base_type OR suppress_emission — both
                // indicate the type needs a wrapper class.
                if (!ManagedBaseTypeExpression.IsMatch(config.AttributeBody) &&
                    config.ExposedMembers.Count == 0 &&
                    !SuppressEmissionExpression.IsMatch(config.AttributeBody))
                {
                    continue;
                }

                // System header types (e.g., std::basic_string<char>) may not
                // have a RecordBinding in the compilation because the parser
                // only creates local placeholders.  Create a record so the
                // marshaller metadata can be applied and the wrapper class
                // emitted.
                // Derive a simple managed name from the native type name.
                // e.g. "std::basic_string" → "BasicString"
                var lastNs = recordName.LastIndexOf("::", StringComparison.Ordinal);
                var shortName = lastNs >= 0 ? recordName[(lastNs + 2)..] : recordName;
                var pascalName = string.Concat(
                    shortName.Split('_', StringSplitOptions.RemoveEmptyEntries)
                        .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
                record = new RecordBinding(
                    CreateRecordSymbolId(recordName),
                    recordName,
                    pascalName,
                    config.TemplateParameterNames.Count > 0 ? recordName : cppTypeName);
                record.SetMetadata(MetadataKeys.PlaceholderRecord, false);
                record.SetMetadata(MetadataKeys.ForwardDeclaration, false);
                ApplyTemplateParameters(record, config.TemplateParameterNames);
                // ManagedNamespace will be set below from managed_namespace.
                compilation.Members.Add(record);
                IndexRecordLookup(recordsByNativeName, record);
            }

            // When a Marshaller<T> attribute provides metadata for a
            // placeholder record (e.g., std::basic_string<char> from system
            // headers whose definition is not in the owned include tree),
            // promote the placeholder to a "real" record so the renderer
            // emits its wrapper class instead of degrading it.
            if (record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord))
            {
                record.SetMetadata(MetadataKeys.PlaceholderRecord, false);
                record.SetMetadata(MetadataKeys.ForwardDeclaration, false);
            }

            ApplyTemplateParameters(record, config.TemplateParameterNames);

            var body = config.AttributeBody;

            if (OwnedHandleExpression.IsMatch(body))
            {
                record.SetMetadata(MetadataKeys.MarshallerConfiguredRecord, true);
            }

            // public_type → ManagedTypeFormat / ManagedName
            if (PublicTypeExpression.Match(body) is { Success: true } publicTypeMatch)
            {
                var publicType = publicTypeMatch.Groups["value"].Value;
                // When suppress_emission is set on this record (hand-written
                // C# struct replaces the auto-generated wrapper), the
                // public_type from the Marshaller takes precedence over any
                // rename attribute — the two represent different types.
                var hasExplicitRename = record.GetMetadata<bool>(MetadataKeys.ExplicitManagedRename);
                var overrideRename = SuppressEmissionExpression.IsMatch(body) &&
                    !string.Equals(record.ManagedTypeFormat, publicType, StringComparison.Ordinal);

                if (!overrideRename &&
                    hasExplicitRename &&
                    record.ManagedTypeFormat is not null &&
                    !string.Equals(record.ManagedTypeFormat, publicType, StringComparison.Ordinal))
                {
                    Logger.LogWarning(
                        "Record '{NativeName}' has [[sharpcpp::rename(\"{ManagedTypeFormat}\")]] " +
                        "but marshaller declares managed_type(public_type = \"{PublicType}\"). " +
                        "The rename attribute takes precedence.",
                        record.NativeName, record.ManagedTypeFormat, publicType);
                }
                else if (record.ManagedTypeFormat is null || overrideRename)
                {
                    record.ManagedTypeFormat = publicType;
                    record.ManagedName = GetManagedTypeHead(publicType);
                }
            }

            // marshaller_type → HandleMarshallerTypeName
            if (MarshallerTypeExpression.Match(body) is { Success: true } marshallerTypeMatch)
            {
                record.HandleMarshallerTypeName = marshallerTypeMatch.Groups["value"].Value;
            }

            // suppress_emission = true → SuppressEmission metadata
            if (SuppressEmissionExpression.IsMatch(body))
            {
                record.SetMetadata(MetadataKeys.SuppressEmission, true);
                // Clear generic parameters so type references use the
                // simple non-generic name (e.g. "Vector3" not "Vector3<T>").
                // The hand-written C# struct replaces the auto-generated
                // generic wrapper.
                record.GenericParameters.Clear();
                record.SupportedGenericInstantiations.Clear();
            }

            // managed_namespace → ManagedNamespace
            if (ManagedNamespaceExpression.Match(body) is { Success: true } nsMatch)
            {
                record.ManagedNamespace = nsMatch.Groups["value"].Value;
            }

            // managed_base_type → ManagedBaseTypeName
            if (ManagedBaseTypeExpression.Match(body) is { Success: true } baseTypeMatch)
            {
                record.ManagedBaseTypeName = baseTypeMatch.Groups["value"].Value;
                record.ManagedTypeFormat ??= record.ManagedName;

                var simpleName = GetSimpleTypeName(record.ManagedTypeFormat ?? record.ManagedName);
                if (!string.Equals(record.ManagedName, simpleName, StringComparison.Ordinal))
                {
                    record.ManagedName = simpleName;
                }
            }

            // managed_base_type_constraints → ManagedBaseTypeConstraints
            if (ManagedBaseTypeConstraintsExpression.Match(body) is { Success: true } constraintsMatch)
            {
                record.ManagedBaseTypeConstraints = constraintsMatch.Groups["value"].Value;
            }

            // managed_static_interfaces → ManagedStaticInterfaces metadata
            if (ManagedStaticInterfacesExpression.Match(body) is { Success: true } staticInterfacesMatch)
            {
                record.SetMetadata(
                    MetadataKeys.ManagedStaticInterfaces,
                    SplitManagedTypeList(staticInterfacesMatch.Groups["value"].Value));
            }

            // managed_static_interfaces_abstract → root does not directly implement inherited static interfaces
            if (ManagedStaticInterfacesAbstractExpression.IsMatch(body))
            {
                record.SetMetadata(MetadataKeys.ManagedStaticInterfacesAbstract, true);
            }

            if (!string.IsNullOrWhiteSpace(config.ManagedInstanceCacheFactoryExpression))
            {
                record.ManagedInstanceCacheFactoryExpression = config.ManagedInstanceCacheFactoryExpression;
            }

            if (!string.IsNullOrWhiteSpace(config.ManagedInstanceCacheTypeName))
            {
                record.ManagedInstanceCacheTypeName = config.ManagedInstanceCacheTypeName;
            }

            if (!config.EmitDefaultNativeReleaseOverride)
            {
                record.EmitDefaultNativeReleaseOverride = false;
            }

            // expose_member methods — metadata comes from AST cursors, not source regex.
            foreach (var member in config.ExposedMembers)
            {
                var managedReturnType = MapExposedMemberType(member.CppReturnType, record, recordsByNativeName) ?? BindingType.Void;

                var function = new FunctionBinding(
                    $"method:{record.NativeName}:{member.MethodName}",
                    member.MethodName,
                    member.MethodName,
                    managedReturnType,
                    $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.NativeName)}_{member.MethodName}",
                    member.CppReturnType,
                    $"{record.CppQualifiedName}::{member.MethodName}")
                {
                    IsStatic = false,
                    AccessModifier = member.Shape == "protected_override" ? BindingAccessModifier.Protected : BindingAccessModifier.Public,
                    IsConstMethod = member.IsConst && member.Parameters.Count == 0,
                    IsOverride = member.Shape == "protected_override",
                    OwningRecordCppTypeName = record.CppQualifiedName
                };
                function.SetMetadata(MetadataKeys.MarshallerExposedMethod, cppTypeName);

                // Skip the first parameter (self-reference passed by the Marshaller infrastructure).
                foreach (var (paramName, paramCppType) in member.Parameters.Skip(1))
                {
                    function.Parameters.Add(new ParameterBinding(
                        $"parameter:{record.NativeName}:{member.MethodName}:{paramName}",
                        paramName,
                        paramName,
                        MapExposedMemberType(paramCppType, record, recordsByNativeName) ?? BindingType.Void,
                        BindingParameterDirection.In,
                        BindingParameterAbiKind.Value,
                        paramCppType));
                }

                record.Members.Add(function);
            }

            AddDefaultConstructorForConfiguredWrapper(compilation, record, cppTypeName, config);
        }
    }

    private static void AddDefaultConstructorForConfiguredWrapper(
        BindingCompilation compilation,
        RecordBinding record,
        string cppTypeName,
        MarshallerConfigExtractor.MarshallerBodyConfig config)
    {
        if (config.ExposedMembers.Count == 0 ||
            string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.IsConstructor && member.Parameters.Count == 0))
        {
            return;
        }

        var constructor = new FunctionBinding(
            $"ctor:{record.NativeName}:marshaller-default",
            record.NativeName,
            record.ManagedName,
            record.HandleType,
            $"{compilation.EntryPointPrefix}_{SanitizeNativeEntrypointName(record.NativeName)}_ctor",
            $"{record.CppQualifiedName}*",
            $"{record.CppQualifiedName}::{record.NativeName}")
        {
            IsStatic = false,
            AccessModifier = BindingAccessModifier.Public,
            IsConstructor = true,
            OwningRecordCppTypeName = record.CppQualifiedName
        };
        constructor.SetMetadata(MetadataKeys.MarshallerExposedMethod, cppTypeName);
        record.Members.Add(constructor);
    }

    private static RecordBinding? FindRecordByName(
        IDictionary<string, RecordBinding> recordsByNativeName,
        string recordName)
    {
        if (recordsByNativeName.TryGetValue(recordName, out var record))
        {
            return record;
        }

        record = recordsByNativeName.Values.FirstOrDefault(candidate =>
            string.Equals(StripGenericArguments(candidate.CppQualifiedName), recordName, StringComparison.Ordinal) ||
            string.Equals(candidate.CppQualifiedName, recordName, StringComparison.Ordinal));
        if (record is not null)
        {
            return record;
        }

        var separatorIndex = recordName.LastIndexOf("::", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return recordsByNativeName.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.CppQualifiedName, recordName, StringComparison.Ordinal) ||
                (candidate.NativeName.StartsWith(recordName, StringComparison.Ordinal) &&
                 (candidate.NativeName.Length == recordName.Length || candidate.NativeName[recordName.Length] == '<')));
        }

        var unqualified = separatorIndex >= 0 ? recordName[(separatorIndex + 2)..] : recordName;
        if (recordsByNativeName.TryGetValue(unqualified, out record))
        {
            return record;
        }

        return null;
    }

    private static Dictionary<string, RecordBinding> BuildRecordLookup(IEnumerable<RecordBinding> records)
    {
        var result = new Dictionary<string, RecordBinding>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            IndexRecordLookup(result, record);
        }

        return result;
    }

    private static void IndexRecordLookup(IDictionary<string, RecordBinding> recordsByNativeName, RecordBinding record)
    {
        if (!string.IsNullOrWhiteSpace(record.CppQualifiedName))
        {
            recordsByNativeName[record.CppQualifiedName] = record;
            recordsByNativeName[StripGenericArguments(record.CppQualifiedName)] = record;
        }

        if (!string.IsNullOrWhiteSpace(record.NativeName))
        {
            recordsByNativeName.TryAdd(record.NativeName, record);
        }
    }

    private static void SyncPlaceholderRecords(BindingCompilation compilation, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        foreach (var placeholder in compilation.Records.Where(r => r.GetMetadata<bool>(MetadataKeys.PlaceholderRecord)).ToArray())
        {
            var realRecord = recordsByNativeName.Values.FirstOrDefault(r =>
                !r.GetMetadata<bool>(MetadataKeys.PlaceholderRecord) &&
                (string.Equals(r.CppQualifiedName, placeholder.CppQualifiedName, StringComparison.Ordinal) ||
                 string.Equals(StripGenericArguments(r.CppQualifiedName), StripGenericArguments(placeholder.CppQualifiedName), StringComparison.Ordinal)));
            if (realRecord is null)
            {
                continue;
            }

            if (ShouldKeepDistinctManagedSurface(placeholder, realRecord))
            {
                placeholder.SetMetadata(MetadataKeys.PlaceholderRecord, false);
                placeholder.SetMetadata(MetadataKeys.ForwardDeclaration, false);
                continue;
            }

            realRecord.ManagedBaseTypeName ??= placeholder.ManagedBaseTypeName;
            realRecord.ManagedBaseTypeConstraints ??= placeholder.ManagedBaseTypeConstraints;
            realRecord.ManagedTypeFormat ??= placeholder.ManagedTypeFormat;
            if (!string.Equals(realRecord.ManagedName, placeholder.ManagedName, StringComparison.Ordinal) &&
                realRecord.ManagedTypeFormat is null)
            {
                realRecord.ManagedName = placeholder.ManagedName;
            }
            realRecord.HandleMarshallerTypeName ??= placeholder.HandleMarshallerTypeName;
            realRecord.ManagedInstanceCacheFactoryExpression ??= placeholder.ManagedInstanceCacheFactoryExpression;
            realRecord.ManagedInstanceCacheTypeName ??= placeholder.ManagedInstanceCacheTypeName;
            realRecord.EmitDefaultNativeReleaseOverride &= placeholder.EmitDefaultNativeReleaseOverride;
            realRecord.HasCopyConstructor |= placeholder.HasCopyConstructor;
            MergeMetadataList(realRecord, placeholder, MetadataKeys.ManagedStaticInterfaces);
            MergeMetadataFlag(realRecord, placeholder, MetadataKeys.ManagedStaticInterfacesAbstract);

            foreach (var member in placeholder.Members.OfType<FunctionBinding>().ToArray())
            {
                if (!realRecord.Members.OfType<FunctionBinding>().Any(m =>
                        string.Equals(m.ManagedName, member.ManagedName, StringComparison.Ordinal)))
                {
                    placeholder.Members.Remove(member);
                    realRecord.Members.Add(member);
                }
            }

            placeholder.SetMetadata(MetadataKeys.SuppressEmission, true);
        }
    }

    private static bool ShouldKeepDistinctManagedSurface(RecordBinding placeholder, RecordBinding realRecord)
    {
        if (!IsConfiguredManagedSurface(placeholder))
        {
            return false;
        }

        var placeholderNamespace = placeholder.ManagedNamespace ?? string.Empty;
        var realNamespace = realRecord.ManagedNamespace ?? string.Empty;
        var placeholderType = placeholder.ManagedTypeFormat ?? placeholder.ManagedName;
        var realType = realRecord.ManagedTypeFormat ?? realRecord.ManagedName;
        return !string.Equals(placeholderNamespace, realNamespace, StringComparison.Ordinal) ||
            !string.Equals(placeholderType, realType, StringComparison.Ordinal);
    }

    private static bool IsConfiguredManagedSurface(RecordBinding record)
    {
        return !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            !string.IsNullOrWhiteSpace(record.ManagedBaseTypeConstraints) ||
            !string.IsNullOrWhiteSpace(record.ManagedTypeFormat) ||
            !string.IsNullOrWhiteSpace(record.ManagedNamespace) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static void DeduplicateRecords(BindingCompilation compilation, IDictionary<string, RecordBinding> recordsByNativeName)
    {
        var duplicateGroups = compilation.Records
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .ToArray();

        if (duplicateGroups.Length == 0)
        {
            return;
        }

        foreach (var group in duplicateGroups)
        {
            var preferred = group
                .OrderByDescending(GetRecordSpecificityScore)
                .ThenByDescending(static record => record.Members.Count)
                .First();

            foreach (var duplicate in group.Where(record => !ReferenceEquals(record, preferred)))
            {
                MergeRecordInto(preferred, duplicate);
                duplicate.SetMetadata(MetadataKeys.SuppressEmission, true);
                duplicate.Detach();
            }

            IndexRecordLookup(recordsByNativeName, preferred);
        }

        compilation.InvalidateCaches();
    }

    private static int GetRecordSpecificityScore(RecordBinding record)
    {
        var score = 0;

        if (!record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(record.ManagedBaseTypeName))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(record.ManagedBaseTypeConstraints))
        {
            score += 5;
        }

        score += record.Members.Count;
        score += record.GenericParameters.Count;

        return score;
    }

    private static void MergeRecordInto(RecordBinding target, RecordBinding source)
    {
        target.ManagedBaseTypeName ??= source.ManagedBaseTypeName;
        target.ManagedBaseTypeConstraints ??= source.ManagedBaseTypeConstraints;
        target.ManagedTypeFormat ??= source.ManagedTypeFormat;
        target.ManagedNamespace ??= source.ManagedNamespace;
        target.HandleMarshallerTypeName ??= source.HandleMarshallerTypeName;
        target.ManagedInstanceCacheFactoryExpression ??= source.ManagedInstanceCacheFactoryExpression;
        target.ManagedInstanceCacheTypeName ??= source.ManagedInstanceCacheTypeName;
        target.EmitDefaultNativeReleaseOverride &= source.EmitDefaultNativeReleaseOverride;
        target.HasCopyConstructor |= source.HasCopyConstructor;
        MergeMetadataList(target, source, MetadataKeys.ManagedStaticInterfaces);
        MergeMetadataFlag(target, source, MetadataKeys.ManagedStaticInterfacesAbstract);

        if (string.IsNullOrWhiteSpace(target.CppQualifiedName) && !string.IsNullOrWhiteSpace(source.CppQualifiedName))
        {
            target.CppQualifiedName = source.CppQualifiedName;
        }

        if (target.GenericParameters.Count == 0)
        {
            foreach (var parameter in source.GenericParameters)
            {
                target.GenericParameters.Add(new BindingGenericParameter(parameter.Name, parameter.Position));
            }
        }

        foreach (var member in source.Members.OfType<BindingNode>().ToArray())
        {
            if (member is FunctionBinding sourceFunction &&
                target.Members.OfType<FunctionBinding>().Any(candidate =>
                    string.Equals(candidate.ManagedName, sourceFunction.ManagedName, StringComparison.Ordinal) &&
                    string.Equals(candidate.CppQualifiedName, sourceFunction.CppQualifiedName, StringComparison.Ordinal)))
            {
                continue;
            }

            member.Detach();
            target.Members.Add(member);
        }

        foreach (var metadata in source.Metadata)
        {
            if (string.Equals(metadata.Key, MetadataKeys.SuppressEmission, StringComparison.Ordinal))
            {
                continue;
            }

            if (!target.HasMetadata(metadata.Key))
            {
                target.SetMetadata(metadata.Key, metadata.Value);
            }
        }
    }

    private static string GetTargetRecordName(string target)
    {
        var withoutPointer = target.TrimEnd('*').Trim();
        var genericStart = withoutPointer.IndexOf('<');
        return genericStart >= 0 ? withoutPointer[..genericStart] : withoutPointer;
    }

    private static string GetManagedTypeHead(string managedTypeFormat)
    {
        // Strip C++ namespace qualifiers (::) — only the local type name
        // belongs in the managed head.  Namespace placement is handled
        // separately via ManagedNamespace.
        var lastColon = managedTypeFormat.LastIndexOf("::", StringComparison.Ordinal);
        var localName = lastColon >= 0 ? managedTypeFormat[(lastColon + 2)..] : managedTypeFormat;

        var genericStart = localName.IndexOf('<');
        return genericStart >= 0 ? localName[..genericStart] : localName;
    }

    private static IReadOnlyList<string> SplitManagedTypeList(string value)
    {
        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void MergeMetadataList(BindingNode target, BindingNode source, string key)
    {
        var sourceValues = source.GetMetadata<IReadOnlyList<string>>(key);
        if (sourceValues is null || sourceValues.Count == 0)
        {
            return;
        }

        var merged = target.GetMetadata<IReadOnlyList<string>>(key) is { Count: > 0 } existing
            ? existing.Concat(sourceValues).Distinct(StringComparer.Ordinal).ToArray()
            : sourceValues.Distinct(StringComparer.Ordinal).ToArray();
        target.SetMetadata(key, merged);
    }

    private static void MergeMetadataFlag(BindingNode target, BindingNode source, string key)
    {
        if (source.GetMetadata<bool>(key))
        {
            target.SetMetadata(key, true);
        }
    }

    private static string GetSimpleTypeName(string managedTypeFormat)
    {
        var typeHead = StripGenericArguments(managedTypeFormat);
        var lastDot = typeHead.LastIndexOf('.');
        return lastDot >= 0 ? typeHead[(lastDot + 1)..] : typeHead;
    }

    private static void ApplyTemplateParameters(RecordBinding record, IReadOnlyList<string> templateParameterNames)
    {
        if (templateParameterNames.Count == 0)
        {
            return;
        }

        record.CppQualifiedName = StripGenericArguments(record.CppQualifiedName);

        if (record.GenericParameters.Count > 0)
        {
            return;
        }

        foreach (var (name, index) in templateParameterNames.Select((name, index) => (name, index)))
        {
            record.GenericParameters.Add(new BindingGenericParameter(name, index));
        }
    }

    private static BindingType? MapExposedMemberType(
        string cppType,
        RecordBinding record,
        IDictionary<string, RecordBinding> recordsByNativeName)
    {
        var normalized = MarshallerConfigExtractor.NormalizeTypeName(cppType);
        var stripped = StripExposedTypeQualifiers(normalized);

        if (normalized.EndsWith("*", StringComparison.Ordinal) &&
            stripped is "char" or "signed char" or "unsigned char")
        {
            return BindingType.IntPtr;
        }

        var genericParameter = record.GenericParameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, stripped, StringComparison.Ordinal));
        if (genericParameter is not null)
        {
            return BindingType.CreateGenericParameter(genericParameter.Name);
        }

        var primitiveType = MapManagedPrimitiveType(stripped);
        if (primitiveType is not null)
        {
            return primitiveType;
        }

        var recordMatch = FindRecordByName(recordsByNativeName, StripGenericArguments(stripped));
        if (recordMatch is not null)
        {
            return BindingType.CreateHandle(
                normalized.EndsWith("*", StringComparison.Ordinal) ? normalized : $"{normalized}*",
                recordMatch.ManagedName,
                recordMatch.SymbolId,
                recordMatch.HandleMarshallerTypeName);
        }

        return null;
    }

    private static string StripExposedTypeQualifiers(string cppType)
    {
        var stripped = cppType;
        if (stripped.StartsWith("const ", StringComparison.Ordinal))
        {
            stripped = stripped["const ".Length..].TrimStart();
        }

        while (stripped.EndsWith("&", StringComparison.Ordinal) || stripped.EndsWith("*", StringComparison.Ordinal))
        {
            stripped = stripped[..^1].TrimEnd();
        }

        return stripped;
    }

    private static BindingType? MapManagedPrimitiveType(string cppType)
    {
        var normalized = MarshallerConfigExtractor.NormalizeTypeName(cppType);
        return normalized switch
        {
            "void" => BindingType.Void,
            "bool" => BindingType.Boolean,
            "char" => BindingType.Int8,
            "unsigned char" => BindingType.UInt8,
            "short" => BindingType.Int16,
            "unsigned short" => BindingType.UInt16,
            "int" => BindingType.Int32,
            "unsigned int" => BindingType.UInt32,
            "long" => BindingType.Int64,
            "unsigned long" => BindingType.UInt64,
            "long long" => BindingType.Int64,
            "unsigned long long" => BindingType.UInt64,
            "float" => BindingType.Single,
            "double" => BindingType.Double,
            "std::size_t" => BindingType.UInt64,
            _ => null
        };
    }

    private static string SanitizeNativeEntrypointName(string name)
    {
        return name
            .Replace("::", "_", StringComparison.Ordinal)
            .Replace("<", "_", StringComparison.Ordinal)
            .Replace(">", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
    }

    private static void ApplyManagedTypeFormats(BindingCompilation compilation, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        // Build alias lookup for stamping generic argument names during
        // suppressed-emission type conversion.
        var aliasByTarget = new Dictionary<BindingAliasTargetKey, string>();
        foreach (var alias in compilation.TypeAliases)
        {
            var targetKey = BindingAliasTargetKey.From(alias.TargetType);
            if (!aliasByTarget.ContainsKey(targetKey))
            {
                aliasByTarget[targetKey] = alias.ManagedName;
            }
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = ApplyManagedTypeFormat(function.ReturnType, recordsByNativeName, aliasByTarget);
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                function.Parameters[i].Type = ApplyManagedTypeFormat(function.Parameters[i].Type, recordsByNativeName, aliasByTarget);
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>())
        {
            field.Type = ApplyManagedTypeFormat(field.Type, recordsByNativeName, aliasByTarget);
        }

        foreach (var method in compilation.Records.SelectMany(static record => record.Members.OfType<FunctionBinding>()))
        {
            method.ReturnType = ApplyManagedTypeFormat(method.ReturnType, recordsByNativeName, aliasByTarget);
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                method.Parameters[i].Type = ApplyManagedTypeFormat(method.Parameters[i].Type, recordsByNativeName, aliasByTarget);
            }
        }

        // Update alias target types with record managed type formats, so the
        // generated "using Alias = TargetType;" emits correctly formatted
        // target type names (e.g. "Std.Vector<Std.String>").
        for (var i = 0; i < compilation.TypeAliases.Count; i++)
        {
            var alias = compilation.TypeAliases[i];
            var updatedTarget = ApplyManagedTypeFormat(alias.TargetType, recordsByNativeName, aliasByTarget);
            if (!ReferenceEquals(updatedTarget, alias.TargetType))
            {
                compilation.TypeAliases[i] = alias with { TargetType = updatedTarget };
            }
        }

        // Prune aliases that would produce invalid generated C#.
        for (var i = compilation.TypeAliases.Count - 1; i >= 0; i--)
        {
            var alias = compilation.TypeAliases[i];
            if (ContainsGenericParameter(alias.TargetType) ||
                !CanRenderAliasTarget(alias.TargetType, recordsByNativeName))
            {
                compilation.TypeAliases.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Stamps alias surface names on function/method parameter and return
    /// types whose underlying managed type identity matches a registered
    /// alias target.  Matching by fully-qualified <c>ManagedTypeName</c>
    /// ensures each closed generic instantiation gets its own alias.
    /// </summary>
    public static void ApplyAliasSurfaces(BindingCompilation compilation)
    {
        if (compilation.TypeAliases.Count == 0)
        {
            return;
        }

        // Build a lookup from target ManagedTypeName → alias ManagedName.
        var aliasByTarget = new Dictionary<BindingAliasTargetKey, string>();
        var aliasByCppType = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in compilation.TypeAliases)
        {
            var targetKey = BindingAliasTargetKey.From(alias.TargetType);
            if (!aliasByTarget.ContainsKey(targetKey))
            {
                aliasByTarget[targetKey] = alias.ManagedName;
            }

            if (!aliasByCppType.ContainsKey(alias.TargetType.CppTypeName))
            {
                aliasByCppType[alias.TargetType.CppTypeName] = alias.ManagedName;
            }
        }

        if (aliasByTarget.Count == 0 && aliasByCppType.Count == 0)
        {
            return;
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = StampAlias(function.ReturnType, aliasByTarget, aliasByCppType);
            for (var i = 0; i < function.Parameters.Count; i++)
            {
                function.Parameters[i].Type = StampAlias(function.Parameters[i].Type, aliasByTarget, aliasByCppType);
            }
        }

        foreach (var method in compilation.Records.SelectMany(static record => record.Members.OfType<FunctionBinding>()))
        {
            method.ReturnType = StampAlias(method.ReturnType, aliasByTarget, aliasByCppType);
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                method.Parameters[i].Type = StampAlias(method.Parameters[i].Type, aliasByTarget, aliasByCppType);
            }
        }

        // Stamp aliases on generic instantiation type arguments.
        foreach (var record in compilation.Records)
        {
            foreach (var instantiation in record.SupportedGenericInstantiations)
            {
                for (var i = 0; i < instantiation.TypeArguments.Count; i++)
                {
                    instantiation.UpdateTypeArgument(i, StampAlias(instantiation.TypeArguments[i], aliasByTarget, aliasByCppType));
                }
            }
        }
    }

    private static BindingType StampAlias(BindingType type, IReadOnlyDictionary<BindingAliasTargetKey, string> aliasByTarget)
    {
        return StampAlias(type, aliasByTarget, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static BindingType StampAlias(
        BindingType type,
        IReadOnlyDictionary<BindingAliasTargetKey, string> aliasByTarget,
        IReadOnlyDictionary<string, string> aliasByCppType)
    {
        if (type.Kind == BindingTypeKind.Primitive && type.PrimitiveKind != BindingPrimitiveKind.Unsupported)
        {
            return type;
        }

        if (type.IsCallback)
        {
            var callbackReturnType = type.CallbackReturnType is null
                ? null
                : StampAlias(type.CallbackReturnType, aliasByTarget, aliasByCppType);
            var callbackParameterTypes = type.CallbackParameterTypes
                .Select(parameterType => StampAlias(parameterType, aliasByTarget, aliasByCppType))
                .ToArray();
            if (callbackReturnType is not null &&
                (!ReferenceEquals(callbackReturnType, type.CallbackReturnType) ||
                 !callbackParameterTypes.SequenceEqual(type.CallbackParameterTypes)))
            {
                type = type.WithCallbackSignature(callbackReturnType, callbackParameterTypes);
            }
        }
        else if (type.GenericArguments.Count > 0)
        {
            var genericArguments = type.GenericArguments
                .Select(argument => StampAlias(argument, aliasByTarget, aliasByCppType))
                .ToArray();
            if (!genericArguments.SequenceEqual(type.GenericArguments))
            {
                type = type.WithGenericArguments(genericArguments);
            }
        }

        var targetKey = BindingAliasTargetKey.From(type.WithManagedTypeFormatOverride(null));
        if ((aliasByTarget.TryGetValue(targetKey, out var aliasName) ||
                aliasByCppType.TryGetValue(type.CppTypeName, out aliasName)) &&
            !string.Equals(aliasName, type.ManagedTypeName, StringComparison.Ordinal))
        {
            type = type.WithManagedTypeFormatOverride(aliasName);
        }

        return type;
    }

    private static bool ContainsGenericParameter(BindingType type)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return true;
        }

        foreach (var argument in type.GenericArguments)
        {
            if (ContainsGenericParameter(argument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanRenderAliasTarget(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return false;
        }

        if (type.Kind is BindingTypeKind.Primitive or BindingTypeKind.Enum)
        {
            return true;
        }

        if (type.Kind != BindingTypeKind.Handle || string.IsNullOrWhiteSpace(type.RecordId))
        {
            return false;
        }

        var record = FindRecordByType(type, recordsByNativeName);
        if (record is not null && type.GenericArguments.Count > 0 && !ContainsGenericParameter(type))
        {
            return true;
        }

        return record is not null &&
            (!record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord) ||
                !string.IsNullOrWhiteSpace(record.ManagedTypeFormat) ||
                !string.IsNullOrWhiteSpace(record.ManagedNamespace) ||
                !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName));
    }

    private static BindingType ApplyManagedTypeFormat(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName, IReadOnlyDictionary<BindingAliasTargetKey, string> aliasByTarget)
    {
        if (type.Kind != BindingTypeKind.Handle || string.IsNullOrWhiteSpace(type.RecordId))
        {
            return type;
        }

        var formattedGenericArguments = new List<BindingType>(type.GenericArguments.Count);
        var genericArgumentsChanged = false;
        foreach (var genericArg in type.GenericArguments)
        {
            StampAlias(genericArg, aliasByTarget);
            var formattedGenericArg = ApplyManagedTypeFormat(genericArg, recordsByNativeName, aliasByTarget);
            formattedGenericArguments.Add(formattedGenericArg);
            genericArgumentsChanged |= !ReferenceEquals(formattedGenericArg, genericArg);
        }

        if (genericArgumentsChanged)
        {
            type = WithGenericArguments(type, formattedGenericArguments);
        }

        var record = FindRecordByType(type, recordsByNativeName);

        if (record is null)
        {
            return type;
        }

        type = RebindRecordId(type, record);

        // When the backing record is suppressed (hand-written C# struct
        // replaces the auto-generated wrapper), convert the Handle type to
        // a Primitive configured type so the renderer treats it as a
        // value type rather than a NativeObject-derived handle.
        if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
        {
            var publicName = record.ManagedTypeFormat ?? record.ManagedName;

            // Stamp the suppressed-emission name on the original handle type
            // so that when this type appears as a generic argument of an outer
            // type (e.g., Ea.Vector<IntVector3>), the outer type renders the
            // correct managed name even though the returned Configured type may
            // be discarded by recursive callers.
            type.SetManagedTypeFormatOverride(publicName);

            if (record.IsGeneric && type.GenericArguments.Count > 0)
            {
                // Stamp aliases on generic arguments so suppressed-emission
                // type conversion uses the alias name (e.g., IntVector3)
                // instead of the raw type name (BaseIntegerVector3<int>).
                foreach (var genericArg in type.GenericArguments)
                {
                    StampAlias(genericArg, aliasByTarget);
                }

                var argNames = type.GenericArguments
                    .Select(static argument => argument.ManagedTypeName)
                    .Cast<object>()
                    .ToArray();
                publicName = TryFormatManagedTypeFormat(publicName, argNames, out var formattedPublicName)
                    ? formattedPublicName
                    : $"{StripGenericArguments(publicName)}<{string.Join(", ", type.GenericArguments.Select(static argument => argument.ManagedTypeName))}>";
            }
            return BindingType.CreateConfigured(
                type.CppTypeName,
                publicName,
                record.HandleMarshallerTypeName);
        }

        type = ProjectManagedGenericArguments(type, record, recordsByNativeName);
        type.SetManagedTypeFormatOverride(GetManagedTypeFormatOverride(record));
        return type;
    }

    private static BindingType RebindRecordId(BindingType type, RecordBinding record)
    {
        if (string.Equals(type.RecordId, record.SymbolId, StringComparison.Ordinal))
        {
            return type;
        }

        var cppTypeName = type.Marshaller.CppIsRecordValue
            ? record.FormatCppTypeName(type.GenericArguments)
            : type.CppTypeName;

        var rebound = BindingType.CreateHandle(
            cppTypeName,
            type.Marshaller.PublicTypeName,
            record.SymbolId,
            type.MarshallerTypeName,
            type.GenericArguments,
            type.Marshaller.CppIsRecordValue);
        rebound.SetManagedTypeFormatOverride(type.ManagedTypeFormatOverride);
        return rebound;
    }

    private static BindingType ProjectManagedGenericArguments(
        BindingType type,
        RecordBinding record,
        IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        var visibleIndices = GetVisibleManagedGenericArgumentIndices(record);
        if (visibleIndices.Count == 0)
        {
            return type;
        }

        var sourceArguments = type.GenericArguments;
        var sourceArgumentsChanged = false;
        if (TryRecoverGenericArgumentsFromCppType(type.CppTypeName, recordsByNativeName, out var recoveredArguments) &&
            recoveredArguments.Count >= sourceArguments.Count)
        {
            sourceArguments = MergeRecoveredGenericArguments(
                type.GenericArguments,
                recoveredArguments,
                out sourceArgumentsChanged);
        }

        if (!sourceArgumentsChanged && visibleIndices.Count == type.GenericArguments.Count && IsIdentityProjection(visibleIndices))
        {
            return type;
        }

        if (visibleIndices.Any(index => index >= sourceArguments.Count))
        {
            return type;
        }

        var projectedArguments = visibleIndices
            .Select(index => sourceArguments[index])
            .ToArray();
        return WithGenericArguments(type, projectedArguments);
    }

    private static IReadOnlyList<BindingType> MergeRecoveredGenericArguments(
        IReadOnlyList<BindingType> currentArguments,
        IReadOnlyList<BindingType> recoveredArguments,
        out bool changed)
    {
        var mergedArguments = recoveredArguments.ToArray();
        changed = recoveredArguments.Count != currentArguments.Count;
        for (var index = 0; index < currentArguments.Count && index < mergedArguments.Length; index++)
        {
            var currentArgument = currentArguments[index];
            var recoveredArgument = mergedArguments[index];
            if (ShouldPreferRecoveredGenericArgument(currentArgument, recoveredArgument))
            {
                mergedArguments[index] = currentArgument.ManagedTypeFormatOverride is null
                    ? recoveredArgument
                    : recoveredArgument.WithManagedTypeFormatOverride(currentArgument.ManagedTypeFormatOverride);
                changed = true;
                continue;
            }

            mergedArguments[index] = currentArgument;
        }

        return changed ? mergedArguments : currentArguments;
    }

    private static bool ShouldPreferRecoveredGenericArgument(
        BindingType currentArgument,
        BindingType recoveredArgument)
    {
        if (currentArgument.Kind != BindingTypeKind.Handle ||
            recoveredArgument.Kind != BindingTypeKind.Handle ||
            (currentArgument.GenericArguments.Count > 0 && recoveredArgument.GenericArguments.Count == 0) ||
            recoveredArgument.ManagedTypeName.Contains('{', StringComparison.Ordinal) ||
            !IsSameRecoveredRecordArgument(currentArgument, recoveredArgument))
        {
            return false;
        }

        return !string.Equals(currentArgument.RecordId, recoveredArgument.RecordId, StringComparison.Ordinal) ||
            !string.Equals(currentArgument.CppTypeName, recoveredArgument.CppTypeName, StringComparison.Ordinal) ||
            currentArgument.Marshaller.CppIsRecordValue != recoveredArgument.Marshaller.CppIsRecordValue;
    }

    private static bool IsSameRecoveredRecordArgument(BindingType currentArgument, BindingType recoveredArgument)
    {
        if (!string.IsNullOrWhiteSpace(currentArgument.RecordId) &&
            string.Equals(currentArgument.RecordId, recoveredArgument.RecordId, StringComparison.Ordinal))
        {
            return true;
        }

        var currentCppTypeName = NormalizeRecoveredRecordTypeName(currentArgument.CppTypeName);
        var recoveredCppTypeName = NormalizeRecoveredRecordTypeName(recoveredArgument.CppTypeName);
        return string.Equals(currentCppTypeName, recoveredCppTypeName, StringComparison.Ordinal);
    }

    private static string NormalizeRecoveredRecordTypeName(string typeName)
        => StripCvAndIndirection(MarshallerConfigExtractor.NormalizeTypeName(typeName)).TrimEnd('*').Trim();

    private static bool IsIdentityProjection(IReadOnlyList<int> indices)
    {
        for (var index = 0; index < indices.Count; index++)
        {
            if (indices[index] != index)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryRecoverGenericArgumentsFromCppType(
        string cppTypeName,
        IReadOnlyDictionary<string, RecordBinding> recordsByNativeName,
        out IReadOnlyList<BindingType> arguments)
    {
        if (!TryGetTopLevelTemplateArguments(cppTypeName.TrimEnd('*', '&').Trim(), out var argumentNames))
        {
            arguments = Array.Empty<BindingType>();
            return false;
        }

        arguments = argumentNames
            .Select(argument => MapRecoveredTemplateArgument(argument, recordsByNativeName))
            .ToArray();
        return true;
    }

    private static BindingType MapRecoveredTemplateArgument(string argument, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        var normalizedArgument = MarshallerConfigExtractor.NormalizeTypeName(argument);
        var normalized = StripCvAndIndirection(normalizedArgument);
        var lookupName = normalized.TrimEnd('*').Trim();
        if (MapManagedPrimitiveType(lookupName) is { } primitive)
        {
            return primitive;
        }

        var record = FindRecordByCppTypeName(lookupName, recordsByNativeName);
        if (record is not null)
        {
            var isPointerArgument = normalized.EndsWith("*", StringComparison.Ordinal);
            var isRecordValueArgument = !isPointerArgument && CanUseRecoveredRecordValueArgument(record);
            var handleCppTypeName = isPointerArgument
                ? normalized
                : isRecordValueArgument
                    ? lookupName
                    : $"{lookupName}*";
            var handle = BindingType.CreateHandle(
                handleCppTypeName,
                record.ManagedName,
                record.SymbolId,
                record.HandleMarshallerTypeName,
                cppIsRecordValue: isRecordValueArgument);
            var managedTypeFormatOverride = record.ManagedTypeFormat;
            if (!string.IsNullOrWhiteSpace(managedTypeFormatOverride) &&
                !managedTypeFormatOverride.Contains('{', StringComparison.Ordinal))
            {
                handle.SetManagedTypeFormatOverride(managedTypeFormatOverride);
            }

            return handle;
        }

        return BindingType.CreateConfigured(lookupName, lookupName);
    }

    private static bool CanUseRecoveredRecordValueArgument(RecordBinding record)
    {
        return record.HasCopyConstructor ||
            record.GetMetadata<bool>(MetadataKeys.MarshallerConfiguredRecord) ||
            record.GetMetadata<bool>(MetadataKeys.SuppressEmission) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName) ||
            !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            !string.IsNullOrWhiteSpace(record.ManagedBaseTypeConstraints) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static bool TryGetTopLevelTemplateArguments(string typeName, out IReadOnlyList<string> arguments)
    {
        var genericStart = typeName.IndexOf('<');
        var genericEnd = typeName.LastIndexOf('>');
        if (genericStart < 0 || genericEnd <= genericStart)
        {
            arguments = Array.Empty<string>();
            return false;
        }

        arguments = SplitTopLevelArguments(typeName[(genericStart + 1)..genericEnd]);
        return true;
    }

    private static IReadOnlyList<string> SplitTopLevelArguments(string value)
    {
        var result = new List<string>();
        var depth = 0;
        var segmentStart = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '<':
                case '(':
                case '[':
                    depth++;
                    break;
                case '>':
                case ')':
                case ']':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ',' when depth == 0:
                    result.Add(MarshallerConfigExtractor.NormalizeTypeName(value[segmentStart..index]));
                    segmentStart = index + 1;
                    break;
            }
        }

        result.Add(MarshallerConfigExtractor.NormalizeTypeName(value[segmentStart..]));
        return result;
    }

    private static string StripCvAndIndirection(string typeName)
    {
        var result = typeName;
        while (result.StartsWith("const ", StringComparison.Ordinal) || result.StartsWith("volatile ", StringComparison.Ordinal))
        {
            result = result.StartsWith("const ", StringComparison.Ordinal)
                ? result[6..].TrimStart()
                : result[9..].TrimStart();
        }

        return result.TrimEnd('&').Trim();
    }

    private static IReadOnlyList<int> GetVisibleManagedGenericArgumentIndices(RecordBinding record)
    {
        var indices = new SortedSet<int>();
        CollectManagedFormatPlaceholderIndices(record.ManagedTypeFormat, indices);
        CollectManagedFormatPlaceholderIndices(record.ManagedBaseTypeName, indices);
        return indices.ToArray();
    }

    private static void CollectManagedFormatPlaceholderIndices(string? format, ISet<int> target)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '{')
            {
                continue;
            }

            var closeIndex = format.IndexOf('}', index + 1);
            if (closeIndex <= index + 1)
            {
                continue;
            }

            if (int.TryParse(format[(index + 1)..closeIndex], out var placeholderIndex) &&
                placeholderIndex >= 0)
            {
                target.Add(placeholderIndex);
            }

            index = closeIndex;
        }
    }

    private static BindingType WithGenericArguments(BindingType type, IReadOnlyList<BindingType> genericArguments)
    {
        if (type.Kind != BindingTypeKind.Handle)
        {
            return type;
        }

        var clone = BindingType.CreateHandle(
            type.CppTypeName,
            type.Marshaller.PublicTypeName,
            type.RecordId,
            type.MarshallerTypeName,
            genericArguments,
            type.Marshaller.CppIsRecordValue);
        clone.SetManagedTypeFormatOverride(type.ManagedTypeFormatOverride);
        return clone;
    }

    private static string GetManagedTypeFormatOverride(RecordBinding record)
    {
        if (!string.IsNullOrWhiteSpace(record.ManagedTypeFormat))
        {
            return record.ManagedTypeFormat!;
        }

        if (!record.IsGeneric)
        {
            return record.ManagedName;
        }

        return $"{record.ManagedName}<{string.Join(", ", record.GenericParameters.Select(static parameter => $"{{{parameter.Position}}}"))}>";
    }

    private static string StripGenericArguments(string name)
    {
        var genericStart = name.IndexOf('<');
        return genericStart >= 0 ? name[..genericStart] : name;
    }

    private static bool TryFormatManagedTypeFormat(string format, object[] arguments, out string formatted)
    {
        if (!format.Contains('{', StringComparison.Ordinal))
        {
            formatted = string.Empty;
            return false;
        }

        try
        {
            formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, arguments);
            return true;
        }
        catch (FormatException)
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static RecordBinding? FindRecordByType(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        var record = recordsByNativeName.Values.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, type.RecordId, StringComparison.Ordinal));
        if (record is not null || type.RecordId is null || !type.RecordId.StartsWith("record:", StringComparison.Ordinal))
        {
            return record;
        }

        var qualifiedName = MarshallerConfigExtractor.NormalizeTypeName(type.RecordId["record:".Length..]);
        return FindRecordByCppTypeName(qualifiedName, recordsByNativeName);
    }

    private static RecordBinding? FindRecordByCppTypeName(string cppTypeName, IReadOnlyDictionary<string, RecordBinding> recordsByNativeName)
    {
        var normalizedName = MarshallerConfigExtractor.NormalizeTypeName(cppTypeName);
        var strippedName = StripGenericArguments(normalizedName);
        if (recordsByNativeName.TryGetValue(normalizedName, out var record) ||
            recordsByNativeName.TryGetValue(strippedName, out record))
        {
            return record;
        }

        var isQualified = strippedName.Contains("::", StringComparison.Ordinal);
        return recordsByNativeName.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.CppQualifiedName, normalizedName, StringComparison.Ordinal) ||
            string.Equals(StripGenericArguments(candidate.CppQualifiedName), strippedName, StringComparison.Ordinal) ||
            (!isQualified &&
                (string.Equals(candidate.NativeName, strippedName, StringComparison.Ordinal) ||
                 candidate.CppQualifiedName.EndsWith("::" + strippedName, StringComparison.Ordinal) ||
                 StripGenericArguments(candidate.CppQualifiedName).EndsWith("::" + strippedName, StringComparison.Ordinal))));
    }

    private static string CreateRecordSymbolId(string cppQualifiedName)
        => $"record:{MarshallerConfigExtractor.NormalizeTypeName(cppQualifiedName)}";
}
