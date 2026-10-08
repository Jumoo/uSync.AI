using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Agent.Core.Agents;
using uSync.AI.Agent.Services;
using uSync.AI.Sync.Services;
using uSync.Core;
using uSync.Core.Extensions;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace uSync.AI.Agent.Serializers;

/// <summary>
/// Serializes an Umbraco.AI.Agent <see cref="AIAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// The agent's config is written element by element rather than as the JSON blob Umbraco.AI
/// stores, because two parts of it are references that have to be resolved on the target: the
/// contexts a standard agent uses, and its per-user-group tool permissions. User groups are
/// created per site and have a different key on each, so those are matched by alias.
/// </para>
/// <para>
/// A profile, guardrail, context or user group that can't be found on the target is left off
/// with a warning rather than failing the import. For a user group that means its tool
/// permissions are not applied on this server, which is the safe direction: the agent falls
/// back to its default tools for that group, it never gains any.
/// </para>
/// </remarks>
[SyncSerializer("7F0B2C7B-6C1D-4E0F-9B3A-5D2E8A1C4F07", "AI Agent Serializer", "AIAgent")]
public class AIAgentSerializer : SyncSerializerRoot<AIAgent>, ISyncSerializer<AIAgent>
{
    private readonly SyncAIAgentService _agentService;
    private readonly SyncAIService _aiService;

    public AIAgentSerializer(
        ILogger<SyncSerializerRoot<AIAgent>> logger,
        SyncAIAgentService agentService,
        SyncAIService aiService)
        : base(logger)
    {
        _agentService = agentService;
        _aiService = aiService;
    }

    protected override async Task<SyncAttempt<XElement>> SerializeCoreAsync(AIAgent item, SyncSerializerOptions options)
    {
        var node = InitializeBaseNode(item, ItemAlias(item));

        var info = new XElement("Info",
            new XElement("Name", item.Name),
            new XElement("Description", item.Description ?? string.Empty),
            new XElement("AgentType", item.AgentType),
            new XElement("IsActive", item.IsActive));

        info.Add(item.ProfileId is Guid profileId
            ? new XElement("Profile", new XAttribute("Key", profileId), await _aiService.GetProfileAliasAsync(profileId) ?? string.Empty)
            : new XElement("Profile"));

        node.Add(info);
        node.Add(await ReferencesAsync("Guardrails", "Guardrail", item.GuardrailIds, _aiService.GetGuardrailAliasAsync));
        node.Add(Strings("Surfaces", "Surface", item.SurfaceIds));

        if (SyncAIJson.ToElement("Scope", item.Scope) is { } scope) node.Add(scope);

        // A standard agent saved without a config (as the management API allows) is handed to the
        // saved notification with Config null, but reads back with an empty one. Write the empty
        // one either way, or the file never matches the site and reports a change on every sync.
        var itemConfig = item.Config ?? (item.AgentType == AIAgentType.Standard ? new AIStandardAgentConfig() : null);

        switch (itemConfig)
        {
            case AIStandardAgentConfig standard:
                node.Add(await SerializeStandardAsync(standard));
                break;

            case AIOrchestratedAgentConfig orchestrated:
                var config = new XElement("Config", new XElement("WorkflowId", orchestrated.WorkflowId ?? string.Empty));
                if (SyncAIJson.ToElement("Settings", orchestrated.Settings) is { } settings) config.Add(settings);
                node.Add(config);
                break;
        }

        return SyncAttempt<XElement>.Succeed(item.Name, node, ChangeType.Export, []);
    }

    private async Task<XElement> SerializeStandardAsync(AIStandardAgentConfig config)
    {
        var node = new XElement("Config",
            new XElement("Instructions", new XCData(config.Instructions ?? string.Empty)),
            await ReferencesAsync("Contexts", "Context", config.ContextIds, _aiService.GetContextAliasAsync),
            Strings("AllowedTools", "Tool", config.AllowedToolIds),
            Strings("AllowedToolScopes", "Scope", config.AllowedToolScopeIds));

        if (SyncAIJson.ToElement("OutputSchema", config.OutputSchema) is { } schema) node.Add(schema);

        var groups = new List<(Guid Key, string Alias, AIAgentUserGroupPermissions Permissions)>();
        foreach (var (groupKey, permissions) in config.UserGroupPermissions)
            groups.Add((groupKey, await _agentService.GetUserGroupAliasAsync(groupKey) ?? string.Empty, permissions));

        node.Add(new XElement("UserGroupPermissions", groups
            .OrderBy(x => x.Alias, StringComparer.Ordinal).ThenBy(x => x.Key)
            .Select(x => new XElement("UserGroup",
                new XAttribute("Key", x.Key),
                new XAttribute("Alias", x.Alias),
                Strings("AllowedTools", "Tool", x.Permissions.AllowedToolIds),
                Strings("AllowedToolScopes", "Scope", x.Permissions.AllowedToolScopeIds),
                Strings("DeniedTools", "Tool", x.Permissions.DeniedToolIds),
                Strings("DeniedToolScopes", "Scope", x.Permissions.DeniedToolScopeIds)))));

        return node;
    }

