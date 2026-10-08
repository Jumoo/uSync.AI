using Umbraco.AI.Prompt.Core.Prompts;
using Umbraco.Cms.Core.Models;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Publishing;
using uSync.Core.Dependency;

namespace uSync.AI.Prompt.Publishing;

/// <summary>Lets uSync.Complete push and pull prompts. See <see cref="SyncAIItemManagerBase{TObject}"/>.</summary>
public class AIPromptItemManager(SyncAIPromptService promptService) : SyncAIItemManagerBase<AIPrompt>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Prompt;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Prompt;
    protected override string Icon => "icon-chat";
    protected override Task<AIPrompt?> GetAsync(Guid key) => promptService.GetPromptAsync(key);
    protected override Task<IEnumerable<AIPrompt>> GetAllAsync() => promptService.GetPromptsAsync();
    protected override Guid GetKey(AIPrompt item) => item.Id;
    protected override string GetName(AIPrompt item) => item.Name;
}

/// <summary>
/// A prompt needs its profile (and everything that profile needs), its contexts and its guardrails.
/// </summary>
public class AIPromptDependencyChecker : ISyncDependencyChecker<AIPrompt>
{
    private readonly SyncAIDependencies _dependencies;

    public AIPromptDependencyChecker(SyncAIDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    public UmbracoObjectTypes ObjectType => UmbracoObjectTypes.Unknown;

    public async Task<IEnumerable<uSyncDependency>> GetDependenciesAsync(AIPrompt item, DependencyFlags flags)
    {
        var items = new List<uSyncDependency>
        {
            SyncAIDependencies.Item(uSyncAI.EntityTypes.Prompt, item.Id, item.Name, SyncAIDependencies.PromptOrder, flags),
        };

        if (SyncAIDependencies.WantsDependencies(flags))
        {
            items.AddRange(await _dependencies.ProfileAsync(item.ProfileId, flags));
            items.AddRange(await _dependencies.GuardrailsAsync(item.GuardrailIds, flags));
            items.AddRange(await _dependencies.ContextsAsync(item.ContextIds, flags));
        }

        return SyncAIDependencies.Distinct(items);
    }
}
