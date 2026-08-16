// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Logging;

public sealed class StringHashRegister
{
    private static readonly StringHashRegister GlobalInstance = new(true);

    private readonly object _lockObject = new();
    private readonly bool _threadSafe;
    private readonly Dictionary<StringHash, string> _map = [];

    public StringHashRegister(bool threadSafe)
    {
        _threadSafe = threadSafe;
    }

    public static StringHashRegister Instance => GlobalInstance;

    public StringHash RegisterString(StringHash hash, string value)
    {
        if (_threadSafe)
        {
            lock (_lockObject)
                return RegisterStringCore(hash, value);
        }

        return RegisterStringCore(hash, value);
    }

    public StringHash RegisterString(string value)
    {
        var hash = new StringHash(value, new StringHash.NoReverse());
        return RegisterString(hash, value);
    }

    public string GetStringCopy(StringHash hash)
    {
        if (_threadSafe)
        {
            lock (_lockObject)
                return GetString(hash);
        }

        return GetString(hash);
    }

    public bool Contains(StringHash hash)
    {
        if (_threadSafe)
        {
            lock (_lockObject)
                return _map.ContainsKey(hash);
        }

        return _map.ContainsKey(hash);
    }

    public string GetString(StringHash hash)
    {
        return _map.TryGetValue(hash, out var value) ? value : string.Empty;
    }

    public IReadOnlyDictionary<StringHash, string> GetInternalMap() => _map;

    private StringHash RegisterStringCore(StringHash hash, string value)
    {
        if (!_map.TryGetValue(hash, out var existingValue))
            _map.Add(hash, value);
        else if (!string.Equals(existingValue, value, StringComparison.Ordinal))
            Log.GetLogger().LogWarning("StringHash collision detected! Both \"{First}\" and \"{Second}\" have hash #{Hash}", value, existingValue, hash.ToString());

        return hash;
    }
}
