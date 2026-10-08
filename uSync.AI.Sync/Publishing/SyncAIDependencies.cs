using Umbraco.AI.Core.Profiles;
using Umbraco.Cms.Core;
using uSync.AI.Sync.Services;
using uSync.Core.Dependency;

namespace uSync.AI.Sync.Publishing;

/// <summary>
/// Builds the dependency lists uSync.Complete uses to decide what else has to travel with an AI
/// item when it is pushed or pulled. Shared by the checkers here and in uSync.AI.Prompt and
/// uSync.AI.Agent so they all order things the same way.
/// </summary>
/// <remarks>
/// Orders sit above uSync's own (which top out at 1200) and match the handler priorities, so
/// what an item needs arrives before it: connections, guardrails and contexts, then profiles,
/// then prompts and agents. A profile's own dependencies are always listed alongside it rather
/// than left for the caller to discover, so a prompt or agent brings its whole chain.
/// </remarks>
public sealed class SyncAIDependencies
{
    public const int ConnectionOrder = 2100;
    public const int GuardrailOrder = 2110;
    public const int ContextOrder = 2120;
    public const int ProfileOrder = 2130;
    public const int PromptOrder = 2140;
    public const int AgentOrder = 2150;

    private readonly SyncAIService _aiService;

    public SyncAIDependencies(SyncAIService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>A dependency entry for one item.</summary>
    public static uSyncDependency Item(string entityType, Guid key, string name, int order, DependencyFlags flags)
        => new()
        {
            Name = name,
            Udi = Udi.Create(entityType, key),
            Order = order,
            Level = 0,
            Mode = DependencyMode.MustExist,
            Flags = flags,
        };

    /// <summary>Whether the caller asked for the things an item depends on, not just the item.</summary>
    public static bool WantsDependencies(DependencyFlags flags) => flags.HasFlag(DependencyFlags.IncludeDependencies);

    public async Task<IEnumerable<uSyncDependency>> ConnectionAsync(Guid key, DependencyFlags flags)
        => await _aiService.GetConnectionAsync(key) is { } item
            ? [Item(uSyncAI.EntityTypes.Connection, item.Id, item.Name, ConnectionOrder, flags)]
            : [];

    public async Task<IEnumerable<uSyncDependency>> GuardrailsAsync(IEnumerable<Guid> keys, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>();
        foreach (var key in keys)
        {
            if (await _aiService.GetGuardrailAsync(key) is { } item)
                items.Add(Item(uSyncAI.EntityTypes.Guardrail, item.Id, item.Name, GuardrailOrder, flags));
        }

        return items;
    }

    public async Task<IEnumerable<uSyncDependency>> ContextsAsync(IEnumerable<Guid> keys, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>();
        foreach (var key in keys)
        {
            if (await _aiService.GetContextAsync(key) is { } item)
                items.Add(Item(uSyncAI.EntityTypes.Context, item.Id, item.Name, ContextOrder, flags));
        }

        return items;
    }

    /// <summary>A profile, and everything the profile itself needs.</summary>
    public async Task<IEnumerable<uSyncDependency>> ProfileAsync(Guid? key, DependencyFlags flags)
    {
        if (key is not Guid profileKey || await _aiService.GetProfileAsync(profileKey) is not { } profile) return [];

        var items = new List<uSyncDependency>
        {
            Item(uSyncAI.EntityTypes.Profile, profile.Id, profile.Name, ProfileOrder, flags),
        };

        items.AddRange(await ForProfileAsync(profile, flags));
        return items;
    }

    /// <summary>What a profile needs: its connection, and a chat profile's contexts and guardrails.</summary>
    public async Task<IEnumerable<uSyncDependency>> ForProfileAsync(AIProfile profile, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>(await ConnectionAsync(profile.ConnectionId, flags));

        if (profile.Settings is AIChatProfileSettings chat)
        {
            items.AddRange(await GuardrailsAsync(chat.GuardrailIds, flags));
            items.AddRange(await ContextsAsync(chat.ContextIds, flags));
        }

        return items;
    }

    /// <summary>The same item can be reached by more than one route; keep one entry per UDI.</summary>
    public static IEnumerable<uSyncDependency> Distinct(IEnumerable<uSyncDependency> items)
        => items.Where(x => x.Udi is not null).DistinctBy(x => x.Udi!.ToString());
}
