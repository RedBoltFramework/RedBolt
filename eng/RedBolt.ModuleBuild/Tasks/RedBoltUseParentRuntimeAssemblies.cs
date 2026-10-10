// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// Rewrites an application's deps.json so it consumes assemblies and native assets from the shared parent build output.
/// </summary>
public sealed class RedBoltUseParentRuntimeAssemblies : Microsoft.Build.Utilities.Task
{
    [Required]
    public string DependencyFile { get; set; }

    [Required]
    public string MainProjectName { get; set; }

    public string NativeRuntimeDirectory { get; set; }

    public string NativeModuleManifestDirectory { get; set; }

    public string RuntimeIdentifier { get; set; }

    [Output]
    public Microsoft.Build.Framework.ITaskItem[] NativeAssets { get; set; }

    public override bool Execute()
    {
        var contents = File.ReadAllText(DependencyFile);
        var expression = new Regex(
            "^(?<prefix>\\s*\\\"(?<name>[^\\\"]+)/[^\\\"]+\\\": \\{\\r?\\n(?<indent>\\s*))"
                + "\\\"type\\\": \\\"(?:project|reference|package)\\\","
                + "(?:\\r?\\n\\s*\\\"path\\\": \\\"[^\\\"]+\\\",)?",
            RegexOptions.Multiline);
        var transformed = expression.Replace(
            contents,
            match => match.Groups["name"].Value == MainProjectName
                ? match.Value
                : match.Groups["prefix"].Value
                    + "\"type\": \"package\","
                    + Environment.NewLine
                    + match.Groups["indent"].Value
                    + "\"path\": \".\",");
        transformed = Regex.Replace(
            transformed,
            "^\\s*\\\"path\\\": (?!\\\"\\.\\\")\\\"[^\\\"]+\\\",\\r?\\n",
            "",
            RegexOptions.Multiline);
        var managedPackageAsset = new Regex(
            "\\\"(?:lib|runtimes/[^/\\\"]+/lib)/[^\\\"]*/(?<file>(?![^/\\\"]+\\.resources\\.dll)[^/\\\"]+\\.dll)\\\":");
        transformed = managedPackageAsset.Replace(
            transformed,
            match => "\"" + match.Groups["file"].Value + "\":");

        NativeAssets = Array.Empty<Microsoft.Build.Framework.ITaskItem>();
        if (!string.IsNullOrEmpty(RuntimeIdentifier))
        {
            if (!Directory.Exists(NativeModuleManifestDirectory))
                throw new InvalidOperationException(
                    "RedBolt native module manifests were not generated at '"
                    + NativeModuleManifestDirectory + "'.");

            var selectorsByModule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var artifactsByModule = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var requiresByModule = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            string ReadProperty(string json, string property)
            {
                var match = Regex.Match(json,
                    "\\\"" + Regex.Escape(property) + "\\\"\\s*:\\s*\\\"(?<value>[^\\\"]+)\\\"");
                return match.Success ? match.Groups["value"].Value : "";
            }

            List<string> ReadArray(string json, string property)
            {
                var match = Regex.Match(json,
                    "\\\"" + Regex.Escape(property) + "\\\"\\s*:\\s*\\[(?<values>.*?)\\]",
                    RegexOptions.Singleline);
                if (!match.Success)
                    return new List<string>();
                return Regex.Matches(match.Groups["values"].Value, "\\\"(?<value>[^\\\"]+)\\\"")
                    .Cast<Match>()
                    .Select(value => value.Groups["value"].Value)
                    .ToList();
            }

            var indexFile = Path.Combine(NativeModuleManifestDirectory, "index.json");
            IEnumerable<string> manifestFiles;
            if (File.Exists(indexFile))
            {
                var index = File.ReadAllText(indexFile);
                manifestFiles = Regex.Matches(
                        index,
                        "^    \\\"(?<module>[^\\\"]+)\\\": \\{",
                        RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(match => Path.Combine(
                        NativeModuleManifestDirectory,
                        match.Groups["module"].Value + ".json"));
            }
            else
            {
                manifestFiles = Directory.GetFiles(NativeModuleManifestDirectory, "*.json")
                    .Where(path => !Path.GetFileName(path).Equals(
                        "index.json",
                        StringComparison.OrdinalIgnoreCase));
            }

            foreach (var manifestFile in manifestFiles)
            {
                if (!File.Exists(manifestFile))
                    throw new InvalidOperationException(
                        "Indexed RedBolt native module manifest was not generated at '"
                        + manifestFile + "'.");
                var manifest = File.ReadAllText(manifestFile);
                var module = ReadProperty(manifest, "module");
                var selector = ReadProperty(manifest, "managedSelector");
                if (module.Length == 0 || selector.Length == 0)
                    throw new InvalidOperationException("Invalid RedBolt native module manifest '" + manifestFile + "'.");
                selectorsByModule[module] = selector;
                artifactsByModule[module] = ReadArray(manifest, "artifacts");
                requiresByModule[module] = ReadArray(manifest, "requires");
            }

            var selectedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in selectorsByModule)
            {
                var selectorPattern = "\\\"" + Regex.Escape(module.Value) + "(?:/[^\\\"]+)?\\\"\\s*:\\s*\\{";
                if (Regex.IsMatch(contents, selectorPattern))
                    selectedModules.Add(module.Key);
            }

            var modulesAdded = true;
            while (modulesAdded)
            {
                modulesAdded = false;
                foreach (var module in selectedModules.ToArray())
                {
                    if (!requiresByModule.TryGetValue(module, out var requires))
                        continue;
                    foreach (var required in requires)
                        if (selectedModules.Add(required))
                            modulesAdded = true;
                }
            }

            var nativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nativeFilesBySelector = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in selectedModules)
            {
                if (!artifactsByModule.TryGetValue(module, out var artifacts))
                    throw new InvalidOperationException("Native module '" + module + "' is not registered.");
                var selector = selectorsByModule[module];
                if (!nativeFilesBySelector.TryGetValue(selector, out var selectorFiles))
                {
                    selectorFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    nativeFilesBySelector[selector] = selectorFiles;
                }
                foreach (var artifact in artifacts)
                {
                    var path = Path.Combine(NativeRuntimeDirectory, artifact);
                    if (!File.Exists(path))
                        throw new FileNotFoundException("Native module artifact was not built.", path);
                    nativeFiles.Add(artifact);
                    selectorFiles.Add(artifact);
                }
            }
            NativeAssets = nativeFiles
                .OrderBy(path => path)
                .Select(path => (Microsoft.Build.Framework.ITaskItem)new TaskItem(
                    Path.Combine(NativeRuntimeDirectory, path)))
                .ToArray();

            void AddNativeAssets(string selector, IEnumerable<string> files)
            {
                var runtimeTargetIndex = transformed.IndexOf("/" + RuntimeIdentifier + "\": {");
                if (runtimeTargetIndex < 0)
                    throw new InvalidOperationException(
                        "Runtime target '" + RuntimeIdentifier + "' was not found in '" + DependencyFile + "'.");
                var libraryIndex = transformed.IndexOf("\"" + selector + "/", runtimeTargetIndex);
                if (libraryIndex < 0)
                    throw new InvalidOperationException(
                        "Managed selector '" + selector + "' was not found in '" + DependencyFile + "'.");
                int FindObjectEnd(int objectStart)
                {
                    var depth = 0;
                    var inString = false;
                    var escaped = false;
                    for (var index = objectStart; index < transformed.Length; ++index)
                    {
                        var character = transformed[index];
                        if (inString)
                        {
                            if (escaped)
                                escaped = false;
                            else if (character == '\\')
                                escaped = true;
                            else if (character == '"')
                                inString = false;
                        }
                        else if (character == '"')
                        {
                            inString = true;
                        }
                        else if (character == '{')
                        {
                            ++depth;
                        }
                        else if (character == '}' && --depth == 0)
                        {
                            return index;
                        }
                    }
                    return -1;
                }

                var objectStart = transformed.IndexOf('{', libraryIndex);
                var objectEnd = FindObjectEnd(objectStart);
                if (objectEnd < 0)
                    throw new InvalidOperationException(
                        "Managed selector '" + selector + "' has an invalid dependency entry.");

                var nativeEntries = string.Join(
                    "," + Environment.NewLine,
                    files
                        .OrderBy(path => path)
                        .Select(path => "        \"runtimes/" + RuntimeIdentifier + "/native/"
                            + path + "\": {}"));
                var nativePropertyIndex = transformed.IndexOf(
                    "\"native\": {", objectStart, objectEnd - objectStart);
                if (nativePropertyIndex >= 0)
                {
                    var nativeObjectStart = transformed.IndexOf('{', nativePropertyIndex);
                    var nativeObjectEnd = FindObjectEnd(nativeObjectStart);
                    if (nativeObjectEnd < 0 || nativeObjectEnd > objectEnd)
                        throw new InvalidOperationException(
                            "Managed selector '" + selector + "' has an invalid native dependency entry.");
                    transformed = transformed.Remove(
                        nativeObjectStart + 1,
                        nativeObjectEnd - nativeObjectStart - 1);
                    transformed = transformed.Insert(
                        nativeObjectStart + 1,
                        Environment.NewLine + nativeEntries + Environment.NewLine + "      ");
                    return;
                }

                var closingLineStart = transformed.LastIndexOf('\n', objectEnd) + 1;
                transformed = transformed.Insert(closingLineStart - 1, ",");
                ++closingLineStart;
                transformed = transformed.Insert(
                    closingLineStart,
                    "      \"native\": {" + Environment.NewLine
                        + nativeEntries + Environment.NewLine
                        + "      }" + Environment.NewLine);
            }

            foreach (var selector in nativeFilesBySelector)
                AddNativeAssets(selector.Key, selector.Value);
        }

        File.WriteAllText(DependencyFile, transformed);

        return !Log.HasLoggedErrors;
    }
}
