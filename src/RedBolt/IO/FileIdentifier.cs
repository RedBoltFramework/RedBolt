namespace RedBolt;

public struct FileIdentifier : IEquatable<FileIdentifier>, IComparable<FileIdentifier>
{
    public static readonly FileIdentifier Empty = new();

    public string Scheme;
    public string FileName;

    public FileIdentifier(string scheme, string fileName)
    {
        Scheme = scheme ?? string.Empty;
        FileName = fileName ?? string.Empty;
    }

    public FileIdentifier(FileIdentifier other)
    {
        Scheme = other.Scheme;
        FileName = other.FileName;
    }

    public FileIdentifier(string uri)
        : this(FromUri(uri))
    {
    }

    public static FileIdentifier FromUri(string uri)
    {
        uri ??= string.Empty;

        if (uri.StartsWith("/", StringComparison.Ordinal)
            || (uri.Length >= 3 && uri[1] == ':' && (uri[2] == '/' || uri[2] == '\\')))
        {
            return new FileIdentifier("file", SanitizeFileName(uri));
        }

        var schemePos = uri.IndexOf(':', StringComparison.Ordinal);
        if (schemePos < 0)
            return new FileIdentifier(string.Empty, SanitizeFileName(uri));

        var scheme = uri[..schemePos];
        var path = uri[(schemePos + 1)..];

        static bool IsSlash(char c) => c == '/';

        var numSlashes = 0;
        while (numSlashes < path.Length && IsSlash(path[numSlashes]))
            ++numSlashes;

        if (string.Equals(scheme, "file", StringComparison.Ordinal))
        {
            if (numSlashes == 0 || numSlashes > 3)
                return Empty;

            var localPath = path[(numSlashes - 1)..];
            if (localPath.Length >= 3 && localPath[2] == ':')
                return new FileIdentifier(scheme, localPath[1..]);

            return new FileIdentifier(scheme, localPath);
        }

        return new FileIdentifier(scheme, path[Math.Min(numSlashes, 2)..]);
    }

    public string ToUri()
    {
        if (string.IsNullOrEmpty(Scheme))
            return FileName ?? string.Empty;

        if (string.Equals(Scheme, "file", StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(FileName))
                return string.Empty;
            if (FileName[0] == '/')
                return "file://" + FileName;
            return "file:///" + FileName;
        }

        return $"{Scheme}://{FileName}";
    }

    public void AppendPath(string path)
    {
        path ??= string.Empty;
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

    public bool IsEmpty() => string.IsNullOrEmpty(Scheme) && string.IsNullOrEmpty(FileName);

    public static string SanitizeFileName(string fileName)
    {
        fileName ??= string.Empty;
        if (fileName.Length == 0)
            return string.Empty;

        var normalized = fileName.Replace('\\', '/');
        var isAbsolute = normalized.StartsWith("/", StringComparison.Ordinal)
            || (normalized.Length >= 3 && normalized[1] == ':' && normalized[2] == '/');

        var prefix = string.Empty;
        var pathPart = normalized;
        if (normalized.Length >= 3 && normalized[1] == ':' && normalized[2] == '/')
        {
            prefix = normalized[..2];
            pathPart = normalized[2..];
        }
        else if (isAbsolute)
        {
            pathPart = normalized[1..];
        }

        var parts = pathPart.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..")
                    stack.RemoveAt(stack.Count - 1);
                else if (!isAbsolute && prefix.Length == 0)
                    stack.Add(part);
            }
            else
            {
                stack.Add(part);
            }
        }

        var result = string.Join('/', stack);
        if (prefix.Length != 0)
            return result.Length != 0 ? $"{prefix}/{result}" : $"{prefix}/";
        if (isAbsolute)
            return "/" + result;
        return result;
    }

    public int CompareTo(FileIdentifier other)
    {
        var schemeCompare = string.CompareOrdinal(Scheme, other.Scheme);
        return schemeCompare != 0 ? schemeCompare : string.CompareOrdinal(FileName, other.FileName);
    }

    public bool Equals(FileIdentifier other) =>
        string.Equals(Scheme, other.Scheme, StringComparison.Ordinal)
        && string.Equals(FileName, other.FileName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is FileIdentifier other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Scheme, FileName);

    public override string ToString() => ToUri();

    public static bool operator <(FileIdentifier lhs, FileIdentifier rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(FileIdentifier lhs, FileIdentifier rhs) => lhs.CompareTo(rhs) > 0;
    public static bool operator ==(FileIdentifier lhs, FileIdentifier rhs) => lhs.Equals(rhs);
    public static bool operator !=(FileIdentifier lhs, FileIdentifier rhs) => !lhs.Equals(rhs);

    public static explicit operator bool(FileIdentifier value) => !value.IsEmpty();

    public static FileIdentifier operator +(FileIdentifier lhs, string rhs)
    {
        lhs.AppendPath(rhs);
        return lhs;
    }
}
