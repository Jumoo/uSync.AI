using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.AI.Core.Connections;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Sync.Serializers;

/// <summary>
/// Serializes an Umbraco.AI <see cref="AIConnection"/>. Settings are filtered before they are
/// written (see <see cref="AIConnectionSettingsFilter"/>): by default no API key or other
/// secret reaches the file. The names of the settings that were left out are recorded in
/// <c>&lt;Ignored&gt;</c>, and on import those keep the value the target server already has.
/// Where the target has no value yet the setting gets a placeholder and the import warns:
/// the connection exists, so profiles can be imported, but it won't work until it is filled in.
/// <see cref="AIConnection.Version"/>, dates and user ids are omitted - emitting them would
/// rewrite the file on every save even when nothing meaningful changed.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F01", "AI Connection Serializer", "AIConnection")]
public class AIConnectionSerializer : SyncSerializerRoot<AIConnection>, ISyncSerializer<AIConnection>
{
    private readonly SyncAIService _aiService;
    private readonly IOptionsMonitor<uSyncAIOptions> _options;

    public AIConnectionSerializer(
        ILogger<SyncSerializerRoot<AIConnection>> logger,
        SyncAIService aiService,
        IOptionsMonitor<uSyncAIOptions> options)
        : base(logger)
    {
        _aiService = aiService;
        _options = options;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(AIConnection item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        node.Add(new XElement("Info",
            new XElement("Name", item.Name),
            new XElement("ProviderId", item.ProviderId),
            new XElement("IsActive", item.IsActive)));

        var (settings, ignored) = AIConnectionSettingsFilter.Filter(
            item.Settings, _aiService.GetConnectionSettingsType(item.ProviderId), _options.CurrentValue.Connections);

        if (SyncAIJson.ToElement("Settings", settings) is { } settingsNode) node.Add(settingsNode);

        if (ignored.Count > 0)
            node.Add(new XElement("Ignored", ignored.Select(x => new XElement("Setting", x))));

        return Task.FromResult(SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []));
    }

    protected override async Task<SyncAttempt<AIConnection>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();

        var info = node.Element("Info");
        var name = info?.Element("Name").ValueOrDefault(alias) ?? alias;
        var providerId = info?.Element("ProviderId").ValueOrDefault(string.Empty) ?? string.Empty;

        var item = await FindItemAsync(key) ?? await FindItemAsync(alias);

        if (item is not null && !string.Equals(item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
        {
            // ProviderId is init-only, and a connection's settings only make sense for its provider.
            return SyncAttempt<AIConnection>.Fail(name, ChangeType.Fail,
                $"Connection '{alias}' already exists for provider '{item.ProviderId}', the file is for '{providerId}'. Delete the existing connection to import this one.");
        }

        item ??= new AIConnection { Alias = alias, Name = name, ProviderId = providerId }.WithId(key);

        item.Alias = alias;
        item.Name = name;
        item.IsActive = info?.Element("IsActive").ValueOrDefault(true) ?? true;

        var ignored = node.Element("Ignored")?.Elements("Setting").Select(x => x.Value).ToList() ?? [];
        item.Settings = AIConnectionSettingsFilter.Merge(
            SyncAIJson.ToJsonElement(node.Element("Settings")), ignored, item.Settings, out var missing);

        var changes = new List<uSyncChange>();
        foreach (var setting in missing)
        {
            changes.AddWarning("Settings", setting,
                $"'{setting}' is not synced and has no value on this server. Enter it on the '{alias}' connection before using it.");
        }

        return SyncAttempt<AIConnection>.Succeed(item.Name, item, ChangeType.Import, changes);
    }

    public override Task<AIConnection?> FindItemAsync(Guid key) => _aiService.GetConnectionAsync(key);

    public override Task<AIConnection?> FindItemAsync(string alias) => _aiService.GetConnectionAsync(alias);

    public override Task SaveItemAsync(AIConnection item) => _aiService.SaveConnectionAsync(item);

    public override Task DeleteItemAsync(AIConnection item) => _aiService.DeleteConnectionAsync(item);

    public override string ItemAlias(AIConnection item) => item.Alias;

    public override Guid ItemKey(AIConnection item) => item.Id;
}
