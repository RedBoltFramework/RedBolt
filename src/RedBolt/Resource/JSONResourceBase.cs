using System.Text;

namespace RedBolt;

// C++: No direct C++ equivalent. C++ SimpleResource handles JSON/XML/Binary via Archive serialization.
// C++: BeginLoad in SimpleResource uses JSONFile/XMLFile intermediaries; C# reads raw bytes and checks for XML.
// C++: SimpleResource does NOT have DeserializeJson/SerializeJson/LoadLegacyXML — serialization uses SerializeInBlock(Archive).
// This class is a C#-specific abstraction.
public abstract class JSONResourceBase : Resource
{
    protected JSONResourceBase(string name)
        : base(name)
    {
    }

    public virtual bool DeserializeJson(string jsonString)
    {
        return false;
    }

    public virtual string SerializeJson()
    {
        return string.Empty;
    }

    protected virtual bool LoadLegacyXML(XMLElement source)
    {
        return false;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(content))
            return DeserializeJson(string.Empty);

        var trimmed = content.AsSpan().TrimStart();
        if (!trimmed.IsEmpty && trimmed[0] == '<')
        {
            var xmlFile = new XMLFile(Name);
            return xmlFile.FromString(content) && LoadLegacyXML(xmlFile.GetRoot());
        }

        return DeserializeJson(content);
    }

    public override bool EndLoad() => true;

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var jsonString = SerializeJson();
        var bytes = Encoding.UTF8.GetBytes(jsonString);
        destination.Write(bytes);
        return true;
    }
}
