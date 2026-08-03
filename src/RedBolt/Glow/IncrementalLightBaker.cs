//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Glow;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

public enum IncrementalLightBakerPhase
{
    NotStarted,
    BakingDirectLighting,
    BakingIndirectLighting,
    Finalizing
}

public sealed class IncrementalLightBakerStatus
{
    private int _phase;
    private int _processedElements;
    private int _totalElements;

    public IncrementalLightBakerPhase Phase
    {
        get => (IncrementalLightBakerPhase)Volatile.Read(ref _phase);
        internal set => Volatile.Write(ref _phase, (int)value);
    }

    public int ProcessedElements
    {
        get => Volatile.Read(ref _processedElements);
        internal set => Volatile.Write(ref _processedElements, value);
    }

    public int TotalElements
    {
        get => Volatile.Read(ref _totalElements);
        internal set => Volatile.Write(ref _totalElements, value);
    }

    internal void IncrementProcessedElements() => Interlocked.Increment(ref _processedElements);

    public override string ToString() => Phase switch
    {
        IncrementalLightBakerPhase.Finalizing => "Finalizing...",
        IncrementalLightBakerPhase.BakingDirectLighting =>
            $"Baking direct lighting: {ProcessedElements}/{TotalElements} lightmaps...",
        IncrementalLightBakerPhase.BakingIndirectLighting =>
            $"Baking indirect lighting: {ProcessedElements}/{TotalElements} lightmaps...",
        _ => "Not started."
    };
}

/// <summary>Incremental light baker.</summary>
public sealed class IncrementalLightBaker : IDisposable
{
    private LightBakingSettings _settings;
    private Scene? _scene;
    private Context? _context;
    private BakedSceneCollector? _collector;
    private BakedLightCache? _cache;
    private FileIdentifier _outputDirectory;
    private readonly List<Vector3i> _chunks = [];
    private int _numLightmapCharts;
    private int _numLightmapsTotal;

    public IncrementalLightBakerStatus Status { get; } = new();

    public void Dispose()
    {
    }

    public bool Initialize(LightBakingSettings settings, Scene scene,
        BakedSceneCollector collector, BakedLightCache cache)
    {
        _settings = settings;
        _scene = scene;
        _context = scene.Context;
        _collector = collector;
        _cache = cache;
        _outputDirectory = GetOutputDirectory();
        if (_outputDirectory.IsEmpty())
            return false;

        collector.LockScene(scene, settings.Incremental.ChunkSize);
        _chunks.AddRange(collector.GetChunks());
        if (_chunks.Count != 0)
        {
            var baseChunk = _chunks.Aggregate(Vector3i.Min);
            _chunks.Sort((lhs, rhs) => Swizzle(lhs, baseChunk).CompareTo(Swizzle(rhs, baseChunk)));
        }

        var gi = scene.GetComponent<GlobalIllumination>()!;
        var giFileName = _outputDirectory + settings.Incremental.GIDataFileName;
        using var file = new BinaryFile();
        if (!file.SaveFile(giFileName))
        {
            Log.GetLogger().LogError("Cannot allocate GI data file at '{FileName}'", giFileName.ToUri());
            return false;
        }
        var vfs = _context.GetSubsystem<VirtualFileSystem>()!;
        var resourceName = vfs.GetCanonicalIdentifier(giFileName);
        gi.SetFileRef(new ResourceRef(new StringHash(nameof(BinaryFile)), resourceName.ToUri()));
        return true;
    }

    public void ProcessScene()
    {
        GenerateChartsAndUpdateScene();
        GenerateBakingChunks();
    }

    public bool Bake(StopToken stopToken)
    {
        if (!BakeDirectCharts(stopToken))
            return false;
        return BakeIndirectAndFilter(stopToken);
    }

    public void CommitScene()
    {
        StitchAndSaveImages();
    }

