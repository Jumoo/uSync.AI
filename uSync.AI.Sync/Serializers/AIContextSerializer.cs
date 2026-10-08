using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Contexts;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Sync.Serializers;

/// <summary>
/// Serializes an Umbraco.AI <see cref="AIContext"/> and its resources. Resources are written one
/// element each, in sort order, with the resource type's own settings as JSON.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F03", "AI Context Serializer", "AIContext")]
public class AIContextSerializer : SyncSerializerRoot<AIContext>, ISyncSerializer<AIContext>
{
    private readonly SyncAIService _aiService;

    public AIContextSerializer(ILogger<SyncSerializerRoot<AIContext>> logger, SyncAIService aiService)
        : base(logger)
    {
        _aiService = aiService;
    }

    protected override Task<SyncAttempt<XElement>> SerializeCoreAsync(AIContext item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        node.Add(new XElement("Info", new XElement("Name", item.Name)));

        var resources = new XElement("Resources");
        foreach (var resource in item.Resources.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var resourceNode = new XElement("Resource",
                new XAttribute("Key", resource.Id),
                new XElement("Name", resource.Name),
                new XElement("ResourceTypeId", resource.ResourceTypeId),
                new XElement("Description", resource.Description ?? string.Empty),
                new XElement("InjectionMode", resource.InjectionMode),
                new XElement("SortOrder", resource.SortOrder));

            if (SyncAIJson.ToElement("Settings", resource.Settings) is { } settings) resourceNode.Add(settings);

            resources.Add(resourceNode);
        }

        node.Add(resources);

        return Task.FromResult(SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []));
    }

    protected override async Task<SyncAttempt<AIContext>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();
        var name = node.Element("Info")?.Element("Name").ValueOrDefault(alias) ?? alias;

        var item = await FindItemAsync(key) ?? await FindItemAsync(alias)
            ?? new AIContext { Alias = alias, Name = name }.WithId(key);

        item.Alias = alias;
        item.Name = name;

        var resources = new List<AIContextResource>();
        foreach (var resourceNode in node.Element("Resources")?.Elements("Resource") ?? [])
        {
            var description = resourceNode.Element("Description").ValueOrDefault(string.Empty);

            var resource = new AIContextResource
            {
                Name = resourceNode.Element("Name").ValueOrDefault(string.Empty),
                ResourceTypeId = resourceNode.Element("ResourceTypeId").ValueOrDefault(string.Empty),
                Description = string.IsNullOrEmpty(description) ? null : description,
                InjectionMode = resourceNode.Element("InjectionMode").ValueOrDefault(AIContextResourceInjectionMode.Always),
                SortOrder = resourceNode.Element("SortOrder").ValueOrDefault(resources.Count),
                Settings = SyncAIJson.ToJsonElement(resourceNode.Element("Settings")),
            };

            var resourceKey = resourceNode.Attribute("Key").ValueOrDefault(Guid.Empty);
            if (resourceKey != Guid.Empty) resource.WithId(resourceKey);

            resources.Add(resource);
        }

        item.Resources = resources;

        return SyncAttempt<AIContext>.Succeed(item.Name, item, ChangeType.Import, []);
    }

    public override Task<AIContext?> FindItemAsync(Guid key) => _aiService.GetContextAsync(key);

    public override Task<AIContext?> FindItemAsync(string alias) => _aiService.GetContextAsync(alias);

    public override Task SaveItemAsync(AIContext item) => _aiService.SaveContextAsync(item);

    public override Task DeleteItemAsync(AIContext item) => _aiService.DeleteContextAsync(item);

    public override string ItemAlias(AIContext item) => item.Alias;

    public override Guid ItemKey(AIContext item) => item.Id;
}
