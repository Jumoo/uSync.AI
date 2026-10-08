using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Models;
using Umbraco.AI.Core.Profiles;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;
using AIConstants = Umbraco.AI.Core.Constants;

namespace uSync.AI.Sync.Serializers;

/// <summary>
/// Serializes an Umbraco.AI <see cref="AIProfile"/>. The connection is recorded by Key and alias
/// and resolved key first on import; a profile whose connection can't be found fails rather than
/// being saved pointing at nothing. Capability-specific settings are written as JSON using
/// Umbraco.AI's own serializer options, so they read back through the same settings types.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F04", "AI Profile Serializer", "AIProfile")]
public class AIProfileSerializer : SyncSerializerRoot<AIProfile>, ISyncSerializer<AIProfile>
{
    private readonly SyncAIService _aiService;

    public AIProfileSerializer(ILogger<SyncSerializerRoot<AIProfile>> logger, SyncAIService aiService)
        : base(logger)
    {
        _aiService = aiService;
    }

    protected override async Task<SyncAttempt<XElement>> SerializeCoreAsync(AIProfile item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        node.Add(new XElement("Info",
            new XElement("Name", item.Name),
            new XElement("Capability", item.Capability),
            new XElement("Connection",
                new XAttribute("Key", item.ConnectionId),
                await _aiService.GetConnectionAliasAsync(item.ConnectionId) ?? string.Empty),
            new XElement("Model",
                new XElement("ProviderId", item.Model.ProviderId),
                new XElement("ModelId", item.Model.ModelId))));

        node.Add(new XElement("Tags", item.Tags.OrderBy(x => x, StringComparer.Ordinal).Select(x => new XElement("Tag", x))));

        if (SyncAIJson.ToElement("Settings", item.Settings, AIConstants.DefaultJsonSerializerOptions) is { } settings)
            node.Add(settings);

        if (SyncAIJson.ToElement("CapabilitySettings", item.CapabilitySettings, AIConstants.DefaultJsonSerializerOptions) is { } capabilitySettings)
            node.Add(capabilitySettings);

        return SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []);
    }

    protected override async Task<SyncAttempt<AIProfile>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();

        var info = node.Element("Info");
        var name = info?.Element("Name").ValueOrDefault(alias) ?? alias;
        var capability = info?.Element("Capability").ValueOrDefault(AICapability.Chat) ?? AICapability.Chat;

        var connectionNode = info?.Element("Connection");
        var connectionAlias = connectionNode?.Value;
        var connectionId = await _aiService.ResolveConnectionAsync(
            connectionNode?.Attribute("Key").ValueOrDefault(Guid.Empty) ?? Guid.Empty, connectionAlias);

        if (connectionId is null)
        {
            return SyncAttempt<AIProfile>.Fail(name, ChangeType.Fail,
                $"Connection '{connectionAlias}' not found on this server. Import the connection before this profile.");
        }

        var item = await FindItemAsync(key) ?? await FindItemAsync(alias);

        if (item is not null && item.Capability != capability)
        {
            // Capability is init-only: the settings type and the models on offer both depend on it.
            return SyncAttempt<AIProfile>.Fail(name, ChangeType.Fail,
                $"Profile '{alias}' already exists as a {item.Capability} profile, the file is for {capability}. Delete the existing profile to import this one.");
        }

        item ??= new AIProfile { Alias = alias, Name = name, Capability = capability, ConnectionId = connectionId.Value }.WithId(key);

        item.Alias = alias;
        item.Name = name;
        item.ConnectionId = connectionId.Value;

        var model = info?.Element("Model");
        item.Model = new AIModelRef(
            model?.Element("ProviderId").ValueOrDefault(string.Empty) ?? string.Empty,
            model?.Element("ModelId").ValueOrDefault(string.Empty) ?? string.Empty);

        item.Tags = node.Element("Tags")?.Elements("Tag").Select(x => x.Value).ToList() ?? [];
        item.Settings = DeserializeSettings(capability, node.Element("Settings")?.Value);
        item.CapabilitySettings = SyncAIJson.ToJsonElement(node.Element("CapabilitySettings"));

        return SyncAttempt<AIProfile>.Succeed(item.Name, item, ChangeType.Import, []);
    }

    /// <summary>
    /// The same capability switch as Umbraco.AI's <c>AIProfileSettingsSerializer.Deserialize</c>,
    /// which is internal.
    /// </summary>
    internal static IAIProfileSettings? DeserializeSettings(AICapability capability, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        return capability switch
        {
            AICapability.Chat => JsonSerializer.Deserialize<AIChatProfileSettings>(json, AIConstants.DefaultJsonSerializerOptions),
            AICapability.Embedding => JsonSerializer.Deserialize<AIEmbeddingProfileSettings>(json, AIConstants.DefaultJsonSerializerOptions),
            AICapability.SpeechToText => JsonSerializer.Deserialize<AISpeechToTextProfileSettings>(json, AIConstants.DefaultJsonSerializerOptions),
            AICapability.ImageGeneration => JsonSerializer.Deserialize<AIImageGenerationProfileSettings>(json, AIConstants.DefaultJsonSerializerOptions),
            _ => null,
        };
    }

    public override Task<AIProfile?> FindItemAsync(Guid key) => _aiService.GetProfileAsync(key);

    public override Task<AIProfile?> FindItemAsync(string alias) => _aiService.GetProfileAsync(alias);

    public override Task SaveItemAsync(AIProfile item) => _aiService.SaveProfileAsync(item);

    public override Task DeleteItemAsync(AIProfile item) => _aiService.DeleteProfileAsync(item);

    public override string ItemAlias(AIProfile item) => item.Alias;

    public override Guid ItemKey(AIProfile item) => item.Id;
}