    private void GenerateChartsAndUpdateScene()
    {
        var vfs = _context!.GetSubsystem<VirtualFileSystem>()!;
        _numLightmapCharts = 0;
        foreach (var chunk in _chunks)
        {
            var uniqueGeometries = _collector!.GetUniqueGeometries(chunk);
            var uniqueLightProbes = _collector.GetUniqueLightProbeGroups(chunk);
            var charts = LightmapCharter.GenerateLightmapCharts(
                uniqueGeometries.Cast<Component?>().ToList(), _settings.Charting, _numLightmapCharts);
            LightmapCharter.ApplyLightmapCharts(charts);
            _collector.CommitGeometries(chunk);
            for (var i = 0; i < uniqueLightProbes.Count; ++i)
            {
                var fileName = GetLightProbeBakedDataFileName(chunk, i);
                var resourceName = vfs.GetCanonicalIdentifier(fileName);
                uniqueLightProbes[i].SetBakedDataFileRef(
                    new ResourceRef(new StringHash(nameof(BinaryFile)), resourceName.ToUri()));
            }
            _numLightmapCharts += charts.Count;
        }

        _scene!.ResetLightmaps();
        for (var i = 0; i < _numLightmapCharts; ++i)
        {
            var fileName = GetLightmapFileName(i);
            var resourceName = vfs.GetCanonicalIdentifier(fileName);
            if (!vfs.Exists(fileName))
            {
                using var placeholder = new Image();
                placeholder.SetSize(1, 1, 4);
                placeholder.SetPixel(0, 0, Color.Black);
                placeholder.SaveFile(fileName);
            }
            if (resourceName.Scheme == "file")
                Log.GetLogger().LogWarning("Absolute path is used for lightmap '{FileName}'", fileName.ToUri());
            _scene.AddLightmap(resourceName.ToUri());
        }
    }

    private void GenerateBakingChunks()
    {
        _numLightmapsTotal = 0;
        foreach (var chunk in _chunks)
        {
            var bakedChunk = BakedSceneChunk.Create(_context!, _collector!, chunk, _settings);
            _numLightmapsTotal += bakedChunk.Lightmaps.Count;
            _cache!.StoreBakedChunk(chunk, bakedChunk);
        }
    }

    private bool BakeDirectCharts(StopToken stopToken)
    {
        Status.Phase = IncrementalLightBakerPhase.BakingDirectLighting;
        Status.ProcessedElements = 0;
        Status.TotalElements = _numLightmapsTotal;
        foreach (var chunk in _chunks)
        {
            var bakedChunk = _cache!.LoadBakedChunk(chunk)!;
            for (var i = 0; i < bakedChunk.Lightmaps.Count; ++i)
            {
                if (stopToken.IsStopped())
                    return false;
                var lightmapIndex = bakedChunk.Lightmaps[i];
                var geometryBuffer = bakedChunk.GeometryBuffers[i];
                var bakedDirect = new LightmapChartBakedDirect(geometryBuffer.LightmapSize);
                LightTracer.BakeEmissionLight(bakedDirect, geometryBuffer,
                    _settings.EmissionTracing, _settings.Properties.EmissionBrightness);
                foreach (var light in bakedChunk.BakedLights)
                {
                    LightTracer.BakeDirectLightForCharts(bakedDirect, geometryBuffer,
                        bakedChunk.RaytracerScene, bakedChunk.GeometryBufferToRaytracer,
                        light, _settings.DirectChartTracing);
                }
                _cache.StoreDirectLight(lightmapIndex, bakedDirect);
                Status.IncrementProcessedElements();
            }
        }
        return true;
    }

