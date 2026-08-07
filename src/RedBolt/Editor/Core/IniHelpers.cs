//
// Copyright (c) 2017-2020 the rbfx project.
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
