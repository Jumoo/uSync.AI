using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.AI.Core.EditableModels;

namespace uSync.AI.Sync.Services;

/// <summary>
/// Decides which connection settings are written to a uSync file, and puts the ones that were
/// left out back when the file is imported.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco.AI encrypts sensitive settings only at rest: the <c>AIConnection</c> its services and
/// notifications hand out carries the decrypted value. So "is the value encrypted" cannot be the
/// test for a secret - what identifies one is the provider marking the field
/// <c>[AIField(IsSensitive = true)]</c>. A sensitive field is written only when its value is a
/// <c>$Configuration:Reference</c>, which names where the secret lives rather than being it.
/// </para>
/// <para>
/// The names of the settings that were left out are recorded in the file. On import those keep
/// whatever value the target server already has, so syncing a connection never blanks an API key
/// that was entered on the target by hand.
/// </para>
/// </remarks>
public static class AIConnectionSettingsFilter
{
    private const string EncryptedPrefix = "ENC:";
    private const string ConfigurationReferencePrefix = "$";

    /// <summary>
    /// Stands in for a setting that was left out of the file and has no value on this server
    /// yet. Providers mark their API key as required, so without something in its place a new
    /// connection could not be saved at all - and every profile that uses it would fail with it.
    /// </summary>
    public const string MissingValuePlaceholder = "(not synced - enter this value on this server)";

    /// <summary>
    /// Splits a connection's settings into what is safe to write and the names of what is not.
    /// </summary>
    /// <param name="settings">The connection's settings, typed or as JSON.</param>
    /// <param name="settingsType">
    /// The provider's settings type, which says which fields are sensitive. When it is unknown
    /// every field is treated as sensitive.
    /// </param>
    /// <param name="options">The filtering options.</param>
    public static (JsonObject? Settings, IReadOnlyList<string> Ignored) Filter(object? settings, Type? settingsType, uSyncAIConnectionOptions options)
    {
        if (SyncAIJson.ToNode(settings) is not JsonObject all) return (null, []);

        var kept = new JsonObject();
        var ignored = new List<string>();

        foreach (var (key, value) in all)
        {
            if (ShouldIgnore(key, value, settingsType, options))
                ignored.Add(key);
            else
                kept[key] = value?.DeepClone();
        }

        ignored.Sort(StringComparer.Ordinal);
        return (kept, ignored);
    }

    /// <summary>
    /// The settings to save on import: everything in the file, plus the target's own value for
    /// each setting the file says was left out.
    /// </summary>
    /// <param name="incoming">The settings from the file.</param>
    /// <param name="ignored">The names the file says were left out.</param>
    /// <param name="existing">The settings the connection has on this server, if it exists.</param>
    /// <param name="missing">
    /// The left-out settings this server has no value for. Each is given
    /// <see cref="MissingValuePlaceholder"/> and has to be entered by hand.
    /// </param>
    public static Dictionary<string, object?>? Merge(JsonElement? incoming, IEnumerable<string> ignored, object? existing, out IReadOnlyList<string> missing)
    {
        var notFound = new List<string>();
        missing = notFound;

        var result = incoming is { ValueKind: JsonValueKind.Object } element
            ? element.Deserialize<Dictionary<string, object?>>() ?? []
            : [];

        var current = SyncAIJson.ToNode(existing) as JsonObject;

        foreach (var name in ignored)
        {
            var value = current?.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

            if (value is null || (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) && text.Length == 0))
            {
                notFound.Add(name);
                result[name] = JsonSerializer.SerializeToElement(MissingValuePlaceholder);
            }
            else
            {
                result[name] = JsonSerializer.SerializeToElement(value);
            }
        }

        return result.Count == 0 && incoming is null ? null : result;
    }

    private static bool ShouldIgnore(string key, JsonNode? value, Type? settingsType, uSyncAIConnectionOptions options)
    {
        // Layer 1: named settings are always left out.
        if (options.IgnoreSettings.Contains(key, StringComparer.OrdinalIgnoreCase)) return true;

        var text = value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue) ? stringValue : null;
        var sensitive = IsSensitive(settingsType, key);

        // Layer 2: everything the provider marks sensitive, configuration references included.
        if (options.IgnoreSensitive && sensitive) return true;

        if (options.IgnoreSecretValues is false) return false;

        // Layer 3: secret values. An empty sensitive field holds nothing to protect, and a
        // "$Config:Key" reference is where the secret lives, not the secret - both are written.
        if (text?.StartsWith(EncryptedPrefix, StringComparison.Ordinal) == true) return true;

        return sensitive
            && value is not null
            && text is not ""
            && text?.StartsWith(ConfigurationReferencePrefix, StringComparison.Ordinal) != true;
    }

    private static bool IsSensitive(Type? settingsType, string key)
    {
        // no settings type means no way to tell, so assume the worst.
        if (settingsType is null) return true;

        return settingsType
            .GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?
            .GetCustomAttribute<AIFieldAttribute>()?.IsSensitive == true;
    }
}
