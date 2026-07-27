using System.Text;

namespace RedBolt;

/// <summary>File identifier, similar to Uniform Resource Identifier (URI).</summary>
/// <remarks>
/// Known differences:
/// <list type="bullet">
/// <item>If URI starts with <c>/</c> or <c>x:/</c> it is treated as <c>file</c> scheme automatically.</item>
/// <item>Host names are not supported for <c>file:</c> scheme. All of <c>file:/path/to/file</c>,
/// <c>file://path/to/file</c>, and <c>file:///path/to/file</c> are supported and denote absolute file path.</item>
/// <item>If URI does not contain <c>:</c>, it is treated as special "empty" scheme,
/// and the entire URI is treated as relative path.</item>
/// <item>Conversion to URI string uses <c>scheme://</c> format.</item>
/// </list>
/// </remarks>
public struct FileIdentifier : ICloneable, IEquatable<FileIdentifier>, IComparable<FileIdentifier>
{
    /// <summary>File identifier that references nothing.</summary>
    public static readonly FileIdentifier Empty = new();

    /// <summary>Construct default.</summary>
    public FileIdentifier()
    {
    }

    /// <summary>Construct from scheme and path (as is).</summary>
    public FileIdentifier(string scheme, string fileName)
    {
        Scheme = scheme;
        FileName = fileName;
    }

    public FileIdentifier(FileIdentifier other)
    {
        Scheme = other.Scheme;
        FileName = other.FileName;
    }

    /// <summary>Deprecated. Use FromUri() instead.</summary>
    public FileIdentifier(string uri)
        : this(FromUri(uri))
    {
    }

    /// <summary>Construct from uri-like path.</summary>
    public static FileIdentifier FromUri(string uri)
    {
        // Special case: absolute path
        if (uri.StartsWith("/", StringComparison.Ordinal)
            || (uri.Length >= 3 && uri[1] == ':' && (uri[2] == '/' || uri[2] == '\\')))
        {
            return new FileIdentifier("file", SanitizeFileName(uri));
        }

        var schemePos = uri.IndexOf(':', StringComparison.Ordinal);

        // Special case: empty scheme
        if (schemePos < 0)
            return new FileIdentifier(string.Empty, SanitizeFileName(uri));

        var scheme = uri[..schemePos];
        var path = uri[(schemePos + 1)..];

        static bool IsSlash(char c) => c == '/';

        var numSlashes = 0;
        while (numSlashes < path.Length && IsSlash(path[numSlashes]))
            ++numSlashes;

        // Special case: file scheme
        if (scheme == "file")
        {
            if (numSlashes == 0 || numSlashes > 3)
                return Empty;

            // Keep one leading slash
            var localPath = path[(numSlashes - 1)..];

            // Windows-like path, e.g. /c:/path/to/file
            if (localPath.Length >= 3 && localPath[2] == ':')
                return new FileIdentifier(scheme, localPath[1..]);

            // Unix-like path, e.h. /path/to/file
            return new FileIdentifier(scheme, localPath);
        }

        // Trim up to two leading slashes for other schemes
        return new FileIdentifier(scheme, path[Math.Min(numSlashes, 2)..]);
    }

    /// <summary>Return URI-like path. Does not always return original path.</summary>
    public readonly string ToUri()
    {
        var scheme = Scheme ?? string.Empty;
        var fileName = FileName ?? string.Empty;

        // Special case: empty scheme
        if (scheme.Length == 0)
            return fileName;

        // Special case: file scheme
        if (scheme == "file")
        {
            if (fileName.Length == 0)
                return string.Empty;
            if (fileName[0] == '/')
                return "file://" + fileName;
            return "file:///" + fileName;
        }

        // Use scheme://path/to/file format by default
        return scheme + "://" + fileName;
    }

    /// <summary>Append path to the current path, adding slash in between if it's missing.</summary>
    /// <remarks>Ignores current scheme restrictions.</remarks>
    public void AppendPath(string path)
    {
        if (path.Length == 0)
            return;

        FileName ??= string.Empty;
        if (FileName.Length == 0)
        {
            FileName = path;
            return;
        }

        if (FileName[^1] != '/' && path[0] != '/')
            FileName += '/';
        if (FileName[^1] == '/' && path[0] == '/')
            path = path[1..];

        FileName += path;
    }

    /// <summary>URI-like scheme. May be empty if not specified.</summary>
    public string Scheme = string.Empty;

