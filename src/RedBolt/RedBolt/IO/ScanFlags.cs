using FileTime = System.UInt32;

namespace RedBolt.IO;

[Flags]
public enum ScanFlags : byte
{
    ScanFiles = 0x1,
    ScanDirs = 0x2,
    ScanHidden = 0x4,
    ScanAppend = 0x8,
    ScanRecursive = 0x10
}
