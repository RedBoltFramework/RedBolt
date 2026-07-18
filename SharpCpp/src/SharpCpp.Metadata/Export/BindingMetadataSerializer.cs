using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpCpp.Metadata.Export;

public static class BindingMetadataSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(BindingMetadataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        return json + Environment.NewLine;
    }

    public static BindingMetadataDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<BindingMetadataDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Metadata JSON deserialized to null.");
    }
}
