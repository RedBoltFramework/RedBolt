// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Resource;
using RedBolt.Utility;

using File = global::System.IO.File;

/// <summary>Manages assets of the project.</summary>
public class AssetManager : RedObject
{
    public delegate void ProcessAssetCompleted(
        List<AssetTransformerInput> input, List<AssetTransformerOutput?> output, string message);

    public delegate void ProcessAssetQueued(List<AssetTransformerInput> input, ProcessAssetCompleted callback);

    public event Action<AssetManager>? OnInitialized;

    /// <summary>Number of processed assets and total number of assets in the current queue.</summary>
    public readonly record struct ProgressInfo(int NumProcessedAssets, int NumAssetsTotal);

    public AssetManager()
    {
        _project = new WeakReference<Project>(GetSubsystem<Project>());
        _dataWatcher = new FileWatcher();

        foreach (var isPostTransform in new[] { false, true })
            _transformerHierarchy[isPostTransform ? 1 : 0] = new AssetTransformerHierarchy(isPostTransform);

        _dataWatcher.StartWatching(Project.DataPath, true);
        Context.ReflectionRemoved += OnReflectionRemoved;
        SetProcessCallback(null);
    }

    public override void Dispose()
    {
        Context.ReflectionRemoved -= OnReflectionRemoved;

        _requestQueue.Clear();
        _ignoredAssetUpdates.Clear();
        _assetPipelineFiles.Clear();
        _assets.Clear();
        _transformerHierarchy[1].Dispose();
        _transformerHierarchy[0].Dispose();
        _assetPipelines.Clear();
        _processCallback = null!;
        _dataWatcher.Dispose();
        base.Dispose();
    }

    /// <summary>Override asset processing.</summary>
    public void SetProcessCallback(ProcessAssetQueued? callback, int maxConcurrency = 1)
    {
        void DefaultCallback(List<AssetTransformerInput> input, ProcessAssetCompleted completed)
        {
            ProcessAssetBatch(input, completed);
        }

        _processCallback = callback ?? DefaultCallback;
        _maxConcurrentRequests = Math.Max(maxConcurrency, 1);
    }

    /// <summary>Process asset without affecting internal state of AssetManager.</summary>
    public void ProcessAssetBatch(List<AssetTransformerInput> input, ProcessAssetCompleted callback)
    {
        var batchSize = input.Count;

        var result = new List<AssetTransformerOutput?>(batchSize);
        for (var i = 0; i < batchSize; ++i)
            result.Add(ProcessAsset(input[i]));

        callback(input, result, string.Empty);
    }

    /// <summary>Initialize asset manager.</summary>
    /// <remarks>Should be called after the manager configuration is loaded from file and plugins are initialized.</remarks>
    public void Initialize(bool readOnly)
    {
        _autoProcessAssets = !readOnly;

        InitializeAssetPipelines();
        InvalidateOutdatedAssetsInPath(string.Empty);

        if (_autoProcessAssets)
        {
            EnsureAssetsAndCacheValid();
            ScanAndQueueAssetProcessing();
        }

        if (_requestQueue.Count == 0 && _numOngoingRequests == 0)
        {
            _initialized = true;
            OnInitialized?.Invoke(this);
        }
    }

    public void Update()
    {
        var cache = GetSubsystem<ResourceCache>();
        cache.SetResourceReloadSuspended(IsProcessing);

        if (IsProcessing)
        {
            if (_requestQueue.Count != 0 && _numOngoingRequests < _maxConcurrentRequests)
                ConsumeAssetQueue();
            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            OnInitialized?.Invoke(this);
        }

        // Reset progress
        _progress = new ProgressInfo();

        ProcessFileSystemUpdates();

        if (_autoProcessAssets)
        {
            EnsureAssetsAndCacheValid();
            ScanAndQueueAssetProcessing();
        }
    }

    public void MarkCacheDirty(string resourcePath)
    {
        InvalidateAssetsInPath(resourcePath);
    }

    /// <summary>Return current progress of asset processing.</summary>
    public ProgressInfo Progress => _progress;