    /// <summary>URI-like path to the file.</summary>
    public string FileName = string.Empty;

    /// <summary>Return whether the identifier is empty.</summary>
    public readonly bool IsEmpty() => string.IsNullOrEmpty(Scheme) && string.IsNullOrEmpty(FileName);

    /// <summary>Operators.</summary>
    public static explicit operator bool(FileIdentifier value) => !value.IsEmpty();

    public static bool operator !(FileIdentifier value) => value.IsEmpty();

    public static bool operator <(FileIdentifier lhs, FileIdentifier rhs) => lhs.CompareTo(rhs) < 0;

    public static bool operator >(FileIdentifier lhs, FileIdentifier rhs) => lhs.CompareTo(rhs) > 0;

    public static bool operator ==(FileIdentifier lhs, FileIdentifier rhs) => lhs.Equals(rhs);

    public static bool operator !=(FileIdentifier lhs, FileIdentifier rhs) => !lhs.Equals(rhs);

    public static FileIdentifier operator +(FileIdentifier lhs, string rhs)
    {
        lhs.AppendPath(rhs);
        return lhs;
    }

    public static string SanitizeFileName(string fileName)
    {
        var sanitizedName = new StringBuilder(fileName.Length);
        var segmentStartIndex = 0;

        for (var i = 0; i < fileName.Length; ++i)
        {
            var c = fileName[i];
            if (c == '\\' || c == '/')
            {
                if (ResolvePathSegment(sanitizedName, ref segmentStartIndex))
                {
                    sanitizedName.Append('/');
                    segmentStartIndex = sanitizedName.Length;
                }
            }
            else
            {
                sanitizedName.Append(c);
            }
        }

        ResolvePathSegment(sanitizedName, ref segmentStartIndex);
        // Remove trailing / if it isn't the only one
        if (sanitizedName.Length > 1)
        {
            var lastCharIndex = sanitizedName.Length - 1;
            if (sanitizedName[lastCharIndex] == '/')
                sanitizedName.Length = lastCharIndex;
        }

        return sanitizedName.ToString().Trim();

        static bool ResolvePathSegment(StringBuilder sanitizedName, ref int segmentStartIndex)
        {
            var segmentLength = sanitizedName.Length - segmentStartIndex;
            if (segmentLength <= 2)
            {
                if (segmentLength == 0)
                {
                    // Keep leading /, otherwise skip empty segment.
                    return segmentStartIndex == 0;
                }

                if (segmentLength == 1 && sanitizedName[segmentStartIndex] == '.')
                {
                    sanitizedName.Length = segmentStartIndex;
                    return false;
                }

                if (segmentLength == 2 && sanitizedName[segmentStartIndex] == '.'
                    && sanitizedName[segmentStartIndex + 1] == '.')
                {
                    // If there is a possibility of parent path
                    if (segmentStartIndex > 1)
                    {
                        // Find where parent path starts
                        var parentSeparator = -1;
                        for (var i = segmentStartIndex - 2; i >= 0; --i)
                        {
                            if (sanitizedName[i] == '/')
                            {
                                parentSeparator = i;
                                break;
                            }
                        }

                        // Find where parent path starts and set segment start right after / symbol.
                        segmentStartIndex = parentSeparator < 0 ? 0 : parentSeparator + 1;
                    }
                    else
                    {
                        // If there is no way the parent path has parent of it's own then reset full path to empty.
                        segmentStartIndex = 0;
                    }

                    // Reset sanitized name to position right after last known / or at the start.
                    sanitizedName.Length = segmentStartIndex;
                    return false;
                }
            }

            return true;
        }
    }

    public readonly int CompareTo(FileIdentifier other)
    {
        var schemeCompare = string.CompareOrdinal(Scheme ?? string.Empty, other.Scheme ?? string.Empty);
        return schemeCompare != 0
            ? schemeCompare
            : string.CompareOrdinal(FileName ?? string.Empty, other.FileName ?? string.Empty);
    }

    public readonly bool Equals(FileIdentifier other) =>
        string.Equals(Scheme ?? string.Empty, other.Scheme ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(FileName ?? string.Empty, other.FileName ?? string.Empty, StringComparison.Ordinal);

    public override readonly bool Equals(object? obj) => obj is FileIdentifier other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Scheme ?? string.Empty, FileName ?? string.Empty);

    public override readonly string ToString() => ToUri();

    public readonly object Clone() => new FileIdentifier(this);
}