    protected override async Task<SyncAttempt<AIAgent>> DeserializeCoreAsync(XElement node, SyncSerializerOptions options)
    {
        var alias = node.GetAlias();
        var key = node.GetKey();

        var info = node.Element("Info");
        var name = info?.Element("Name").ValueOrDefault(alias) ?? alias;
        var agentType = info?.Element("AgentType").ValueOrDefault(AIAgentType.Standard) ?? AIAgentType.Standard;
        var description = info?.Element("Description").ValueOrDefault(string.Empty);

        var item = await FindItemAsync(key) ?? await FindItemAsync(alias);

        if (item is not null && item.AgentType != agentType)
        {
            // AgentType is init-only, and decides what the config means.
            return SyncAttempt<AIAgent>.Fail(name, ChangeType.Fail,
                $"Agent '{alias}' already exists as a {item.AgentType} agent, the file is for {agentType}. Delete the existing agent to import this one.");
        }

        item ??= new AIAgent { Alias = alias, Name = name, AgentType = agentType }.WithId(key);

        var changes = new List<uSyncChange>();

        item.Alias = alias;
        item.Name = name;
        item.Description = string.IsNullOrEmpty(description) ? null : description;
        item.IsActive = info?.Element("IsActive").ValueOrDefault(true) ?? true;
        item.ProfileId = await ResolveAsync(info?.Element("Profile"), "Profile", _aiService.ResolveProfileAsync, changes);
        item.GuardrailIds = await ResolveAllAsync(node.Element("Guardrails"), "Guardrail", _aiService.ResolveGuardrailAsync, changes);
        item.SurfaceIds = ReadStrings(node.Element("Surfaces"), "Surface");

        var scope = node.Element("Scope")?.Value;
        item.Scope = string.IsNullOrWhiteSpace(scope) ? null : JsonSerializer.Deserialize<AIAgentScope>(scope);

        var config = node.Element("Config");
        item.Config = agentType switch
        {
            AIAgentType.Standard => await DeserializeStandardAsync(config, changes),
            AIAgentType.Orchestrated => new AIOrchestratedAgentConfig
            {
                WorkflowId = NullIfEmpty(config?.Element("WorkflowId").ValueOrDefault(string.Empty)),
                Settings = SyncAIJson.ToJsonElement(config?.Element("Settings")),
            },
            _ => null,
        };

        return SyncAttempt<AIAgent>.Succeed(item.Name, item, ChangeType.Import, changes);
    }

    private async Task<AIStandardAgentConfig> DeserializeStandardAsync(XElement? config, List<uSyncChange> changes)
    {
        var permissions = new Dictionary<Guid, AIAgentUserGroupPermissions>();

        foreach (var groupNode in config?.Element("UserGroupPermissions")?.Elements("UserGroup") ?? [])
        {
            var groupAlias = groupNode.Attribute("Alias").ValueOrDefault(string.Empty);
            var groupKey = groupNode.Attribute("Key").ValueOrDefault(Guid.Empty);

            var resolved = await _agentService.ResolveUserGroupAsync(groupKey, groupAlias);
            if (resolved is null)
            {
                changes.AddWarning("UserGroupPermissions", groupAlias,
                    $"User group '{groupAlias}' ({groupKey}) not found on this server - its tool permissions were not applied");
                continue;
            }

            permissions[resolved.Value] = new AIAgentUserGroupPermissions
            {
                AllowedToolIds = ReadStrings(groupNode.Element("AllowedTools"), "Tool"),
                AllowedToolScopeIds = ReadStrings(groupNode.Element("AllowedToolScopes"), "Scope"),
                DeniedToolIds = ReadStrings(groupNode.Element("DeniedTools"), "Tool"),
                DeniedToolScopeIds = ReadStrings(groupNode.Element("DeniedToolScopes"), "Scope"),
            };
        }

        return new AIStandardAgentConfig
        {
            Instructions = NullIfEmpty(config?.Element("Instructions").ValueOrDefault(string.Empty)),
            ContextIds = await ResolveAllAsync(config?.Element("Contexts"), "Context", _aiService.ResolveContextAsync, changes),
            AllowedToolIds = ReadStrings(config?.Element("AllowedTools"), "Tool"),
            AllowedToolScopeIds = ReadStrings(config?.Element("AllowedToolScopes"), "Scope"),
            OutputSchema = SyncAIJson.ToJsonElement(config?.Element("OutputSchema")),
            UserGroupPermissions = permissions,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // none of these lists are ordered as far as Umbraco.AI is concerned, so sort for a stable file.
    private static XElement Strings(string listName, string itemName, IEnumerable<string> values)
        => new(listName, values.OrderBy(x => x, StringComparer.Ordinal).Select(x => new XElement(itemName, x)));

    private static List<string> ReadStrings(XElement? list, string itemName)
        => list?.Elements(itemName).Select(x => x.Value).ToList() ?? [];

    private static async Task<XElement> ReferencesAsync(string listName, string itemName, IEnumerable<Guid> ids, Func<Guid, Task<string?>> getAlias)
    {
        var entries = new List<(Guid Key, string Alias)>();
        foreach (var id in ids) entries.Add((id, await getAlias(id) ?? string.Empty));

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

    public override Task<AIAgent?> FindItemAsync(Guid key) => _agentService.GetAgentAsync(key);

    public override Task<AIAgent?> FindItemAsync(string alias) => _agentService.GetAgentAsync(alias);

    public override Task SaveItemAsync(AIAgent item) => _agentService.SaveAgentAsync(item);

    public override Task DeleteItemAsync(AIAgent item) => _agentService.DeleteAgentAsync(item);

    public override string ItemAlias(AIAgent item) => item.Alias;

    public override Guid ItemKey(AIAgent item) => item.Id;
}
