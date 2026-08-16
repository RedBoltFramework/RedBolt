// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

/// <summary>Base class for post-transformers.</summary>
public abstract class BaseAssetPostTransformer : AssetTransformer
{
    public BaseAssetPostTransformer()
    {
    }

    /// <summary>File name of parameter file connected to this transformer.</summary>
    public abstract string GetParametersFileName();

    /// <summary>Implement AssetTransformer.</summary>
    public override bool IsApplicable(AssetTransformerInput input)
    {
        return input.ResourceName.EndsWith(GetParametersFileName(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Implement AssetTransformer.</summary>
    public override bool IsPostTransform() => true;

    /// <summary>Load parameters from file.</summary>
    protected T LoadParameters<T>(string fileName)
        where T : IArchiveSerializable, new()
    {
        var result = new T();

        using var file = new JSONFile();
        if (file.LoadFile(FileIdentifier.FromUri(fileName)))
        {
            if (file.LoadObject("params", result))
                return result;
        }

        return new T();
    }

    protected struct PatternMatch
    {
        public string FileName = string.Empty;
        public string Match = string.Empty;

        public PatternMatch()
        {
        }
    }

    /// <summary>Return files in resource directory that match the template.</summary>
    protected List<PatternMatch> GetResourcesByPattern(string baseResourceName, string fileNamePattern)
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var fileNames = new List<string>();
        cache.Scan(fileNames, baseResourceName, "*", ScanFlags.ScanFiles | ScanFlags.ScanRecursive);

        var result = new List<PatternMatch>();
        var regex = PatternToRegex(fileNamePattern);
        foreach (var fileName in fileNames)
        {
            var match = regex.Match(fileName);
            if (match.Success && match.Index == 0 && match.Length == fileName.Length)
            {
                result.Add(new PatternMatch
                {
                    FileName = fileName,
                    Match = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty,
                });
            }
        }
        return result;
    }

    /// <summary>Return file name for template match.</summary>
    protected string GetMatchFileName(string fileNameTemplate, in PatternMatch match)
    {
        var placeholder = fileNameTemplate.IndexOf("{}", StringComparison.Ordinal);
        if (placeholder < 0)
            return fileNameTemplate;
        return fileNameTemplate[..placeholder] + match.Match + fileNameTemplate[(placeholder + 2)..];
    }

    private static Regex PatternToRegex(string pattern)
    {
        var result = new StringBuilder();
        foreach (var ch in pattern)
        {
            if (ch == '*')
                result.Append("(.*)");
            else
            {
                if (ch is '[' or ']' or '(' or ')' or '{' or '}' or '*' or '+' or '?' or '|'
                    or '^' or '$' or '.' or '\\')
                {
                    result.Append('\\');
                }
                result.Append(ch);
            }
        }
        return new Regex(result.ToString(),
            RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
