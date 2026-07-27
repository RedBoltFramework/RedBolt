namespace RedBolt.Tests;

public sealed class ScanFlagsTests
{
    [Fact]
    public void Scan_flags_match_cpp_byte_flag_values()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(ScanFlags)));
        Assert.True(typeof(ScanFlags).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(0x1, (byte)ScanFlags.ScanFiles);
        Assert.Equal(0x2, (byte)ScanFlags.ScanDirs);
        Assert.Equal(0x4, (byte)ScanFlags.ScanHidden);
        Assert.Equal(0x8, (byte)ScanFlags.ScanAppend);
        Assert.Equal(0x10, (byte)ScanFlags.ScanRecursive);
    }
}
