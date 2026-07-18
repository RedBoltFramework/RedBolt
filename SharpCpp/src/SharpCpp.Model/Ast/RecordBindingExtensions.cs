namespace SharpCpp.Model.Ast;

public static class RecordBindingExtensions
{
    public static RecordBinding? GetPrimaryBaseRecord(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var primaryBase = record.Bases.FirstOrDefault(static candidate => !candidate.IsVirtual);
        return primaryBase is null
            ? null
            : compilation.FindRecordBySymbolId(primaryBase.RecordId);
    }

    public static IReadOnlyList<RecordBinding> GetSecondaryBaseRecords(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var primaryBase = record.Bases.FirstOrDefault(static candidate => !candidate.IsVirtual);

        return record.Bases
            .Where(candidate => !candidate.IsVirtual && !ReferenceEquals(candidate, primaryBase))
            .Select(baseBinding => compilation.FindRecordBySymbolId(baseBinding.RecordId))
            .Where(static candidate => candidate is not null)
            .Cast<RecordBinding>()
            .ToArray();
    }

    public static IReadOnlyList<RecordBinding> EnumerateAllBaseRecords(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var results = new List<RecordBinding>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        CollectBaseRecords(record, compilation, results, visited);
        return results;
    }

    public static IReadOnlyList<FunctionBinding> GetDeclaredVirtualMethods(this RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.Members
            .OfType<FunctionBinding>()
            .Where(static function => function.IsVirtual && !function.IsStatic && !function.IsIgnored())
            .ToArray();
    }

    public static IReadOnlyList<VirtualMethodSlotBinding> GetEffectiveVirtualMethodSlots(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var orderedMethods = new List<FunctionBinding>();

        foreach (var baseRecord in record.EnumeratePrimaryBaseChain(compilation))
        {
            foreach (var method in baseRecord.GetDeclaredVirtualMethods())
            {
                AddOrReplaceMethod(orderedMethods, method);
            }
        }

        foreach (var method in record.GetDeclaredVirtualMethods())
        {
            AddOrReplaceMethod(orderedMethods, method);
        }

        return orderedMethods
            .Select(method => CreateSlot(record, compilation, method))
            .Where(static slot => slot is not null)
            .Cast<VirtualMethodSlotBinding>()
            .ToArray();
    }

    public static bool RequiresDirector(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        return record.IsDirector && record.GetEffectiveVirtualMethodSlots(compilation).Count > 0;
    }

    public static bool CanInstantiateDirector(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        return record.RequiresDirector(compilation) && !record.IsAbstract;
    }

    public static bool SignaturesMatch(FunctionBinding left, FunctionBinding right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsStatic != right.IsStatic ||
            left.IsConstMethod != right.IsConstMethod ||
            !string.Equals(left.NativeName, right.NativeName, StringComparison.Ordinal) ||
            left.Parameters.Count != right.Parameters.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Parameters.Count; index++)
        {
            var leftParameter = left.Parameters[index];
            var rightParameter = right.Parameters[index];

            if (!string.Equals(leftParameter.CppTypeName, rightParameter.CppTypeName, StringComparison.Ordinal) ||
                leftParameter.Direction != rightParameter.Direction ||
                leftParameter.AbiKind != rightParameter.AbiKind)
            {
                return false;
            }
        }

