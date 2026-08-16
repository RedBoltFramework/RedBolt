// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using FileTime = System.UInt32;

[Flags]
public enum ScanFlags : byte
{
    ScanFiles = 0x1,
    ScanDirs = 0x2,
    ScanHidden = 0x4,
    ScanAppend = 0x8,
    ScanRecursive = 0x10
}
