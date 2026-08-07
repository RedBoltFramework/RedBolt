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

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Resource;
using RedBolt.Utility;

public static class ConcurrentAssetProcessing
{
    private struct ConcurrentAssetProcessingSettings : ISimpleSettingsPageValues
    {
        public ConcurrentAssetProcessingSettings()
        {
            MaxConcurrency = 4;
        }

        public string UniqueName => "Editor.Assets:Concurrency";

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "MaxConcurrency", ref MaxConcurrency,
                new ConcurrentAssetProcessingSettings().MaxConcurrency,
                ArchiveSerialization.SerializeValue);
        }

        public void RenderSettings()
        {
            ImGui.SliderInt("Max Concurrency"u8, ref MaxConcurrency, 1, 16);
            //
        }

        public int MaxConcurrency;
    }

    private sealed class ConcurrentAssetProcessingSettingsPage
        : SimpleSettingsPage<ConcurrentAssetProcessingSettings>
    {
        public ConcurrentAssetProcessingSettingsPage(Action<int> callback)
        {
            _updateCallback = callback;
        }

        public override void RenderSettings()
        {
            var maxConcurrency = Values.MaxConcurrency;

            base.RenderSettings();

            if (Values.MaxConcurrency != maxConcurrency)
                _updateCallback(Values.MaxConcurrency);
        }

        public override void SerializeInBlock(Archive archive)
        {
            base.SerializeInBlock(archive);

            if (archive.IsInput)
                _updateCallback(Values.MaxConcurrency);
        }

        private readonly Action<int> _updateCallback;
    }

    private const string CommandName = "ProcessAsset";

    private static void RequestProcessAsset(Project project, List<AssetTransformerInput> input,
        AssetManager.ProcessAssetCompleted callback)
    {
        var tempDir = project.CreateTemporaryDir();
        var isTempDirCaptured = false;
        try
        {
            // Strip the base path to avoid having spaces in the path passed to the command
            var projectPath = project.ProjectPath;
            var relativeTempPath = tempDir.Path[projectPath.Length..];

            var inputPath = relativeTempPath + "input.json";
            var outputPath = relativeTempPath + "output.json";

            using var inputFile = new JSONFile();
            if (!inputFile.SaveObjectCallback(archive =>
                    ArchiveSerialization.SerializeVector(archive, "input", input, SerializeInput)))
            {
                callback(input, [], "Cannot serialize input pipe");
                return;
            }

            if (!inputFile.SaveFile(new FileIdentifier(projectPath + inputPath)))
            {
                callback(input, [], "Cannot save input pipe");
                return;
            }

            var command = $"{CommandName} {inputPath} {outputPath}";
            var inputCopy = CloneInputs(input);
            project.ExecuteRemoteCommandAsync(command, (success, commandOutput) =>
            {
                try
                {
                    if (!success)
                    {
                        callback(inputCopy, [], commandOutput);
                        return;
                    }

                    using var outputFile = new JSONFile();
                    if (!outputFile.LoadFile(new FileIdentifier(projectPath + outputPath)))
                    {
                        callback(inputCopy, [], "Cannot load output pipe");
                        return;
                    }

                    List<AssetTransformerOutput?> output = [];
                    if (!outputFile.LoadObjectCallback(archive =>
                            ArchiveSerialization.SerializeVector(archive, "output", output, SerializeOutput)))
                    {
                        callback(inputCopy, [], "Cannot deserialize output pipe");
                        return;
                    }

                    callback(inputCopy, output, commandOutput);
                }
                finally
                {
                    tempDir.Dispose();
                }
            });
            isTempDirCaptured = true;
        }
        finally
        {
            if (!isTempDirCaptured)
                tempDir.Dispose();
        }
    }

    private static bool ProcessAsset(Project project, string inputName, string outputName)
    {
        var assetManager = project.AssetManager;

        var projectPath = project.ProjectPath;

        using var inputFile = new JSONFile();
        if (!inputFile.LoadFile(new FileIdentifier(projectPath + inputName)))
        {
            Log.GetLogger().LogError("Cannot load input pipe");
            return false;
        }

        List<AssetTransformerInput> input = [];
        if (!inputFile.LoadObjectCallback(archive =>
                ArchiveSerialization.SerializeVector(archive, "input", input, SerializeInput)))
        {
            Log.GetLogger().LogError("Cannot deserialize input pipe");
            return false;
        }

        var success = false;
        assetManager.ProcessAssetBatch(input,
            (processedInput, output, error) =>
        {
            // Processing errors should be logged by the asset manager

            using var outputFile = new JSONFile();
            if (!outputFile.SaveObjectCallback(archive =>
                    ArchiveSerialization.SerializeVector(archive, "output", output, SerializeOutput)))
            {
                Log.GetLogger().LogError("Cannot serialize output pipe");
                return;
            }

            if (!outputFile.SaveFile(new FileIdentifier(projectPath + outputName)))
            {
                Log.GetLogger().LogError("Cannot save output pipe");
                return;
            }

            success = true;
        });

        return success;
    }

    private static List<AssetTransformerInput> CloneInputs(IReadOnlyList<AssetTransformerInput> input)
    {
        var result = new List<AssetTransformerInput>(input.Count);
        foreach (var value in input)
            result.Add(value.Clone());
        return result;
    }

    private static void SerializeInput(Archive archive, string name, ref AssetTransformerInput value)
    {
        value ??= new AssetTransformerInput();
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }

    private static void SerializeOutput(Archive archive, string name, ref AssetTransformerOutput? value)
    {
        using var block = archive.OpenUnorderedBlock(name);

        var hasValue = value is not null;
        ArchiveSerialization.SerializeValue(archive, "hasValue", ref hasValue);

        if (archive.IsInput)
        {
            if (hasValue)
            {
                value ??= new AssetTransformerOutput();
                using var valueBlock = archive.OpenUnorderedBlock("value");
                value.SerializeInBlock(archive);
            }
            else
                value = null;
        }
        else if (hasValue)
        {
            using var valueBlock = archive.OpenUnorderedBlock("value");
            value!.SerializeInBlock(archive);
        }
    }

    public static void Register(Project project)
    {
        var assetManager = new WeakReference<AssetManager>(project.AssetManager);

        project.OnCommand += (Project sender, string command, string args, ref bool processed) =>
        {
            var argsVector = args.Split(' ');
            if (!string.Equals(command, CommandName, StringComparison.Ordinal) || argsVector.Length != 2)
                return;

            var inputName = argsVector[0];
            var outputName = argsVector[1];

            if (ProcessAsset(project, inputName, outputName))
                processed = true;
        };

        if ((project.Flags & ProjectFlag.SingleProcess) == 0)
        {
            AssetManager.ProcessAssetQueued callback = (input, completionCallback) => //
            { //
                RequestProcessAsset(project, input, completionCallback);
            };

            void UpdateMaxConcurrency(int value)
            {
                if (assetManager.TryGetTarget(out var target))
                    target.SetProcessCallback(callback, value);
            }

            UpdateMaxConcurrency(new ConcurrentAssetProcessingSettings().MaxConcurrency);

            var settingsPage = new ConcurrentAssetProcessingSettingsPage(UpdateMaxConcurrency);
            project.SettingsManager.AddPage(settingsPage);
        }
    }
}