    private bool BakeIndirectAndFilter(StopToken stopToken)
    {
        Status.Phase = IncrementalLightBakerPhase.BakingIndirectLighting;
        Status.ProcessedElements = 0;
        Status.TotalElements = _numLightmapsTotal;
        var numTexels = checked((int)(_settings.Charting.LightmapSize * _settings.Charting.LightmapSize));
        var directFilterBuffer = new Vector3f[numTexels];
        var indirectFilterBuffer = new Vector4f[numTexels];
        var lightProbesBakedData = new LightProbeCollectionBakedData();
        var bakedIndirect = new LightmapChartBakedIndirect(checked((int)_settings.Charting.LightmapSize));

        foreach (var chunk in _chunks)
        {
            if (stopToken.IsStopped())
                return false;
            var bakedChunk = _cache!.LoadBakedChunk(chunk)!;
            var bakedDirectLightmaps = new LightmapChartBakedDirect?[_numLightmapCharts];
            foreach (var lightmapIndex in bakedChunk.RequiredDirectLightmaps)
                bakedDirectLightmaps[lightmapIndex] = _cache.LoadDirectLight(lightmapIndex);

            lightProbesBakedData.Resize(bakedChunk.LightProbesCollection.GetNumProbes());
            LightTracer.BakeIndirectLightForLightProbes(lightProbesBakedData,
                bakedChunk.LightProbesCollection, bakedDirectLightmaps,
                bakedChunk.RaytracerScene, _settings.IndirectProbesTracing);
            var lightProbesMesh = new TetrahedralMesh();
            lightProbesMesh.Define(bakedChunk.LightProbesCollection.WorldPositions);

            for (var i = 0; i < bakedChunk.Lightmaps.Count; ++i)
            {
                if (stopToken.IsStopped())
                    return false;
                var lightmapIndex = bakedChunk.Lightmaps[i];
                var geometryBuffer = bakedChunk.GeometryBuffers[i];
                var bakedDirect = _cache.LoadDirectLight(lightmapIndex)!;
                Array.Clear(bakedIndirect.Light);
                LightTracer.BakeIndirectLightForCharts(bakedIndirect, bakedDirectLightmaps,
                    geometryBuffer, lightProbesMesh, lightProbesBakedData,
                    bakedChunk.RaytracerScene, bakedChunk.GeometryBufferToRaytracer,
                    _settings.IndirectChartTracing);
                bakedIndirect.NormalizeLight();

                if (_settings.DirectFilter.KernelRadius > 0)
                {
                    LightmapFilter.FilterDirectLight(bakedDirect, directFilterBuffer,
                        geometryBuffer, _settings.DirectFilter,
                        checked((int)_settings.DirectChartTracing.NumTasks));
                }
                if (_settings.IndirectFilter.KernelRadius > 0)
                {
                    LightmapFilter.FilterIndirectLight(bakedIndirect, indirectFilterBuffer,
                        geometryBuffer, _settings.IndirectFilter,
                        checked((int)_settings.IndirectChartTracing.NumTasks));
                }

                var lightmap = new BakedLightmap(checked((int)_settings.Charting.LightmapSize));
                for (var texel = 0; texel < lightmap.Lightmap.Count; ++texel)
                {
                    lightmap.Lightmap[texel] = Vector3f.Max(Vector3f.Zero, directFilterBuffer[texel])
                        + Vector3f.Max(Vector3f.Zero, indirectFilterBuffer[texel].ToVector3());
                }
                _cache.StoreLightmap(lightmapIndex, lightmap);
                Status.IncrementProcessedElements();
            }

            foreach (var light in bakedChunk.BakedLights)
            {
                LightTracer.BakeDirectLightForLightProbes(lightProbesBakedData,
                    bakedChunk.LightProbesCollection, bakedChunk.RaytracerScene,
                    light, _settings.DirectProbesTracing);
            }
            for (var groupIndex = 0; groupIndex < bakedChunk.NumUniqueLightProbes; ++groupIndex)
            {
                var fileName = GetLightProbeBakedDataFileName(chunk, groupIndex);
                if (!LightProbeGroup.SaveLightProbesBakedData(_context!, fileName,
                    bakedChunk.LightProbesCollection, lightProbesBakedData, checked((uint)groupIndex)))
                {
                    var groupName = groupIndex < bakedChunk.LightProbesCollection.Names.Count
                        ? bakedChunk.LightProbesCollection.Names[groupIndex] : string.Empty;
                    Log.GetLogger().LogError("Cannot save light probes for group '{Group}' in chunk {Chunk}",
                        groupName, chunk);
                }
            }
        }

        Status.Phase = IncrementalLightBakerPhase.Finalizing;
        return true;
    }

