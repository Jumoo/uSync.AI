using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Prompt.Core.Prompts;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Prompt.Serializers;

/// <summary>
/// Serializes an Umbraco.AI.Prompt <see cref="AIPrompt"/>. The profile, contexts and guardrails
/// it uses are recorded by Key and alias and resolved key first. One that can't be found on the
/// target is left off with a warning rather than failing the import: the prompt still runs
/// (against the default profile, without that context or guardrail) and the next import puts
/// the reference back once the missing item exists.
/// </summary>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F06", "AI Prompt Serializer", "AIPrompt")]
public class AIPromptSerializer : SyncSerializerRoot<AIPrompt>, ISyncSerializer<AIPrompt>
{
    private readonly SyncAIPromptService _promptService;
    private readonly SyncAIService _aiService;

    public AIPromptSerializer(
        ILogger<SyncSerializerRoot<AIPrompt>> logger,
        SyncAIPromptService promptService,
        SyncAIService aiService)
        : base(logger)
    {
        _promptService = promptService;
        _aiService = aiService;
    }

    protected override async Task<SyncAttempt<XElement>> SerializeCoreAsync(AIPrompt item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        var info = new XElement("Info",
            new XElement("Name", item.Name),
            new XElement("Description", item.Description ?? string.Empty),
            new XElement("IsActive", item.IsActive),
            new XElement("DisplayMode", item.DisplayMode),
            new XElement("IncludeEntityContext", item.IncludeEntityContext),
            new XElement("OptionCount", item.OptionCount));

        info.Add(item.ProfileId is Guid profileId
            ? new XElement("Profile", new XAttribute("Key", profileId), await _aiService.GetProfileAliasAsync(profileId) ?? string.Empty)
            : new XElement("Profile"));

        node.Add(info);
        node.Add(new XElement("Instructions", new XCData(item.Instructions)));

        node.Add(await ReferencesAsync("Contexts", "Context", item.ContextIds, _aiService.GetContextAliasAsync));
        node.Add(await ReferencesAsync("Guardrails", "Guardrail", item.GuardrailIds, _aiService.GetGuardrailAliasAsync));
        node.Add(new XElement("Tags", item.Tags.OrderBy(x => x, StringComparer.Ordinal).Select(x => new XElement("Tag", x))));

        if (SyncAIJson.ToElement("Scope", item.Scope) is { } scope) node.Add(scope);

        return SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []);
    }

    protected override async Task<SyncAttempt<AIPrompt>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();

        var info = node.Element("Info");
        var name = info?.Element("Name").ValueOrDefault(alias) ?? alias;
        var instructions = node.Element("Instructions").ValueOrDefault(string.Empty);
        var description = info?.Element("Description").ValueOrDefault(string.Empty);

        // unlike the core Umbraco.AI entities, AIPrompt has no DateCreated default and the
        // prompt service doesn't set one (only the management API mapper does), so set it here.
        var item = await FindItemAsync(key) ?? await FindItemAsync(alias)
            ?? new AIPrompt { Alias = alias, Name = name, Instructions = instructions, DateCreated = DateTime.UtcNow }.WithId(key);

        var changes = new List<uSyncChange>();

        item.Alias = alias;
        item.Name = name;
        item.Description = string.IsNullOrEmpty(description) ? null : description;
        item.Instructions = instructions;
        item.IsActive = info?.Element("IsActive").ValueOrDefault(true) ?? true;
        item.DisplayMode = info?.Element("DisplayMode").ValueOrDefault(AIPromptDisplayMode.PropertyAction) ?? AIPromptDisplayMode.PropertyAction;
        item.IncludeEntityContext = info?.Element("IncludeEntityContext").ValueOrDefault(true) ?? true;
        item.OptionCount = info?.Element("OptionCount").ValueOrDefault(1) ?? 1;

        item.ProfileId = await ResolveAsync(info?.Element("Profile"), "Profile", _aiService.ResolveProfileAsync, changes);
        item.ContextIds = await ResolveAllAsync(node.Element("Contexts"), "Context", _aiService.ResolveContextAsync, changes);
        item.GuardrailIds = await ResolveAllAsync(node.Element("Guardrails"), "Guardrail", _aiService.ResolveGuardrailAsync, changes);
        item.Tags = node.Element("Tags")?.Elements("Tag").Select(x => x.Value).ToList() ?? [];

        var scope = node.Element("Scope")?.Value;
        item.Scope = string.IsNullOrWhiteSpace(scope) ? null : JsonSerializer.Deserialize<AIPromptScope>(scope);

        return SyncAttempt<AIPrompt>.Succeed(item.Name, item, ChangeType.Import, changes);
    }

    private static async Task<XElement> ReferencesAsync(string listName, string itemName, IEnumerable<Guid> ids, Func<Guid, Task<string?>> getAlias)
    {
        var entries = new List<(Guid Key, string Alias)>();
        foreach (var id in ids) entries.Add((id, await getAlias(id) ?? string.Empty));

        // order is not significant to Umbraco.AI, so sort for a stable file.
        return new XElement(listName, entries
            .OrderBy(x => x.Alias, StringComparer.Ordinal).ThenBy(x => x.Key)
            .Select(x => new XElement(itemName, new XAttribute("Key", x.Key), x.Alias)));
    }

    private static async Task<Guid?> ResolveAsync(XElement? node, string type, Func<Guid, string?, Task<Guid?>> resolve, List<uSyncChange> changes)
    {
        var key = node?.Attribute("Key").ValueOrDefault(Guid.Empty) ?? Guid.Empty;
        var alias = node?.Value;

        if (key == Guid.Empty && string.IsNullOrWhiteSpace(alias)) return null;

        var resolved = await resolve(key, alias);
        if (resolved is null)
            changes.AddWarning(type, alias ?? key.ToString(), $"{type} '{alias}' ({key}) not found on this server - left off");

        return resolved;
    }

    private static async Task<List<Guid>> ResolveAllAsync(XElement? list, string type, Func<Guid, string?, Task<Guid?>> resolve, List<uSyncChange> changes)
    {
        var ids = new List<Guid>();
        foreach (var node in list?.Elements(type) ?? [])
        {
            if (await ResolveAsync(node, type, resolve, changes) is Guid id) ids.Add(id);
        }

        return ids;
    }

    public override Task<AIPrompt?> FindItemAsync(Guid key) => _promptService.GetPromptAsync(key);

    public override Task<AIPrompt?> FindItemAsync(string alias) => _promptService.GetPromptAsync(alias);

    public override Task SaveItemAsync(AIPrompt item) => _promptService.SavePromptAsync(item);

    public override Task DeleteItemAsync(AIPrompt item) => _promptService.DeletePromptAsync(item);

    public override string ItemAlias(AIPrompt item) => item.Alias;

    public override Guid ItemKey(AIPrompt item) => item.Id;
}