    /// <summary>Return whether asset manager is currently processing assets.</summary>
    public bool IsProcessing => _requestQueue.Count != 0 || _numOngoingRequests != 0;

    /// <summary>Serialize.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        SerializeMapOptional(archive, "Assets", _assets, SerializeAssetDesc);
        SerializeMapOptional(
            archive, "AssetPipelineModificationTimes", _assetPipelineFiles, ArchiveSerialization.SerializeValue);

        if (archive.IsInput)
        {
            foreach (var pair in _assets)
                pair.Value.ResourceName = pair.Key;
        }
    }

    public void LoadFile(string fileName)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.LoadFile(new FileIdentifier(fileName)))
            jsonFile.LoadObject("Cache", this);
    }

    public void SaveFile(string fileName)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.SaveObject("Cache", this))
            jsonFile.SaveFile(new FileIdentifier(fileName));
    }

    private static List<string> CollectDirectories(IReadOnlyList<string> fileNames)
    {
        var result = new List<string>(fileNames.Count);
        foreach (var fileName in fileNames)
            result.Add(GetResourcePath(fileName));

        result.Sort(StringComparer.Ordinal);
        for (var i = result.Count - 1; i > 0; --i)
        {
            if (string.Equals(result[i], result[i - 1], StringComparison.Ordinal))
                result.RemoveAt(i);
        }

        return result;
    }

    private static void RemoveRedundantPrefixesSorted(List<string> prefixes)
    {
        if (prefixes.Count == 0)
            return;

        if (prefixes[0].Length == 0)
        {
            if (prefixes.Count > 1)
                prefixes.RemoveRange(1, prefixes.Count - 1);
            return;
        }

        for (var i = 1; i < prefixes.Count; ++i)
        {
            var isPrefixed = false;
            for (var j = 0; j < i; ++j)
            {
                if (prefixes[j].Length != 0 && prefixes[i].StartsWith(prefixes[j], StringComparison.Ordinal))
                {
                    isPrefixed = true;
                    break;
                }
            }
            if (isPrefixed)
            {
                prefixes[i] = string.Empty;
                break;
            }
        }
        prefixes.RemoveAll(static prefix => prefix.Length == 0);
    }

    private static List<string> EnumerateDisjoinedDirectories(IReadOnlyList<string> fileNames)
    {
        var result = CollectDirectories(fileNames);
        RemoveRedundantPrefixesSorted(result);
        return result;
    }

    private List<string> EnumerateAssetFiles(string resourcePath)
    {
        var result = new List<string>();
        var rootPath = GetFileName(resourcePath);
        if (!Directory.Exists(rootPath))
            return result;

        foreach (var fileName in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(rootPath, fileName).Replace('\\', '/');
            if (!Project.IsFileNameIgnored(relativeName))
                result.Add(AddTrailingSlash(resourcePath) + relativeName);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private SortedDictionary<string, uint> EnumerateAssetPipelineFiles()
    {
        var result = new SortedDictionary<string, uint>(StringComparer.Ordinal);
        if (!Directory.Exists(Project.DataPath))
            return result;

        foreach (var fileName in Directory.EnumerateFiles(Project.DataPath, "*", SearchOption.AllDirectories))
        {
            var resourceName = Path.GetRelativePath(Project.DataPath, fileName).Replace('\\', '/');
            if (AssetPipeline.CheckExtension(resourceName))
                result.Add(resourceName, GetLastModifiedTime(fileName));
        }
        return result;
    }

    private AssetPipelineDesc LoadAssetPipeline(AssetPipeline pipeline, uint modificationTime)
    {
        var result = new AssetPipelineDesc
        {
            ResourceName = pipeline.Name,
            ModificationTime = modificationTime,
        };
        foreach (var transformer in pipeline.GetTransformers())
        {
            if (transformer is not null)
                result.Transformers.Add(transformer);
        }
        foreach (var dependency in pipeline.GetDependencies())
            result.Dependencies.Add(dependency.Clone());
        return result;
    }

    private List<AssetPipelineDesc> LoadAssetPipelines(SortedDictionary<string, uint> assetPipelineFiles)
    {
        var cache = GetSubsystem<ResourceCache>();

        var result = new List<AssetPipelineDesc>();
        foreach (var pair in assetPipelineFiles)
        {
            // Never cache these files.
            var pipeline = cache.GetTempResource<AssetPipeline>(pair.Key);
            if (pipeline is null)
            {
                Log.GetLogger().LogError("Failed to load {ResourceName} as JSON file", pair.Key);
                continue;
            }

            result.Add(LoadAssetPipeline(pipeline, pair.Value));
        }
        return result;
    }

    private static Dictionary<string, AssetPipelineDiff> DiffAssetPipelines(
        IReadOnlyList<AssetPipelineDesc> oldPipelines, IReadOnlyList<AssetPipelineDesc> newPipelines)
    {
        var result = new Dictionary<string, AssetPipelineDiff>(StringComparer.Ordinal);
        foreach (var pipelineDesc in oldPipelines)
        {
            result.TryGetValue(pipelineDesc.ResourceName, out var diff);
            diff ??= new AssetPipelineDiff();
            diff.OldPipeline = pipelineDesc;
            result[pipelineDesc.ResourceName] = diff;
        }
        foreach (var pipelineDesc in newPipelines)
        {
            result.TryGetValue(pipelineDesc.ResourceName, out var diff);
            diff ??= new AssetPipelineDiff();
            diff.NewPipeline = pipelineDesc;
            result[pipelineDesc.ResourceName] = diff;
        }
        return result;
    }

    private static List<string> GetTransformerTypes(AssetPipelineDesc pipeline)
    {
        var result = new List<string>();
        foreach (var transformer in pipeline.Transformers)
            result.Add(transformer.GetTypeName());

        result.Sort(StringComparer.Ordinal);
        for (var i = result.Count - 1; i > 0; --i)
        {
            if (string.Equals(result[i], result[i - 1], StringComparison.Ordinal))
                result.RemoveAt(i);
        }

        return result;
    }

    private static List<AssetTransformer> GetTransformers(AssetPipelineDesc pipeline)
    {
        return [.. pipeline.Transformers];
    }

    private string GetFileName(string resourceName)
    {
        return Project.DataPath + resourceName;
    }

    private bool IsAssetUpToDate(AssetDesc assetDesc)
    {
        // Check if asset file exists
        var fileName = GetFileName(assetDesc.ResourceName);
        if (!File.Exists(fileName))
            return false;

        // Check if the asset has not been modified
        var assetModificationTime = GetLastModifiedTime(fileName);
        if (assetDesc.ModificationTime != assetModificationTime)
        {
            if (!_ignoredAssetUpdates.Contains(assetDesc.ResourceName))
                return false;

            // Ignore the update once if it was requested
            _ignoredAssetUpdates.Remove(assetDesc.ResourceName);
            assetDesc.ModificationTime = assetModificationTime;
        }

        // Check if outputs are present, don't check modification times for simplicity
        foreach (var outputResourceName in assetDesc.Outputs)
        {
            var outputFileName = Project.CachePath + outputResourceName;
            if (!File.Exists(outputFileName))
                return false;
        }

        // Check if dependencies are present and up-to-date
        foreach (var pair in assetDesc.DependencyModificationTimes)
        {
            var dependencyFileName = GetFileName(pair.Key);
            if (!File.Exists(dependencyFileName))
                return false;

            var modificationTime = GetLastModifiedTime(dependencyFileName);
            if (modificationTime != pair.Value)
                return false;
        }

        return true;
    }

    private AssetTransformerOutput? ProcessAsset(AssetTransformerInput input)
    {
        var cachePath = Project.CachePath;
        var transformers = _transformerHierarchy[input.IsPostTransform ? 1 : 0]
            .GetTransformerCandidates(input.ResourceName, input.Flavor);

        var output = new AssetTransformerOutput();
        return AssetTransformer.ExecuteTransformersAndStore(input, cachePath, output, transformers) ? output : null;
    }

    private void InvalidateAssetsInPath(string resourcePath)
    {
        _hasInvalidAssets = true;
        foreach (var pair in _assets)
        {
            if (pair.Key.StartsWith(resourcePath, StringComparison.Ordinal))
                pair.Value.CacheInvalid = true;
        }
    }

    private void InvalidateTransformedAssetsInPath(string resourcePath, IReadOnlyList<string> transformers)
    {
        _hasInvalidAssets = true;
        foreach (var pair in _assets)
        {
            if (pair.Key.StartsWith(resourcePath, StringComparison.Ordinal)
                && pair.Value.IsAnyTransformerUsed(transformers))
            {
                pair.Value.CacheInvalid = true;
            }
        }
    }

    private void InvalidateApplicableAssetsInPath(string resourcePath, IReadOnlyList<AssetTransformer> transformers)
    {
        _hasInvalidAssets = true;
        foreach (var pair in _assets)
        {
            var resourceName = pair.Key;
            var assetDesc = pair.Value;
            if (resourceName.StartsWith(resourcePath, StringComparison.Ordinal))
            {
                const bool isPostTransform = false; // Not checked by AssetTransformer::IsApplicable
                var input = new AssetTransformerInput(
                    isPostTransform, _defaultFlavor, resourceName, GetFileName(resourceName), assetDesc.ModificationTime);
                if (AssetTransformer.IsApplicable(input, transformers))
                    assetDesc.CacheInvalid = true;
            }
        }
    }

    private void InvalidateOutdatedAssetsInPath(string resourcePath)
    {
        _hasInvalidAssets = true;
        foreach (var pair in _assets)
        {
            if (pair.Key.StartsWith(resourcePath, StringComparison.Ordinal)
                || pair.Value.DependencyModificationTimes.ContainsKey(resourcePath))
            {
                if (!IsAssetUpToDate(pair.Value))
                    pair.Value.CacheInvalid = true;
            }
        }
    }

    private void CleanupAssetOutputs(AssetDesc assetDesc)
    {
        foreach (var outputResourceName in assetDesc.Outputs)
        {
            var outputFileName = Project.CachePath + outputResourceName;
            TryDeleteFile(outputFileName);
        }
    }

    private void CleanupInvalidatedAssets()
    {
        foreach (var pair in _assets)
        {
            if (pair.Value.CacheInvalid)
                CleanupAssetOutputs(pair.Value);
        }

        foreach (var resourceName in _assets.Where(static pair => pair.Value.CacheInvalid).Select(static pair => pair.Key).ToArray())
            _assets.Remove(resourceName);
    }

    private void CleanupCacheFolder()
    {
        var foldersToKeep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assetDesc in _assets.Values)
        {
            foreach (var outputResourceName in assetDesc.Outputs)
            {
                for (var i = outputResourceName.IndexOf('/'); i != -1; i = outputResourceName.IndexOf('/', i + 1))
                    foldersToKeep.Add(outputResourceName[..i]);
            }
        }

        if (!Directory.Exists(Project.CachePath))
            return;

        var allFolders = Directory.EnumerateDirectories(Project.CachePath, "*", SearchOption.AllDirectories)
            .Select(fileName => Path.GetRelativePath(Project.CachePath, fileName).Replace('\\', '/'))
            .Where(static folder => !folder.EndsWith(".", StringComparison.Ordinal))
            .ToArray();

        foreach (var resourcePath in allFolders)
        {
            if (!foldersToKeep.Contains(resourcePath))
                TryDeleteDirectory(Project.CachePath + resourcePath);
        }
    }

    private List<string> GetUpdatedPaths(bool updateAll)
    {
        _allPathUpdates.Clear();
        var change = new FileChange();
        while (_dataWatcher.GetNextChange(ref change))
        {
            _allPathUpdates.Add(change.FileName);
            if (change.OldFileName.Length != 0)
                _allPathUpdates.Add(change.OldFileName);
        }
        _allPathUpdates.Sort(StringComparer.Ordinal);

        _compressedPathUpdates.Clear();
        if (updateAll)
            _compressedPathUpdates.Add(string.Empty);
        else
        {
            foreach (var path in _allPathUpdates)
            {
                if (_compressedPathUpdates.Count != 0
                    && path.StartsWith(_compressedPathUpdates[^1], StringComparison.Ordinal))
                {
                    continue;
                }
                _compressedPathUpdates.Add(path);
            }
        }

        return _compressedPathUpdates;
    }

    private void InitializeAssetPipelines()
    {
        var newAssetPipelineFiles = EnumerateAssetPipelineFiles();
        var newAssetPipelines = LoadAssetPipelines(newAssetPipelineFiles);

        foreach (var pair in _assetPipelineFiles)
        {
            if (!newAssetPipelineFiles.TryGetValue(pair.Key, out var value) || pair.Value != value)
                InvalidateAssetsInPath(GetResourcePath(pair.Key));
        }
        foreach (var pair in newAssetPipelineFiles)
        {
            if (!_assetPipelineFiles.TryGetValue(pair.Key, out var value) || pair.Value != value)
                InvalidateAssetsInPath(GetResourcePath(pair.Key));
        }

        _assetPipelines = newAssetPipelines;
        _assetPipelineFiles = newAssetPipelineFiles;
        UpdateTransformHierarchy();
    }

    private void UpdateAssetPipelines()
    {
        var newAssetPipelineFiles = EnumerateAssetPipelineFiles();
        var newAssetPipelines = LoadAssetPipelines(newAssetPipelineFiles);
        var pipelinesDiff = DiffAssetPipelines(_assetPipelines, newAssetPipelines);

        foreach (var pair in pipelinesDiff)
        {
            var diff = pair.Value;
            // Skip if unchanged
            if (diff.NewPipeline is not null && diff.OldPipeline is not null
                && diff.NewPipeline.ModificationTime == diff.OldPipeline.ModificationTime)
            {
                continue;
            }

            var resourcePath = GetResourcePath(pair.Key);
            // Invalidate all assets using transformers in old pipeline
            if (diff.OldPipeline is not null)
                InvalidateTransformedAssetsInPath(resourcePath, GetTransformerTypes(diff.OldPipeline));
            // Invalidate all assets that may use transformers in new pipeline
            if (diff.NewPipeline is not null)
                InvalidateApplicableAssetsInPath(resourcePath, GetTransformers(diff.NewPipeline));
        }

        _assetPipelineFiles = newAssetPipelineFiles;
        _assetPipelines = newAssetPipelines;
        UpdateTransformHierarchy();
    }

    private void UpdateTransformHierarchy()
    {
        foreach (var transformerHierarchy in _transformerHierarchy)
        {
            transformerHierarchy.Clear();
            foreach (var pipeline in _assetPipelines)
            {
                foreach (var transformer in pipeline.Transformers)
                    transformerHierarchy.AddTransformer(GetResourcePath(pipeline.ResourceName), transformer);
                foreach (var link in pipeline.Dependencies)
                    transformerHierarchy.AddDependency(link.Class, link.DependsOn);
            }
            transformerHierarchy.CommitDependencies();
        }
    }

    private void ProcessFileSystemUpdates()
    {
        // TODO(editor): Throttle updates?
        var pathUpdates = GetUpdatedPaths(_reloadAssetPipelines);
        _reloadAssetPipelines = false;

        if (pathUpdates.Count != 0)
        {
            UpdateAssetPipelines();
            foreach (var updatedPath in pathUpdates)
                InvalidateOutdatedAssetsInPath(updatedPath);
        }
    }

    private void EnsureAssetsAndCacheValid()
    {
        if (!_hasInvalidAssets)
            return;

        // Suppress resource reloading before files are deleted.
        // If no processing is queued, this flag will be overwritten on next Update().
        var cache = GetSubsystem<ResourceCache>();
        cache.SetResourceReloadSuspended(true);

        CleanupInvalidatedAssets();
        CleanupCacheFolder();
        _scanAssets = true;
        _hasInvalidAssets = false;
    }

    private void ScanAndQueueAssetProcessing()
    {
        if (!_scanAssets)
            return;

        var stats = new Stats();
        ScanAssetsInPath(string.Empty, stats);

        Log.GetLogger().LogInformation(
            "Assets scanned: {Processed} processed, {UpToDate} up-to-date, {Ignored} ignored",
            stats.NumProcessedAssets, stats.NumUpToDateAssets, stats.NumIgnoredAssets);

        _scanAssets = false;
    }

    private void ScanAssetsInPath(string resourcePath, Stats stats)
    {
        var queuedAssets = new List<string>();
        foreach (var resourceName in EnumerateAssetFiles(resourcePath))
        {
            if (!_assets.TryGetValue(resourceName, out var assetDesc))
            {
                var batch = new List<AssetTransformerInput>();
                if (QueueAssetProcessing(batch, resourceName, _defaultFlavor, false /*isPostTransform*/))
                {
                    queuedAssets.Add(resourceName);
                    if (batch.Count != 0)
                    {
                        _requestQueue.Add(batch);
                        _progress = _progress with { NumAssetsTotal = _progress.NumAssetsTotal + 1 };
                    }
                    ++stats.NumProcessedAssets;
                }
                else
                    ++stats.NumIgnoredAssets;
            }
            else if (assetDesc.Transformers.Count == 0)
                ++stats.NumIgnoredAssets;
            else
                ++stats.NumUpToDateAssets;
        }

        if (queuedAssets.Count != 0)
        {
            _requestQueue.Add([]); // Asset barrier

            foreach (var processedDirectory in EnumerateDisjoinedDirectories(queuedAssets))
            {
                var batch = new List<AssetTransformerInput>();
                foreach (var resourceName in EnumerateAssetFiles(processedDirectory))
                    QueueAssetProcessing(batch, resourceName, _defaultFlavor, true /*isPostTransform*/);

                if (batch.Count != 0)
                {
                    _requestQueue.Add(batch);
                    _progress = _progress with { NumAssetsTotal = _progress.NumAssetsTotal + 1 };
                }
            }
        }

        _requestQueue.Reverse();
    }

    private bool QueueAssetProcessing(List<AssetTransformerInput> queue, string resourceName,
        in ApplicationFlavor flavor, bool isPostTransform)
    {
        var transformers = _transformerHierarchy[isPostTransform ? 1 : 0]
            .GetTransformerCandidates(resourceName, flavor);
        var fileName = GetFileName(resourceName);
        var assetModifiedTime = GetLastModifiedTime(fileName);

        if (!_assets.TryGetValue(resourceName, out var assetDesc))
        {
            assetDesc = new AssetDesc();
            _assets.Add(resourceName, assetDesc);
        }
        assetDesc.ResourceName = resourceName;
        assetDesc.ModificationTime = assetModifiedTime;

        var input = new AssetTransformerInput(isPostTransform, flavor, resourceName, fileName, assetModifiedTime);
        if (!AssetTransformer.IsApplicable(input, transformers))
        {
            // If post-transformed asset changed, return true to queue post-transform.
            // Don't queue anything right now.
            if (!isPostTransform)
            {
                var postTransformers = _transformerHierarchy[1].GetTransformerCandidates(resourceName, flavor);
                return postTransformers.Count != 0;
            }

            return false;
        }

        // In most cases, everything is already cleared.
        // However, post-transformer processing may be triggered by changes in other files in the directory.
        // Cleanup all known outputs just in case.
        CleanupAssetOutputs(assetDesc);

        var tempPath = Project.GetRandomTemporaryPath();
        var outputResourceName = resourceName + ".d";
        var outputFileName = tempPath + outputResourceName;
        queue.Add(new AssetTransformerInput(input, tempPath, outputFileName, outputResourceName));
        return true;
    }

    private void ConsumeAssetQueue()
    {
        var queue = new List<List<AssetTransformerInput>>();
        while (_requestQueue.Count != 0 && _numOngoingRequests < _maxConcurrentRequests)
        {
            var lastIndex = _requestQueue.Count - 1;
            if (_requestQueue[lastIndex].Count == 0)
            {
                if (_numOngoingRequests != 0)
                    break;

                _requestQueue.RemoveAt(lastIndex);
                continue;
            }

            ++_numOngoingRequests;
            queue.Add(_requestQueue[lastIndex]);
            _requestQueue.RemoveAt(lastIndex);
        }

        foreach (var input in queue)
            _processCallback(input, CompleteAssetBatchProcessing);
    }

    private bool CompleteAssetProcessing(AssetTransformerInput input, AssetTransformerOutput? output)
    {
        if (output is null)
            return false;

        if (!_assets.TryGetValue(input.ResourceName, out var assetDesc))
        {
            assetDesc = new AssetDesc();
            _assets.Add(input.ResourceName, assetDesc);
        }
        assetDesc.ResourceName = input.ResourceName;
        assetDesc.ModificationTime = input.InputFileTime;
        assetDesc.DependencyModificationTimes = new Dictionary<string, uint>(
            output.DependencyModificationTimes, StringComparer.Ordinal);
        assetDesc.Outputs = [.. output.OutputResourceNames];
        assetDesc.Transformers = new HashSet<string>(output.AppliedTransformers, StringComparer.Ordinal);

        if (output.SourceModified)
            _ignoredAssetUpdates.Add(input.ResourceName);

        Log.GetLogger().LogDebug(
            "Asset {ResourceName} was processed with {Transformers} ({GeneratedFiles} files generated, "
            + "{ModifiedFiles} files modified{SourceModified})",
            input.ResourceName, assetDesc.GetTransformerDebugString(), assetDesc.Outputs.Count,
            output.ModifiedResourceNames.Count, output.SourceModified ? ", source modified" : string.Empty);

        return true;
    }

    private void CompleteAssetBatchProcessing(
        List<AssetTransformerInput> input, List<AssetTransformerOutput?> output, string message)
    {
        if (_numOngoingRequests > 0)
            --_numOngoingRequests;
        else
            System.Diagnostics.Debug.Fail(
                "AssetManager::CompleteAssetProcessing() called with no ongoing requests");

        _progress = _progress with { NumProcessedAssets = _progress.NumProcessedAssets + 1 };

        var batchSize = input.Count;
        System.Diagnostics.Debug.Assert(output.Count == 0 || output.Count == batchSize,
            "AssetManager::CompleteAssetProcessing() called with invalid output size");

        for (var i = 0; i < batchSize; ++i)
        {
            if (!CompleteAssetProcessing(input[i], i < output.Count ? output[i] : null))
                Log.GetLogger().LogWarning("Asset {ResourceName} was not processed", input[i].ResourceName);
        }

        if (message.Length != 0)
            Log.GetLogger().LogWarning("{Message}", message);
    }

    private void OnReflectionRemoved(ObjectReflection reflection)
    {
        var anyRemoved = false;
        foreach (var transformerHierarchy in _transformerHierarchy)
            anyRemoved = transformerHierarchy.RemoveTransformers(reflection.ManagedType) || anyRemoved;

        if (anyRemoved)
        {
            InvalidateAssetsInPath(string.Empty);
            _assetPipelines.Clear();
            _reloadAssetPipelines = true;

            var cache = GetSubsystem<ResourceCache>();
            cache.ReleaseResources<AssetPipeline>();
        }
    }

    private Project Project
    {
        get
        {
            _project.TryGetTarget(out var project);
            return project!;
        }
    }

    private sealed class AssetPipelineDesc
    {
        public string ResourceName = string.Empty;
        public uint ModificationTime;
        public List<AssetTransformer> Transformers = [];
        public List<AssetTransformerDependency> Dependencies = [];
    }

    private sealed class AssetPipelineDiff
    {
        public AssetPipelineDesc? OldPipeline;
        public AssetPipelineDesc? NewPipeline;
    }

    private sealed class AssetDesc
    {
        public string ResourceName = string.Empty;
        public List<string> Outputs = [];
        public HashSet<string> Transformers = new(StringComparer.Ordinal);
        public uint ModificationTime;
        public Dictionary<string, uint> DependencyModificationTimes = new(StringComparer.Ordinal);

        public bool CacheInvalid;

        public void SerializeInBlock(Archive archive)
        {
            SerializeVectorOptional(archive, "outputs", Outputs, ArchiveSerialization.SerializeValue);
            SerializeSetOptional(archive, "transformers", Transformers, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "modificationTime", ref ModificationTime, 0u, ArchiveSerialization.SerializeValue);
            SerializeMapOptional(archive, "dependencyModificationTimes", DependencyModificationTimes,
                ArchiveSerialization.SerializeValue);
        }

        public bool IsAnyTransformerUsed(IReadOnlyList<string> transformers)
        {
            foreach (var transformer in transformers)
            {
                if (Transformers.Contains(transformer))
                    return true;
            }
            return false;
        }

        public string GetTransformerDebugString()
        {
            var transformers = Transformers.ToList();
            transformers.Sort(StringComparer.Ordinal);
            return string.Join(", ", transformers);
        }
    }

    private sealed class Stats
    {
        public int NumProcessedAssets;
        public int NumIgnoredAssets;
        public int NumUpToDateAssets;
    }

    private readonly WeakReference<Project> _project;
    private readonly FileWatcher _dataWatcher;

    private ProcessAssetQueued _processCallback = null!;
    private int _maxConcurrentRequests;

    private ApplicationFlavor _defaultFlavor = new(); // TODO(editor): Make configurable

    private bool _initialized;
    private bool _autoProcessAssets;
    private bool _reloadAssetPipelines;
    private bool _hasInvalidAssets;
    private bool _scanAssets;

    private List<AssetPipelineDesc> _assetPipelines = [];
    private readonly AssetTransformerHierarchy[] _transformerHierarchy = new AssetTransformerHierarchy[2];
    private readonly Dictionary<string, AssetDesc> _assets = new(StringComparer.Ordinal);
    private SortedDictionary<string, uint> _assetPipelineFiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ignoredAssetUpdates = new(StringComparer.Ordinal);

    private readonly List<List<AssetTransformerInput>> _requestQueue = [];
    private readonly List<string> _allPathUpdates = [];
    private readonly List<string> _compressedPathUpdates = [];
    private int _numOngoingRequests;

    private ProgressInfo _progress;

    private static void SerializeAssetDesc(Archive archive, string name, ref AssetDesc value)
    {
        value ??= new AssetDesc();
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }

    private static void SerializeMapOptional<T>(Archive archive, string name, IDictionary<string, T> value,
        ArchiveValueSerializer<T> serializeValue)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : value.Count != 0);
        if (serialize)
            ArchiveSerialization.SerializeMap(archive, name, value, ArchiveSerialization.SerializeValue, serializeValue);
        else if (archive.IsInput)
            value.Clear();
    }

    private static void SerializeVectorOptional<T>(Archive archive, string name, List<T> value,
        ArchiveValueSerializer<T> serializeValue)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : value.Count != 0);
        if (serialize)
            ArchiveSerialization.SerializeVector(archive, name, value, serializeValue);
        else if (archive.IsInput)
            value.Clear();
    }

    private static void SerializeSetOptional<T>(Archive archive, string name, ISet<T> value,
        ArchiveValueSerializer<T> serializeValue)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : value.Count != 0);
        if (serialize)
            ArchiveSerialization.SerializeSet(archive, name, value, serializeValue);
        else if (archive.IsInput)
            value.Clear();
    }

    private static string GetResourcePath(string fullPath)
    {
        var normalized = fullPath.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash == -1 ? string.Empty : normalized[..(slash + 1)];
    }

    private static string AddTrailingSlash(string path)
    {
        if (path.Length == 0 || path[^1] == '/')
            return path;
        return path + "/";
    }

    private static uint GetLastModifiedTime(string fileName)
    {
        if (!File.Exists(fileName))
            return 0;

        try
        {
            var modifiedTime = File.GetLastWriteTimeUtc(fileName);
#if REDBOLT_PLATFORM_WINDOWS
            {
                var creationTime = File.GetCreationTimeUtc(fileName);
                if (creationTime > modifiedTime)
                    modifiedTime = creationTime;
            }
#endif
            var seconds = new DateTimeOffset(modifiedTime).ToUnixTimeSeconds();
            return seconds > 0 ? unchecked((uint)seconds) : 0u;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static void TryDeleteFile(string fileName)
    {
        try
        {
            File.Delete(fileName);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
