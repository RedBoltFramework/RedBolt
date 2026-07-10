using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PListFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PListFileTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RedBolt.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Data"));
        return dir;
    }

    private PListFile LoadSample()
    {
        var tempDir = CreateTempDir();
        var path = Path.Combine(tempDir, "Data", "sample.plist");
        File.WriteAllText(path,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict>" +
            "<key>Health</key><integer>42</integer>" +
            "<key>Visible</key><true/>" +
            "<key>Speed</key><real>3.5</real>" +
            "<key>Rect</key><string>{{1,2},{3,4}}</string>" +
            "<key>Vector2</key><string>{5,6}</string>" +
            "<key>Vector3</key><string>{7,8,9}</string>" +
            "<key>Nested</key><dict><key>Name</key><string>RedBolt</string></dict>" +
            "<key>Items</key><array><string>A</string><integer>2</integer></array>" +
            "</dict></plist>");
        Resources.AddResourceDir(tempDir);
        return Resources.GetResource<PListFile>("Data/sample.plist");
    }

    [Fact]
    public void Scalar_loading_works()
    {
        var plist = LoadSample();
        Assert.Equal(42, plist.GetRoot()["Health"].GetInt());
        Assert.True(plist.GetRoot()["Visible"].GetBool());
        Assert.True(MathDefs.ApproximatelyEquivalent(plist.GetRoot()["Speed"].GetFloat(), 3.5f));
    }

    [Fact]
    public void Geometry_string_parsing_works()
    {
        var plist = LoadSample();
        Assert.Equal(new IntRect(1, 2, 4, 6), plist.GetRoot()["Rect"].GetIntRect());
        Assert.Equal(new Vector2i(5, 6), plist.GetRoot()["Vector2"].GetIntVector2());
        Assert.Equal(new Vector3i(7, 8, 9), plist.GetRoot()["Vector3"].GetIntVector3());
    }

    [Fact]
    public void Dict_and_array_loading_works()
    {
        var plist = LoadSample();
        Assert.Equal("RedBolt", plist.GetRoot()["Nested"].GetValueMap()["Name"].GetString());
        Assert.Equal(2, plist.GetRoot()["Items"].GetValueVector().Count);
        Assert.Equal(2, plist.GetRoot()["Items"].GetValueVector()[1].GetInt());
    }

    [Fact]
    public void Copy_and_equality_work()
    {
        var plist = LoadSample();
        var clone = new PListValue(plist.GetRoot()["Items"]);
        Assert.True(clone.Equals(plist.GetRoot()["Items"]));
    }
}