    private void StitchAndSaveImages()
    {
        var lightmapSize = checked((int)_settings.Charting.LightmapSize);
        var numTexels = lightmapSize * lightmapSize;
        using var stitchingContext = LightmapStitcher.Initialize(_context!, lightmapSize, 4);
        var buffer = new Vector4f[numTexels];
        using var lightmapImage = new Image();
        if (!lightmapImage.SetSize(lightmapSize, lightmapSize, 4))
        {
            Log.GetLogger().LogError("Cannot allocate image for lightmap");
            return;
        }

        foreach (var chunk in _chunks)
        {
            var bakedChunk = _cache!.LoadBakedChunk(chunk)!;
            for (var i = 0; i < bakedChunk.Lightmaps.Count; ++i)
            {
                var lightmapIndex = bakedChunk.Lightmaps[i];
                var bakedLightmap = _cache.LoadLightmap(lightmapIndex)!.Value;
                var geometryBuffer = bakedChunk.GeometryBuffers[i];
                if (_settings.Stitching.NumIterations > 0 && geometryBuffer.Seams.Count != 0)
                {
                    using var seamsModel = LightmapStitcher.CreateSeamsModel(_context!, geometryBuffer.Seams);
                    LightmapStitcher.StitchLightmapSeams(stitchingContext,
                        bakedLightmap.Lightmap, buffer, _settings.Stitching, seamsModel);
                }
                else
                {
                    for (var texel = 0; texel < numTexels; ++texel)
                        buffer[texel] = new Vector4f(bakedLightmap.Lightmap[texel], 1.0f);
                }

                for (var texel = 0; texel < numTexels; ++texel)
                {
                    var x = texel % geometryBuffer.LightmapSize;
                    var y = texel / geometryBuffer.LightmapSize;
                    var color = new Color(buffer[texel].ToVector3()).LinearToGamma();
                    color.R *= 0.5f;
                    color.G *= 0.5f;
                    color.B *= 0.5f;
                    lightmapImage.SetPixel(x, y, color);
                }
                lightmapImage.SaveFile(GetLightmapFileName(lightmapIndex));
            }
        }
    }

    private FileIdentifier GetOutputDirectory()
    {
        if (_settings.Incremental.OutputDirectory.Length != 0)
            return FileIdentifier.FromUri(_settings.Incremental.OutputDirectory);
        var sceneFileName = _scene!.GetFileName();
        if (sceneFileName.Length == 0)
        {
            Log.GetLogger().LogError("Cannot find output directory for lightmaps: scene file name is undefined");
            return FileIdentifier.Empty;
        }
        return FileIdentifier.FromUri(sceneFileName + ".d");
    }

    private FileIdentifier GetLightmapFileName(int lightmapIndex) =>
        _outputDirectory + Format(_settings.Incremental.LightmapNameFormat, lightmapIndex);

    private FileIdentifier GetLightProbeBakedDataFileName(Vector3i chunk, int index) =>
        _outputDirectory + Format(_settings.Incremental.LightProbeGroupNameFormat,
            chunk.X, chunk.Y, chunk.Z, index);

    private static string Format(string pattern, params object[] values)
    {
        foreach (var value in values)
        {
            var placeholder = pattern.IndexOf("{}", StringComparison.Ordinal);
            if (placeholder < 0)
                throw new FormatException($"Not enough placeholders in '{pattern}'.");
            pattern = pattern[..placeholder]
                + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                + pattern[(placeholder + 2)..];
        }
        return pattern;
    }

    private static ulong Swizzle(Vector3i value, Vector3i baseValue)
    {
        ReadOnlySpan<uint> components =
        [
            checked((uint)(value.X - baseValue.X)),
            checked((uint)(value.Y - baseValue.Y)),
            checked((uint)(value.Z - baseValue.Z))
        ];
        ulong result = 0;
        const int bitsPerComponent = 64 / 3;
        for (var component = 0; component < components.Length; ++component)
        for (var bitIndex = 0; bitIndex < bitsPerComponent; ++bitIndex)
            result |= (ulong)((components[component] >> bitIndex) & 1u) << (bitIndex * 3 + component);
        return result;
    }
}
