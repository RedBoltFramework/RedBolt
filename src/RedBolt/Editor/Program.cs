// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor;

using System;

internal static class EditorProgram
{
    private static int Main(string[] args)
    {
        try
        {
            using var application = new EditorApplication();
            return application.Run(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
