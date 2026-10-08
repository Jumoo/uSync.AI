using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Settings;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Sync.Serializers;

/// <summary>
/// Serializes the Umbraco.AI <see cref="AISettings"/> singleton. Each default profile is recorded
/// by Key and alias; one that can't be found on the target is left unset with a warning, rather
/// than failing the import, because the settings are still usable without it.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F05", "AI Settings Serializer", "AISettings")]
public class AISettingsSerializer : SyncSerializerRoot<AISettings>, ISyncSerializer<AISettings>
{
    public const string SettingsAlias = "ai-settings";

    private readonly SyncAIService _aiService;

    public AISettingsSerializer(ILogger<SyncSerializerRoot<AISettings>> logger, SyncAIService aiService)
        : base(logger)
    {
        _aiService = aiService;
    }

    protected override async Task<SyncAttempt<XElement>> SerializeCoreAsync(AISettings item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, SettingsAlias);

        node.Add(new XElement("Profiles",
            await ProfileNodeAsync("DefaultChat", item.DefaultChatProfileId),
            await ProfileNodeAsync("DefaultEmbedding", item.DefaultEmbeddingProfileId),
            await ProfileNodeAsync("DefaultSpeechToText", item.DefaultSpeechToTextProfileId),
            await ProfileNodeAsync("DefaultImageGeneration", item.DefaultImageGenerationProfileId),
            await ProfileNodeAsync("ClassifierChat", item.ClassifierChatProfileId)));

        node.Add(new XElement("DisclosureNoticeMode", item.DisclosureNoticeMode));

        return SyncAttempt<XElement>.Succeed(SettingsAlias, node, ChangeType.Export, []);
    }

    protected override async Task<SyncAttempt<AISettings>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var item = await _aiService.GetSettingsAsync();
        var changes = new List<uSyncChange>();
        var profiles = node.Element("Profiles");

        item.DefaultChatProfileId = await ResolveProfileAsync(profiles, "DefaultChat", changes);
        item.DefaultEmbeddingProfileId = await ResolveProfileAsync(profiles, "DefaultEmbedding", changes);
        item.DefaultSpeechToTextProfileId = await ResolveProfileAsync(profiles, "DefaultSpeechToText", changes);
        item.DefaultImageGenerationProfileId = await ResolveProfileAsync(profiles, "DefaultImageGeneration", changes);
        item.ClassifierChatProfileId = await ResolveProfileAsync(profiles, "ClassifierChat", changes);

        item.DisclosureNoticeMode = node.Element("DisclosureNoticeMode").ValueOrDefault(item.DisclosureNoticeMode);

        return SyncAttempt<AISettings>.Succeed(SettingsAlias, item, ChangeType.Import, changes);
    }

    private async Task<XElement> ProfileNodeAsync(string name, Guid? profileId)
        => profileId is Guid id
            ? new XElement(name, new XAttribute("Key", id), await _aiService.GetProfileAliasAsync(id) ?? string.Empty)
            : new XElement(name);

    private async Task<Guid?> ResolveProfileAsync(XElement? profiles, string name, List<uSyncChange> changes)
    {
        var node = profiles?.Element(name);
        var key = node?.Attribute("Key").ValueOrDefault(Guid.Empty) ?? Guid.Empty;
        var alias = node?.Value;

        if (key == Guid.Empty && string.IsNullOrWhiteSpace(alias)) return null;

        var resolved = await _aiService.ResolveProfileAsync(key, alias);
        if (resolved is null)
            changes.AddWarning("Profiles", name, $"Profile '{alias}' ({key}) not found on this server - {name} left unset");

        return resolved;
    }

    // the settings can't be deleted, so a delete marker has nothing to act on.
    protected override Task<SyncAttempt<AISettings>> ProcessDeleteAsync(Guid key, string alias, SerializerFlags flags)
        => Task.FromResult(SyncAttempt<AISettings>.Succeed(alias, ChangeType.NoChange));

    public override async Task<AISettings?> FindItemAsync(Guid key)
        => key == AISettings.SettingsId ? await _aiService.GetSettingsAsync() : null;

    public override async Task<AISettings?> FindItemAsync(string alias)
        => alias == SettingsAlias ? await _aiService.GetSettingsAsync() : null;

    public override Task SaveItemAsync(AISettings item) => _aiService.SaveSettingsAsync(item);

    public override Task DeleteItemAsync(AISettings item) => Task.CompletedTask;

    public override string ItemAlias(AISettings item) => SettingsAlias;

    public override Guid ItemKey(AISettings item) => item.Id;
}
