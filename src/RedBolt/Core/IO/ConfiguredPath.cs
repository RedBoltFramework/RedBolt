// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using System;
using System.IO;

public static class ConfiguredPath
{
    public static string ExpandVariables(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace(
            "{exeDir}", Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.Ordinal);
    }
}
