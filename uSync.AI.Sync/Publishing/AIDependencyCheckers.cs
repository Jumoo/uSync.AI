using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.Cms.Core.Models;
using uSync.Core.Dependency;

namespace uSync.AI.Sync.Publishing;

// Dependency checkers tell uSync.Complete what has to be on the target for an item to work.
// Umbraco.AI's entities are not Umbraco object types, so ObjectType is Unknown throughout;
// uSync finds a checker by the entity type it is declared for, not by that value.

public class AIConnectionDependencyChecker : ISyncDependencyChecker<AIConnection>
{
    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIConnection item, DependencyFlags flags)
        => Task.FromResult<IEnumerable<uSyncDependency>>(
            [SyncAIDependencies.Item(uSyncAI.EntityTypes.Connection, item.Id, item.Name, SyncAIDependencies.ConnectionOrder, flags)]);
}

public class AIGuardrailDependencyChecker : ISyncDependencyChecker<AIGuardrail>
{
    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIGuardrail item, DependencyFlags flags)
        => Task.FromResult<IEnumerable<uSyncDependency>>(
            [SyncAIDependencies.Item(uSyncAI.EntityTypes.Guardrail, item.Id, item.Name, SyncAIDependencies.GuardrailOrder, flags)]);
}

public class AIContextDependencyChecker : ISyncDependencyChecker<AIContext>
{
    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIContext item, DependencyFlags flags)
        => Task.FromResult<IEnumerable<uSyncDependency>>(
            [SyncAIDependencies.Item(uSyncAI.EntityTypes.Context, item.Id, item.Name, SyncAIDependencies.ContextOrder, flags)]);
}

/// <summary>A profile needs its connection, and a chat profile its contexts and guardrails.</summary>
public class AIProfileDependencyChecker : ISyncDependencyChecker<AIProfile>
{
    private readonly SyncAIDependencies _dependencies;

    public AIProfileDependencyChecker(SyncAIDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public async Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIProfile item, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>
        {
            SyncAIDependencies.Item(uSyncAI.EntityTypes.Profile, item.Id, item.Name, SyncAIDependencies.ProfileOrder, flags),
        };

        if (SyncAIDependencies.WantsDependencies(flags))
            items.AddRange(await _dependencies.ForProfileAsync(item, flags));

        return SyncAIDependencies.Distinct(items);
    }
}
