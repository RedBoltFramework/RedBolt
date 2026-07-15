using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Application flavor component type.</summary>
public class ApplicationFlavorComponent : HashSet<string>
{
    public ApplicationFlavorComponent() : base(StringComparer.Ordinal) { }
    public ApplicationFlavorComponent(IEnumerable<string> collection) : base(collection, StringComparer.Ordinal) { }
}

/// <summary>Application flavor map type.</summary>
public class ApplicationFlavorMap : Dictionary<string, ApplicationFlavorComponent>
{
    public ApplicationFlavorMap() : base(StringComparer.Ordinal) { }
}

/// <summary>Class that represents pattern of flavor components.</summary>
public struct ApplicationFlavorPattern
{
    private static readonly Regex FlavorComponentRegex = new(
        @"\A\s*([\w\*]*)\s*=\s*((?:[\w\*]+\s*,\s*)*(?:[\w\*]+))\s*\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Flavor components.</summary>
    public ApplicationFlavorMap Components;

    /// <summary>Construct.</summary>
    public ApplicationFlavorPattern() { Components = new ApplicationFlavorMap(); }

    /// <summary>Construct from string representation.</summary>
    public ApplicationFlavorPattern(string str) : this() { Components = ParseFlavorString(str); }

    internal ApplicationFlavorPattern(ApplicationFlavorMap components)
    {
        Components = components;
    }

    internal static ApplicationFlavorMap ParseFlavorString(string str)
    {
        var result = new ApplicationFlavorMap();
        var components = str.Split(';');
        foreach (var component in components)
        {
            var match = FlavorComponentRegex.Match(component);
            if (!match.Success)
            {
                Log.Logger.LogError(
                    "Invalid application flavor component: '{Component}', should be like 'component=tag1[,tag2,...]'",
                    component);
                continue;
            }

            var key = match.Groups[1].Value;
            var tags = match.Groups[2].Value.Split(',');

            if (!result.TryGetValue(key, out var tagSet))
            {
                tagSet = new ApplicationFlavorComponent();
                result[key] = tagSet;
            }

            foreach (var tag in tags)
                tagSet.Add(tag);
        }

        return result;
    }
}

/// <summary>Class that represents specific set of flavor components.</summary>
public struct ApplicationFlavor
{
    /// <summary>Universal flavor matches any pattern.</summary>
    public static readonly ApplicationFlavor Universal = new(new ApplicationFlavorMap
    {
        ["*"] = new ApplicationFlavorComponent { "*" }
    });

    /// <summary>Empty flavor matches only empty patterns.</summary>
    public static readonly ApplicationFlavor Empty = new(new ApplicationFlavorMap());

    /// <summary>Flavor of current platform.</summary>
    public static readonly ApplicationFlavor Platform = CreatePlatformFlavor();

    /// <summary>Flavor components.</summary>
    public ApplicationFlavorMap Components;

    /// <summary>Construct.</summary>
    public ApplicationFlavor() { Components = new ApplicationFlavorMap(); }

    /// <summary>Construct from string representation.</summary>
    public ApplicationFlavor(string str) : this() { Components = ApplicationFlavorPattern.ParseFlavorString(str); }

    internal ApplicationFlavor(ApplicationFlavorMap components)
    {
        Components = components;
    }

    /// <summary>Returns distance (smaller is better) if flavor matches the pattern. Returns null if doesn't match.</summary>
    public readonly uint? Matches(ApplicationFlavorPattern pattern)
    {
        if (ComponentsEqual(Universal.Components))
            return uint.MaxValue;

        uint distance = 0;
        foreach (var (key, patternTags) in pattern.Components)
        {
            if (patternTags.Contains("*"))
                continue;

            if (!Components.TryGetValue(key, out var flavorTags))
                return null;

            if (flavorTags.Contains("*"))
            {
                distance += (uint)patternTags.Count;
                continue;
            }

            foreach (var tag in patternTags)
            {
                if (!flavorTags.Contains(tag))
                    return null;
            }

            distance += (uint)(flavorTags.Count - patternTags.Count);
        }

        foreach (var (key, flavorTags) in Components)
        {
            if (pattern.Components.TryGetValue(key, out var patternTags) && !patternTags.Contains("*"))
                continue;

            if (flavorTags.Contains("*"))
                continue;

            distance += (uint)flavorTags.Count;
        }

        return distance;
    }

    /// <summary>Returns string representation of flavor.</summary>
    public override readonly string ToString()
    {
        var result = string.Empty;
        var sortedKeys = Components.Keys.OrderBy(k => k, StringComparer.Ordinal);
        foreach (var key in sortedKeys)
        {
            if (result.Length > 0)
                result += ";";

            result += key + "=";
            var tags = Components[key];
            var sortedTags = tags.OrderBy(t => t, StringComparer.Ordinal);
            result += string.Join(",", sortedTags);
        }

        return result;
    }

    private readonly bool ComponentsEqual(ApplicationFlavorMap other)
    {
        if (Components.Count != other.Count)
            return false;
        foreach (var (key, tags) in Components)
        {
            if (!other.TryGetValue(key, out var otherTags))
                return false;
            if (!tags.SetEquals(otherTags))
                return false;
        }
        return true;
    }

    private static ApplicationFlavor CreatePlatformFlavor()
    {
        string platformStr;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            platformStr = "platform=desktop,windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platformStr = "platform=desktop,macos";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            platformStr = "platform=desktop,linux";
        else
            platformStr = string.Empty;

        return string.IsNullOrEmpty(platformStr) ? Empty : new ApplicationFlavor(platformStr);
    }
}
