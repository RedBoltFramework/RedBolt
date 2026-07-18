namespace SharpCpp.Model.Ast;

public sealed class BindingCompilation : BindingContainerNode
{
    private readonly List<BindingTypeAlias> _typeAliases = new();

    public BindingCompilation(
        string projectName,
        string managedNamespace,
        string managedClassName,
        string nativeLibraryName,
        IEnumerable<string> headerFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedClassName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeLibraryName);
        ArgumentNullException.ThrowIfNull(headerFiles);

        ProjectName = projectName;
        ManagedNamespace = managedNamespace;
        ManagedClassName = managedClassName;
        NativeLibraryName = nativeLibraryName;
        EntryPointPrefix = "sharpcpp_e2e";
        HeaderFiles = new List<string>(headerFiles);
        NativeSupportHeaders = new List<string>();
    }

    public override BindingNodeKind Kind => BindingNodeKind.Compilation;

    public string ProjectName { get; }

    public string ManagedNamespace { get; set; }

    public string ManagedClassName { get; set; }

    public string NativeLibraryName { get; set; }

    public string EntryPointPrefix { get; set; }

    public IList<string> HeaderFiles { get; }

    public IList<string> NativeSupportHeaders { get; }

    public IList<BindingTypeAlias> TypeAliases => _typeAliases;

    public IReadOnlyDictionary<string, string> NamespaceMappings { get; set; } = new Dictionary<string, string>();

    private IReadOnlyList<RecordBinding>? _records;
    private IReadOnlyList<EnumBinding>? _enums;
    private IReadOnlyList<FunctionBinding>? _functions;
    private IReadOnlyList<ManagedClassBinding>? _managedClasses;
    private IReadOnlyDictionary<string, RecordBinding>? _recordsBySymbolId;
    private IReadOnlyDictionary<string, EnumBinding>? _enumsBySymbolId;

    public IReadOnlyList<RecordBinding> Records => _records ??= Descendants<RecordBinding>().ToArray();

    public IReadOnlyList<EnumBinding> Enums => _enums ??= Descendants<EnumBinding>().ToArray();

    public IReadOnlyList<FunctionBinding> Functions => _functions ??= Descendants<FunctionBinding>().ToArray();

    public IReadOnlyList<ManagedClassBinding> ManagedClasses => _managedClasses ??= Descendants<ManagedClassBinding>().ToArray();

    public RecordBinding? FindRecordBySymbolId(string? symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
        {
            return null;
        }

        _recordsBySymbolId ??= Records
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);

        return _recordsBySymbolId.TryGetValue(symbolId, out var record) ? record : null;
    }

    public EnumBinding? FindEnumBySymbolId(string? symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
        {
            return null;
        }

        _enumsBySymbolId ??= Enums
            .GroupBy(static @enum => @enum.SymbolId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);

        return _enumsBySymbolId.TryGetValue(symbolId, out var @enum) ? @enum : null;
    }

    /// <summary>Invalidate cached descendants (call after pipeline passes that add members).</summary>
    public void InvalidateCaches()
    {
        _records = null;
        _enums = null;
        _functions = null;
        _managedClasses = null;
        _recordsBySymbolId = null;
        _enumsBySymbolId = null;
    }

    internal override void NotifyStructureChanged()
    {
        InvalidateCaches();
        base.NotifyStructureChanged();
    }
}
