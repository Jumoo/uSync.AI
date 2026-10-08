using Microsoft.Extensions.Options;
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
/// then prompts and agents.
/// <para>
/// Each checker lists only an item's direct dependencies - a prompt names its profile, not the
/// profile's connection. uSync.Complete asks each dependency for its own in turn, so a prompt
/// still brings its whole chain, and because it caches each item's list separately, saving a
/// profile only has to clear the profile's entry for a later push of the prompt to be right.
/// </para>
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
    private readonly IOptionsMonitor<uSyncAIOptions> _options;

    public SyncAIDependencies(SyncAIService aiService, IOptionsMonitor<uSyncAIOptions> options)
    {
        _aiService = aiService;
        _options = options;
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

    /// <summary>
    /// Whether to list the things an item depends on, not just the item: always, unless
    /// <see cref="uSyncAIPublishingOptions.AlwaysIncludeDependencies"/> is off, in which case only
    /// when the caller asked for them.
    /// </summary>
    public bool WantsDependencies(DependencyFlags flags)
        => _options.CurrentValue.Publishing.AlwaysIncludeDependencies
            || flags.HasFlag(DependencyFlags.IncludeDependencies);

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

    /// <summary>A profile - its own dependencies come from its checker, see the remarks above.</summary>
    public async Task<IEnumerable<uSyncDependency>> ProfileAsync(Guid? key, DependencyFlags flags)
        => key is Guid profileKey && await _aiService.GetProfileAsync(profileKey) is { } profile
            ? [Item(uSyncAI.EntityTypes.Profile, profile.Id, profile.Name, ProfileOrder, flags)]
            : [];

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
