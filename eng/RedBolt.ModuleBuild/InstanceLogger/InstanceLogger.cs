// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.ModuleBuild.InstanceLogger;

using System.Text;
using Microsoft.Build.Framework;

// Reports projects that build more than once with different global properties in one build.
// An instance that only reports "skipped" (a project that is not selected for its platform) writes no
// files. INSTANCES marks it "skipped" and DUPLICATES leaves it out.
// Usage: -logger:<dll>;<output file>
public sealed class InstanceLogger : ILogger
{
    // Global properties that MSBuild adds on its own. They do not make a real difference between instances.
    private static readonly string[] IgnoredProperties =
    {
        "MSBuildRestoreSessionId",
        "CurrentSolutionConfigurationContents",
        "SolutionDir",
        "SolutionExt",
        "SolutionFileName",
        "SolutionName",
        "SolutionPath",
        "BuildingSolutionFile",
        "LangID",
        "LangName",
        "DevEnvDir",
    };

    // Global properties that identify an instance of a project. Instances that differ in one of these are
    // not compared, so a project with one output for all platforms (a host tool) is not reported when it
    // builds once for each platform. Examine INSTANCES for such projects.
    private static readonly string[] KeyProperties =
    {
        "Configuration",
        "RedBoltPlatform",
        "TargetFramework",
        "RuntimeIdentifier",
        "MSBuildIsRestoring",
    };

    // Targets that start a real build. Other target lists are query calls.
    private static readonly string[] BuildTargets = { "Build", "Rebuild", "Restore", "Publish" };

    // The target that a project instance runs instead of a build when the project is not selected, and the
    // target that only a real build runs. The names are the targets of RedBolt.ProjectEligibility.targets
    // and of the SDK.
    private const string SkipTarget = "RedBoltReportSkippedProject";
    private const string CompileTarget = "CoreCompile";

    private readonly object _lock = new();
    private readonly HashSet<Instance> _instances = new();
    private readonly HashSet<int> _skippedInstanceIds = new();
    private readonly HashSet<int> _compiledInstanceIds = new();

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Minimal;

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.ProjectStarted += OnProjectStarted;
        eventSource.TargetStarted += OnTargetStarted;
    }

    public void Shutdown()
    {
        if (string.IsNullOrWhiteSpace(Parameters))
            return;

        var path = Path.GetFullPath(Parameters.Trim());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Render(), new UTF8Encoding(false));
    }

    private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.ProjectFile))
            return;

        var targetNames = e.TargetNames ?? string.Empty;
        if (!IsBuildCall(targetNames))
            return;

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (e.GlobalProperties is not null)
        {
            foreach (var pair in e.GlobalProperties)
            {
                if (Array.IndexOf(IgnoredProperties, pair.Key) < 0)
                    properties[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        var instance = new Instance(
            e.ProjectFile,
            targetNames,
            string.Join(";", properties.Select(pair => pair.Key + "=" + pair.Value)),
            properties,
            e.BuildEventContext?.ProjectInstanceId ?? -1);

        lock (_lock)
            _instances.Add(instance);
    }

    // The events of one project instance share ProjectInstanceId, also when other build calls enter the
    // instance again. TargetStarted comes after ProjectStarted, so Render decides at the end.
    private void OnTargetStarted(object sender, TargetStartedEventArgs e)
    {
        var id = e.BuildEventContext?.ProjectInstanceId ?? -1;
        if (id < 0)
            return;

        lock (_lock)
        {
            if (e.TargetName == SkipTarget)
                _skippedInstanceIds.Add(id);
            else if (e.TargetName == CompileTarget)
                _compiledInstanceIds.Add(id);
        }
    }

    // A skipped instance ran the skip target and never compiled in any build call.
    private bool IsSkipped(Instance instance) =>
        _skippedInstanceIds.Contains(instance.ProjectInstanceId)
        && !_compiledInstanceIds.Contains(instance.ProjectInstanceId);

    private static bool IsBuildCall(string targetNames)
    {
        if (string.IsNullOrWhiteSpace(targetNames))
            return true;

        return targetNames
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(name => Array.IndexOf(BuildTargets, name) >= 0);
    }

    private string Render()
    {
        Instance[] instances;
        HashSet<Instance> skipped;
        lock (_lock)
        {
            instances = _instances
                .OrderBy(i => i.Project, StringComparer.Ordinal)
                .ThenBy(i => i.TargetNames, StringComparer.Ordinal)
                .ThenBy(i => i.PropertyText, StringComparer.Ordinal)
                .ToArray();
            skipped = instances.Where(IsSkipped).ToHashSet();
        }

        var text = new StringBuilder();
        text.Append("INSTANCES\n");
        foreach (var instance in instances)
            text.Append($"{instance.Project} | targets={instance.TargetNames} | {instance.PropertyText}"
                + (skipped.Contains(instance) ? " | skipped\n" : "\n"));

        text.Append("DUPLICATES\n");
        var duplicates = instances
            .Where(i => !skipped.Contains(i))
            .GroupBy(GetKey)
            .Where(group => group.Select(i => i.PropertyText).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        if (duplicates.Length == 0)
        {
            text.Append("none\n");
            return text.ToString();
        }

        foreach (var group in duplicates)
        {
            text.Append($"{group.Key}\n");
            var sets = group
                .GroupBy(i => i.PropertyText, StringComparer.Ordinal)
                .Select(set => set.First().Properties)
                .ToArray();

            var names = sets
                .SelectMany(set => set.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal);

            foreach (var name in names)
            {
                var values = sets
                    .Select(set => set.TryGetValue(name, out var value) ? value : "<unset>")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                if (values.Length > 1)
                    text.Append($"  {name}: {string.Join(" | ", values)}\n");
            }
        }

        return text.ToString();
    }

    private static string GetKey(Instance instance)
    {
        var key = new StringBuilder(instance.Project);
        foreach (var name in KeyProperties)
        {
            instance.Properties.TryGetValue(name, out var value);
            key.Append($" | {name}={value ?? string.Empty}");
        }

        return key.ToString();
    }

    private sealed record Instance(
        string Project,
        string TargetNames,
        string PropertyText,
        SortedDictionary<string, string> Properties,
        int ProjectInstanceId)
    {
        // Instances are equal when the project, targets and properties match.
        public bool Equals(Instance? other) =>
            other is not null
            && Project == other.Project
            && TargetNames == other.TargetNames
            && PropertyText == other.PropertyText;

        public override int GetHashCode() => HashCode.Combine(Project, TargetNames, PropertyText);
    }
}
