using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RedBolt;

public sealed class DebugFrameSnapshotBatch
{
    public Drawable? Drawable;
    public Geometry? Geometry;
    public Material? Material;
    public Light? Light;
    public PipelineState? PipelineState;
    public uint SourceBatchIndex;
    public float Distance;
    public uint NumVertices;
    public uint NumPrimitives;
    public bool NewInstancingGroup;

    public override string ToString()
    {
        var drawableName = Drawable is not null && SourceBatchIndex != uint.MaxValue
            ? Drawable.GetFullNameDebug()
            : string.Empty;
        var lightName = Light is not null ? Light.GetFullNameDebug() : "null";
        var materialName = Material is not null ? Material.GetName() : "null";

        var bulletPoint = NewInstancingGroup ? '*' : '.';
        var geometryText = drawableName.Length != 0
            ? $"[{drawableName}].{SourceBatchIndex} with material [{materialName}] lit with"
            : "Light volume geometry for";
        var detailsText = string.Create(CultureInfo.InvariantCulture,
            $"distance={Distance:F2} state={RenderPipelineDebugger.FormatObject(PipelineState)} geometry={RenderPipelineDebugger.FormatObject(Geometry)} material={RenderPipelineDebugger.FormatObject(Material)}");

        return $"{bulletPoint} {NumVertices}v {NumPrimitives}t {geometryText} [{lightName}] ({detailsText})";
    }
}

public sealed class DebugFrameSnapshotQuad
{
    public string DebugComment = string.Empty;
    public Vector2i Size;

    public override string ToString()
    {
        var sizeText = Size != default ? $" {Size.X}x{Size.Y}" : string.Empty;
        return $"+ [quad{sizeText}] {DebugComment}";
    }
}

public sealed class DebugFrameSnapshotPass
{
    public string Name = string.Empty;
    public readonly List<DebugFrameSnapshotBatch> Batches = [];
    public readonly List<DebugFrameSnapshotQuad> Quads = [];

    public override string ToString()
    {
        var numBatches = 0u;
        var numVertices = 0u;
        var numPrimitives = 0u;
        var result = new StringBuilder();

        numBatches += (uint)Batches.Count;
        foreach (var batch in Batches)
        {
            result.AppendLine(batch.ToString());
            numVertices += batch.NumVertices;
            numPrimitives += batch.NumPrimitives;
        }

        numBatches += (uint)Quads.Count;
        foreach (var quad in Quads)
        {
            result.AppendLine(quad.ToString());
            numVertices += 4;
            numPrimitives += 2;
        }
        return $"Pass {Name} - {numBatches}b {numVertices}v {numPrimitives}t:\n\n{result}\n";
    }
}

public sealed class DebugFrameSnapshot
{
    public readonly List<DebugFrameSnapshotPass> Passes = [];
    public readonly HashSet<PipelineState> ScenePipelineStates = [];
    public readonly HashSet<Material> SceneMaterials = [];
    public readonly HashSet<RawShader> SceneShaders = [];

    public override string ToString()
    {
        var result = new StringBuilder();
        foreach (var pass in Passes)
            result.Append(pass);
        result.Append($"Pipeline states in scene ({ScenePipelineStates.Count}): \n\n{ScenePipelineStatesToString()}\n");
        result.Append($"Materials in scene ({SceneMaterials.Count}): \n\n{SceneMaterialsToString()}\n");
        result.Append($"Shaders in scene ({SceneShaders.Count}): \n\n{SceneShadersToString()}\n");
        return result.ToString();
    }

    public string ScenePipelineStatesToString()
    {
        var result = new StringBuilder();
        foreach (var pipelineState in ScenePipelineStates.OrderBy(RenderPipelineDebugger.MakeSortKey, StringComparer.Ordinal))
        {
            var desc = pipelineState.GetDesc();
            if (desc.AsGraphics() is { } graphicsDesc)
            {
                result.Append($"- {RenderPipelineDebugger.FormatObject(pipelineState)}: VS={RenderPipelineDebugger.FormatObject(graphicsDesc.VertexShader)} PS={RenderPipelineDebugger.FormatObject(graphicsDesc.PixelShader)} GS={RenderPipelineDebugger.FormatObject(graphicsDesc.GeometryShader)} HS={RenderPipelineDebugger.FormatObject(graphicsDesc.HullShader)} DS={RenderPipelineDebugger.FormatObject(graphicsDesc.DomainShader)}\n");
            }
            if (desc.AsCompute() is { } computeDesc)
            {
                result.Append($"- {RenderPipelineDebugger.FormatObject(pipelineState)}: CS={RenderPipelineDebugger.FormatObject(computeDesc.ComputeShader)}\n");
            }
        }
        return result.ToString();
    }

