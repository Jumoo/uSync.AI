using Umbraco.AI.Agent.Core.Agents;
using Umbraco.Cms.Core.Models;
using uSync.AI.Agent.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Publishing;
using uSync.Core.Dependency;

namespace uSync.AI.Agent.Publishing;

/// <summary>Lets uSync.Complete push and pull agents. See <see cref="SyncAIItemManagerBase{TObject}"/>.</summary>
public class AIAgentItemManager(SyncAIAgentService agentService) : SyncAIItemManagerBase<AIAgent>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Agent;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Agent;
    protected override string Icon => "icon-bot";
    protected override Task<AIAgent?> GetAsync(Guid key) => agentService.GetAgentAsync(key);
    protected override Task<IEnumerable<AIAgent>> GetAllAsync() => agentService.GetAgentsAsync();
    protected override Guid GetKey(AIAgent item) => item.Id;
    protected override string GetName(AIAgent item) => item.Name;
}

/// <summary>
/// An agent needs its profile (and everything that profile needs), its guardrails and, for a
/// standard agent, its contexts. User groups are not dependencies: they are not synced, and the
/// agent serializer matches them by alias on the target.
/// </summary>
public class AIAgentDependencyChecker : ISyncDependencyChecker<AIAgent>
{
    private readonly SyncAIDependencies _dependencies;

    public AIAgentDependencyChecker(SyncAIDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public async Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIAgent item, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>
        {
            SyncAIDependencies.Item(uSyncAI.EntityTypes.Agent, item.Id, item.Name, SyncAIDependencies.AgentOrder, flags),
        };

        if (SyncAIDependencies.WantsDependencies(flags))
        {
            items.AddRange(await _dependencies.ProfileAsync(item.ProfileId, flags));
            items.AddRange(await _dependencies.GuardrailsAsync(item.GuardrailIds, flags));

            if (item.Config is AIStandardAgentConfig standard)
                items.AddRange(await _dependencies.ContextsAsync(standard.ContextIds, flags));
        }

        return SyncAIDependencies.Distinct(items);
    }
}
