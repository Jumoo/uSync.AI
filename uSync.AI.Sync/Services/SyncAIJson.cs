using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace uSync.AI.Sync.Services;

/// <summary>
/// JSON helpers for the blobs Umbraco.AI stores as untyped objects (connection settings, resource
/// settings, rule config, profile settings). Output is always key-sorted and indented, so the
/// same data produces the same file text whatever order the provider wrote it in.
/// </summary>
public static class SyncAIJson
{
    private static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

    /// <summary>
    /// Any stored value as a <see cref="JsonNode"/>: a <see cref="JsonElement"/> read back from
    /// storage, a typed settings object, or a dictionary.
    /// </summary>
    public static JsonNode? ToNode(object? value, JsonSerializerOptions? options = null)
        => value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            string text when LooksLikeJson(text) => JsonNode.Parse(text),
            _ => JsonSerializer.SerializeToNode(value, value.GetType(), options),
        };

    /// <summary>Key-sorted, indented JSON text, or an empty string for null.</summary>
    public static string ToStableJson(object? value, JsonSerializerOptions? options = null)
    {
        var node = ToNode(value, options);
        return node is null ? string.Empty : Sort(node)!.ToJsonString(_indented);
    }

    /// <summary>A JSON element for <paramref name="name"/>, or nothing when the value is empty.</summary>
    public static XElement? ToElement(string name, object? value, JsonSerializerOptions? options = null)
    {
        var json = ToStableJson(value, options);
        return json.Length == 0 ? null : new XElement(name, new XCData(json));
    }

    public static JsonElement? ToJsonElement(XElement? node)
    {
        var text = node?.Value;
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    public static JsonNode? Sort(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var sorted = new JsonObject();
                foreach (var property in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
                    sorted[property.Key] = Sort(property.Value?.DeepClone());
                return sorted;

            case JsonArray array:
                return new JsonArray(array.Select(x => Sort(x?.DeepClone())).ToArray());

            default:
                return node?.DeepClone();
        }
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.AsSpan().Trim();
        return trimmed.Length > 1 && (trimmed[0] == '{' || trimmed[0] == '[');
    }
}
