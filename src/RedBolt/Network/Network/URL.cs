//
// Copyright (c) 2017-2022 the rbfx project.
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

namespace RedBolt.Network;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public struct URL
{
    private static readonly Regex UrlRegex = new(
        @"^(([^:/?#]+)://)?((([^:/?#@]+)(:([^:/?#@]+))?@)?([^/?#:]*)(:([0-9]+))?)?(/([^?#]*))?(\?([^#]*))?(#(.*))?",
        RegexOptions.CultureInvariant);

    public URL()
    {
        Scheme = string.Empty;
        User = string.Empty;
        Password = string.Empty;
        Host = string.Empty;
        Port = 0;
        Path = string.Empty;
        Query = string.Empty;
        Hash = string.Empty;
    }

    public URL(ushort port)
        : this()
    {
        Port = port;
    }

    public URL(string url)
        : this()
    {
        ParseURL(url);
    }

    /// <summary>Returns false if URL fails to parse.</summary>
    public readonly bool IsValid =>
        !string.IsNullOrEmpty(Scheme)
        || !string.IsNullOrEmpty(User)
        || !string.IsNullOrEmpty(Password)
        || !string.IsNullOrEmpty(Host)
        || Port != 0
        || !string.IsNullOrEmpty(Path)
        || !string.IsNullOrEmpty(Query)
        || !string.IsNullOrEmpty(Hash);

    public static implicit operator URL(string url) => new(url);

    public static implicit operator bool(URL url) => url.IsValid;

    /// <summary>Format an URL after modifications were applied. No validation is done.</summary>
    public override readonly string ToString()
    {
        var url = new StringBuilder();
        if (!string.IsNullOrEmpty(Scheme))
        {
            url.Append(Scheme);
            url.Append("://");
        }
        if (!string.IsNullOrEmpty(User))
        {
            url.Append(Encode(User));
            if (!string.IsNullOrEmpty(Password))
            {
                url.Append(':');
                url.Append(Encode(Password));
            }
            url.Append('@');
        }
        url.Append(Host);
        if (Port != 0 && !IsDefaultPort(Port, Scheme))
        {
            url.Append(':');
            url.Append(Port.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrEmpty(Path))
        {
            if (Path[0] != '/')
                url.Append('/');
            url.Append(Path);
        }
        if (!string.IsNullOrEmpty(Query))
        {
            url.Append('?');
            url.Append(Query); // Query parts assumed to be encoded by the user.
        }
        if (!string.IsNullOrEmpty(Hash))
        {
            url.Append('#');
            url.Append(Hash); // Assumed to be encoded by the user.
        }
        return url.ToString();
    }

    /// <summary>Encode a string for inclusion in URLs.</summary>
    public static string Encode(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            var valueChar = (char)valueByte;
            if (valueByte is >= (byte)'a' and <= (byte)'z'
                or >= (byte)'A' and <= (byte)'Z'
                or >= (byte)'0' and <= (byte)'9'
                || valueChar is '-' or '_' or '.' or '~')
            {
                result.Append(valueChar);
            }
            else
            {
                result.Append('%');
                result.Append(valueByte.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return result.ToString();
    }

    /// <summary>Decode a string obtained from an URL.</summary>
    public static string Decode(string value)
    {
        var input = Encoding.UTF8.GetBytes(value);
        var result = new List<byte>(input.Length);
        for (var index = 0; index < input.Length;)
        {
            var valueByte = input[index++];
            if (valueByte == '%')
            {
                var hexStart = index;
                var parsed = 0;
                while (index < input.Length && TryGetHexDigit(input[index], out var digit))
                {
                    parsed = unchecked(parsed * 16 + digit);
                    ++index;
                }
                if (index == hexStart)
                    parsed = 0;
                valueByte = unchecked((byte)parsed);
            }
            result.Add(valueByte);
        }
        return Encoding.UTF8.GetString(result.ToArray());
    }

    public string Scheme;
    public string User;
    public string Password;
    public string Host;
    public ushort Port;
    public string Path;
    public string Query;
    public string Hash;

    private static ushort GetDefaultPort(string? scheme) => scheme switch
    {
        "http" => 80,
        "https" => 443,
        _ => 0
    };

    private static bool IsDefaultPort(ushort port, string? scheme) =>
        scheme == "http" && port == 80 || scheme == "https" && port == 443;

    private static bool TryGetHexDigit(byte value, out int digit)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
            digit = value - '0';
        else if (value is >= (byte)'a' and <= (byte)'f')
            digit = value - 'a' + 10;
        else if (value is >= (byte)'A' and <= (byte)'F')
            digit = value - 'A' + 10;
        else
        {
            digit = 0;
            return false;
        }
        return true;
    }

    private void ParseURL(string url)
    {
        Scheme = User = Password = Host = Path = Query = Hash = string.Empty;
        Port = 0;

        var match = UrlRegex.Match(url);
        if (!match.Success || match.Length != url.Length || match.Groups.Count != 17)
            return;

        Scheme = match.Groups[2].Value;
        User = match.Groups[5].Value;
        Password = match.Groups[7].Value;
        Host = match.Groups[8].Value;
        if (int.TryParse(match.Groups[10].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            Port = unchecked((ushort)port);
        if (Port == 0)
            Port = GetDefaultPort(Scheme);
        Path = match.Groups[11].Value;
        Query = match.Groups[14].Value;
        Hash = match.Groups[16].Value;
    }
}