        return true;
    }

    public static RecordBinding? FindOwningRecord(this FunctionBinding function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return function.Ancestors().OfType<RecordBinding>().FirstOrDefault();
    }

    public static RecordBinding? ResolveDispatchRecord(this FunctionBinding function, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(compilation);

        if (string.IsNullOrWhiteSpace(function.NativeDispatchRecordCppTypeName))
        {
            return null;
        }

        return compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.CppQualifiedName, function.NativeDispatchRecordCppTypeName, StringComparison.Ordinal));
    }

    public static string GetManagedInterfaceName(this RecordBinding record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return $"I{record.ManagedName}";
    }

    public static IReadOnlyList<RecordBinding> GetManagedInterfaceBaseRecords(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        return record.GetSecondaryBaseRecords(compilation);
    }

    public static IReadOnlyList<FunctionBinding> GetRenderableInterfaceMethods(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var methods = new List<FunctionBinding>();
        foreach (var candidateRecord in record.EnumerateAllBaseRecords(compilation).Reverse().Append(record))
        {
            foreach (var method in candidateRecord.Members.OfType<FunctionBinding>())
            {
                if (method.IsConstructor || method.IsStatic || method.AccessModifier != BindingAccessModifier.Public || method.IsIgnored())
                {
                    continue;
                }

                AddOrReplaceMethod(methods, method);
            }
        }

        return methods;
    }

    public static bool HasOwnManagedNonFieldMemberNamed(this RecordBinding record, string managedName)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);

        return record.Members.OfType<FunctionBinding>().Any(function => string.Equals(function.ManagedName, managedName, StringComparison.Ordinal))
            || record.Members.OfType<FunctionBinding>()
                .Select(function => function.GetMetadata<AccessorPropertyAnnotation>(MetadataKeys.AccessorProperty))
                .Any(annotation => annotation is not null && string.Equals(annotation.PropertyName, managedName, StringComparison.Ordinal));
    }

    public static bool HasOwnManagedMemberNamed(this RecordBinding record, string managedName)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedName);

        return record.HasOwnManagedNonFieldMemberNamed(managedName)
            || record.Members.OfType<FieldBinding>().Any(field => string.Equals(field.ManagedName, managedName, StringComparison.Ordinal));
    }

    public static bool HasUnimplementedAbstractBaseMethods(this RecordBinding record, BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(compilation);

        var chain = record.EnumeratePrimaryBaseChain(compilation).Append(record).ToArray();
        var concreteImplementations = chain
            .SelectMany(static candidate => candidate.Members.OfType<FunctionBinding>())
            .Where(static method => !method.IsIgnored() && !method.IsPureVirtual)
            .ToArray();

        foreach (var baseRecord in chain.Take(chain.Length - 1))
        {
            foreach (var abstractMethod in baseRecord.Members.OfType<FunctionBinding>().Where(static method => !method.IsIgnored() && method.IsPureVirtual))
            {
                if (!concreteImplementations.Any(candidate => SignaturesMatch(candidate, abstractMethod)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<RecordBinding> EnumeratePrimaryBaseChain(this RecordBinding record, BindingCompilation compilation)
    {
        var baseChain = new List<RecordBinding>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { record.SymbolId };

        for (var current = record.GetPrimaryBaseRecord(compilation); current is not null; current = current.GetPrimaryBaseRecord(compilation))
        {
            if (!visited.Add(current.SymbolId))
            {
                break;
            }

            baseChain.Insert(0, current);
        }

        return baseChain;
    }

    private static VirtualMethodSlotBinding? CreateSlot(RecordBinding ownerRecord, BindingCompilation compilation, FunctionBinding method)
    {
        var methodOwner = method.FindOwningRecord();
        var implementationRecord = method.ResolveDispatchRecord(compilation) ?? methodOwner;
        if (methodOwner is null || implementationRecord is null)
        {
            return null;
        }

        var managedCallbackOwnerRecord = ReferenceEquals(methodOwner, ownerRecord)
            ? ownerRecord
            : methodOwner;

        return new VirtualMethodSlotBinding(method, implementationRecord, managedCallbackOwnerRecord, method);
    }

    private static void AddOrReplaceMethod(IList<FunctionBinding> methods, FunctionBinding candidate)
    {
        var existingIndex = -1;
        for (var index = 0; index < methods.Count; index++)
        {
            if (!SignaturesMatch(methods[index], candidate))
            {
                continue;
            }

            existingIndex = index;
            break;
        }

        if (existingIndex >= 0)
        {
            methods[existingIndex] = candidate;
            return;
        }

        methods.Add(candidate);
    }

    private static void CollectBaseRecords(
        RecordBinding record,
        BindingCompilation compilation,
        ICollection<RecordBinding> results,
        ISet<string> visited)
    {
        foreach (var baseRecord in record.Bases
                     .Select(baseBinding => compilation.Records.FirstOrDefault(candidate => string.Equals(candidate.SymbolId, baseBinding.RecordId, StringComparison.Ordinal)))
                     .Where(static candidate => candidate is not null)
                     .Cast<RecordBinding>())
        {
            if (!visited.Add(baseRecord.SymbolId))
            {
                continue;
            }

            results.Add(baseRecord);
            CollectBaseRecords(baseRecord, compilation, results, visited);
        }
    }
}