    public string SceneMaterialsToString()
    {
        var result = new StringBuilder();
        foreach (var material in SceneMaterials.OrderBy(material => material.GetName(), StringComparer.Ordinal))
        {
            var name = material.GetName().Length != 0 ? material.GetName() : "Unnamed";
            result.Append($"- {RenderPipelineDebugger.FormatObject(material)}: {name}\n");
        }
        return result.ToString();
    }

    public string SceneShadersToString()
    {
        string[] shaderTypes = ["VS", "PS"];
        var result = new StringBuilder();
        foreach (var shader in SceneShaders.OrderBy(shader => shader.GetShaderType())
                     .ThenBy(shader => shader.GetDebugName(), StringComparer.Ordinal))
        {
            var shaderType = shaderTypes[(int)shader.GetShaderType()];
            result.Append($"- {RenderPipelineDebugger.FormatObject(shader)}: [{shaderType}]{shader.GetDebugName()}\n");
        }
        return result.ToString();
    }
}

/// <summary>Debug utility that takes snapshot of current frame.</summary>
public sealed class RenderPipelineDebugger
{
    private bool _snapshotBuildingInProgress;
    private DebugFrameSnapshot _snapshot = new();
    private bool _passInProgress;

    /// <summary>Begin snapshot.</summary>
    public void BeginSnapshot()
    {
        _snapshotBuildingInProgress = true;
        _snapshot = new DebugFrameSnapshot();
    }

    /// <summary>End snapshot.</summary>
    public void EndSnapshot()
    {
        _snapshotBuildingInProgress = false;
    }

    /// <summary>Begin pass.</summary>
    public void BeginPass(string name)
    {
        if (_passInProgress)
            EndPass();
        _snapshot.Passes.Add(new DebugFrameSnapshotPass { Name = name });
        _passInProgress = true;
    }

    /// <summary>Report scene batch.</summary>
    public void ReportSceneBatch(DebugFrameSnapshotBatch sceneBatch)
    {
        if (!_passInProgress)
            BeginPass("Unnamed");
        _snapshot.Passes[^1].Batches.Add(sceneBatch);
        _snapshot.ScenePipelineStates.Add(sceneBatch.PipelineState!);
        _snapshot.SceneMaterials.Add(sceneBatch.Material!);

        var pipelineStateDesc = sceneBatch.PipelineState!.GetDesc();
        if (pipelineStateDesc.AsGraphics() is { } graphicsDesc)
        {
            _snapshot.SceneShaders.Add(graphicsDesc.VertexShader!);
            _snapshot.SceneShaders.Add(graphicsDesc.PixelShader!);
            if (graphicsDesc.GeometryShader is not null)
                _snapshot.SceneShaders.Add(graphicsDesc.GeometryShader);
            if (graphicsDesc.HullShader is not null)
                _snapshot.SceneShaders.Add(graphicsDesc.HullShader);
            if (graphicsDesc.DomainShader is not null)
                _snapshot.SceneShaders.Add(graphicsDesc.DomainShader);
        }
        if (pipelineStateDesc.AsCompute() is { } computeDesc)
            _snapshot.SceneShaders.Add(computeDesc.ComputeShader!);
    }

    /// <summary>Report quad.</summary>
    public void ReportQuad(string debugComment, Vector2i size = default)
    {
        if (!_passInProgress)
            BeginPass("Unnamed");
        _snapshot.Passes[^1].Quads.Add(new DebugFrameSnapshotQuad { DebugComment = debugComment, Size = size });
    }

    /// <summary>End pass.</summary>
    public void EndPass()
    {
        _passInProgress = false;
    }

    public bool IsSnapshotInProgress() => _snapshotBuildingInProgress;

    public static bool IsSnapshotInProgress(RenderPipelineDebugger? debugger) =>
        debugger is not null && debugger.IsSnapshotInProgress();

    public DebugFrameSnapshot GetSnapshot() => _snapshot;

    internal static string FormatObject(object? value) => value is null
        ? "0x0"
        : $"0x{RuntimeHelpers.GetHashCode(value):x}";

    internal static string MakeSortKey(PipelineState pipelineState)
    {
        if (pipelineState.GetDesc().AsGraphics() is { } graphicsDesc)
            return $"{graphicsDesc.VertexShader!.GetDebugName()} {graphicsDesc.PixelShader!.GetDebugName()}";
        if (pipelineState.GetDesc().AsCompute() is { } computeDesc)
            return computeDesc.ComputeShader!.GetDebugName();
        return string.Empty;
    }
}
