using System.Diagnostics;

namespace RedBolt;

/// <summary>
/// Base class for serializable resource that uses Archive serialization.
/// </summary>
public class RenderPath : SimpleResource
{
    public RenderPath()
    {
    }

    public RenderPath(string name)
        : base(name)
    {
    }

    public override void Dispose()
    {
        for (var i = _passes.Count - 1; i >= 0; --i)
            _passes[i].Dispose();
        _passes.Clear();
        base.Dispose();
    }

    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<RenderPath>();
    }

    /// <summary>
    /// Fill missing parameters with defaults.
    /// </summary>
    public void CollectParameters(StringVariantMap parameters)
    {
        foreach (var pass in _passes)
            pass.CollectParameters(parameters);
    }

    /// <summary>
    /// Initialize render path before using it in view.
    /// </summary>
    public void InitializeView(RenderPipelineView view)
    {
        foreach (var pass in _passes)
            pass.InitializeView(view);
    }

    /// <summary>
    /// Update settings for all passes.
    /// </summary>
    public void UpdateParameters(RenderPipelineSettings settings, IReadOnlyList<RenderPassToggle> enabledPasses, StringVariantMap parameters)
    {
        var numPasses = _passes.Count;
        if (enabledPasses.Count != numPasses)
        {
            Debug.Assert(false, $"Number of passes in render path ({numPasses}) does not match with enabled passes array size ({enabledPasses.Count})");
            return;
        }

        for (var i = 0; i < numPasses; ++i)
        {
            var pass = _passes[i];
            pass.SetEnabled(enabledPasses[i].Enabled);
            pass.UpdateParameters(settings, parameters);
        }

        _traits = default;
        foreach (var pass in _passes)
        {
            var passTraits = pass.GetTraits();
            _traits = new RenderPassTraits(
                _traits.NeedReadWriteColorBuffer || passTraits.NeedReadWriteColorBuffer,
                _traits.NeedBilinearColorSampler || passTraits.NeedBilinearColorSampler);
        }
    }

    /// <summary>
    /// Perform update that does not invoke any rendering commands.
    /// </summary>
    public void Update(SharedRenderPassState sharedState)
    {
        foreach (var pass in _passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Update(sharedState);
        }
    }

    /// <summary>
    /// Execute render commands.
    /// </summary>
    public void Render(SharedRenderPassState sharedState)
    {
        foreach (var pass in _passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Render(sharedState);
        }
    }

    /// <summary>
    /// Make deep copy of RenderPath.
    /// </summary>
    public RenderPath Clone()
    {
        var clone = new RenderPath();
        foreach (var pass in _passes)
            clone.AddPass(pass.Clone());
        return clone;
    }

    /// <summary>
    /// Repair the array of enabled render passes.
    /// </summary>
    public IReadOnlyList<RenderPassToggle> RepairEnabledRenderPasses(IReadOnlyList<RenderPassToggle> sourcePasses)
    {
        if (sourcePasses.Count == _passes.Count)
        {
            var isMatching = true;
            for (var i = 0; i < _passes.Count; ++i)
            {
                if (!string.Equals(_passes[i].GetPassName(), sourcePasses[i].Name, StringComparison.Ordinal))
                {
                    isMatching = false;
                    break;
                }
            }

            if (isMatching)
                return sourcePasses.ToArray();
        }

        var result = new RenderPassToggle[_passes.Count];
        for (var i = 0; i < _passes.Count; ++i)
        {
            var pass = _passes[i];
            RenderPassToggle match = default;
            for (var j = 0; j < sourcePasses.Count; ++j)
            {
                if (string.Equals(sourcePasses[j].Name, pass.GetPassName(), StringComparison.Ordinal))
                {
                    match = sourcePasses[j];
                    break;
                }
            }

            var isEnabled = match.Name is not null ? match.Enabled : pass.IsEnabledByDefault();
            result[i] = new RenderPassToggle(pass.GetPassName(), isEnabled);
        }
        return result;
    }

    /// <summary>
    /// Manage passes.
    /// </summary>
    public IReadOnlyList<RenderPass> Passes => _passes;

    public RenderPassTraits AggregatedPassTraits => _traits;

    public void AddPass(RenderPass pass)
    {
        _passes.Add(pass);
    }

    public void RemovePass(RenderPass pass)
    {
        _passes.Remove(pass);
    }

    public void ReorderPass(RenderPass pass, int index)
    {
        if (index < 0 || index >= _passes.Count)
            return;

        var oldIndex = _passes.IndexOf(pass);
        if (oldIndex >= 0)
        {
            _passes.RemoveAt(oldIndex);
            _passes.Insert(index, pass);
        }
    }

    /// <summary>
    /// Implement SimpleResource.
    /// </summary>
    public override void SerializeInBlock(Archive archive)
    {
        if (archive.IsInput)
        {
            for (var i = _passes.Count - 1; i >= 0; --i)
                _passes[i].Dispose();
            _passes.Clear();
        }

        var serializePasses = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock("passes") : _passes.Count != 0);
        if (!serializePasses)
            return;

        var count = archive.IsInput ? 0u : checked((uint)_passes.Count);
        using var passesBlock = archive.OpenArrayBlock("passes", count);
        if (archive.IsInput)
        {
            var numPasses = checked((int)passesBlock.SizeHint);
            for (var i = 0; i < numPasses; ++i)
            {
                using var passBlock = archive.OpenUnorderedBlock("pass");
                var type = StringHash.Empty;
                ArchiveSerialization.SerializeStringHash(archive, "type", ref type, string.Empty);
                var pass = archive.Context?.CreateObject(type) as RenderPass
                    ?? throw new ArchiveException(
                        $"Failed to create object '{archive.CurrentBlockPath}/pass' of type {type.ToDebugString()}");
                var added = false;
                try
                {
                    using (archive.OpenUnorderedBlock("value"))
                        pass.SerializeInBlock(archive);
                    _passes.Add(pass);
                    added = true;
                }
                finally
                {
                    if (!added)
                        pass.Dispose();
                }
            }
        }
        else
        {
            foreach (var pass in _passes)
            {
                using var passBlock = archive.OpenUnorderedBlock("pass");
                var type = pass.GetTypeNameHash();
                var typeName = pass.GetTypeName();
                ArchiveSerialization.SerializeStringHash(archive, "type", ref type, typeName);
                using (archive.OpenUnorderedBlock("value"))
                    pass.SerializeInBlock(archive);
            }
        }
    }

    private readonly List<RenderPass> _passes = [];
    private RenderPassTraits _traits;
}
