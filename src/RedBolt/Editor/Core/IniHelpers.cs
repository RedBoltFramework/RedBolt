// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Core;

using System;
using System.Globalization;
using System.Text;

using RedBolt.Bindings.DearImGui;

public static class IniHelpers
{
    public static void WriteStringToIni(ImGuiTextBuffer output, string name, string value)
    {
        output.Append(Encoding.UTF8.GetBytes(name));
        output.Append("="u8);
        output.Append(Encoding.UTF8.GetBytes(value));
        output.Append("\n"u8);
    }

    public static string? ReadStringFromIni(string line, string name)
    {
        if (!line.StartsWith(name, StringComparison.Ordinal))
            return null;

        line = line[name.Length..];

        if (line.Length == 0 || line[0] != '=')
            return null;

        return line[1..];
    }

    public static void WriteIntToIni(ImGuiTextBuffer output, string name, int value)
    {
        WriteStringToIni(output, name, value.ToString(CultureInfo.InvariantCulture));
    }

    public static int? ReadIntFromIni(string line, string name)
    {
        var value = ReadStringFromIni(line, name);
        if (value is not null)
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
        return null;
    }
}
