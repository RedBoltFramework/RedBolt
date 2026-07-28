namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;
using RedBolt.Resource;

internal sealed class TestJSONOutputArchive : JSONOutputArchive
{
    public TestJSONOutputArchive()
        : this(new JSONFile())
    {
    }

    private TestJSONOutputArchive(JSONFile jsonFile)
        : base(jsonFile)
    {
        _jsonFile = jsonFile;
    }

    public string ToJsonString() => _jsonFile.ToString();

    public override void Dispose()
    {
        base.Dispose();
        _jsonFile.Dispose();
    }

    private readonly JSONFile _jsonFile;
}

internal sealed class TestJSONInputArchive : JSONInputArchive
{
    public TestJSONInputArchive(string json)
        : this(CreateFile(json))
    {
    }

    private TestJSONInputArchive(JSONFile jsonFile)
        : base(jsonFile)
    {
        _jsonFile = jsonFile;
    }

    public override void Dispose()
    {
        base.Dispose();
        _jsonFile.Dispose();
    }

    private static JSONFile CreateFile(string json)
    {
        var jsonFile = new JSONFile();
        if (jsonFile.FromString(json))
            return jsonFile;

        jsonFile.Dispose();
        throw new ArchiveException("Failed to parse JSON string");
    }

    private readonly JSONFile _jsonFile;
}
