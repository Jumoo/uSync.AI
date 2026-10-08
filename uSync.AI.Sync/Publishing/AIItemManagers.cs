using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using uSync.AI.Sync.Services;

namespace uSync.AI.Sync.Publishing;

public class AIConnectionItemManager(SyncAIService aiService) : SyncAIItemManagerBase<AIConnection>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Connection;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Connection;
    protected override string Icon => "icon-plug";
    protected override Task<AIConnection?> GetAsync(Guid key) => aiService.GetConnectionAsync(key);
    protected override Task<IEnumerable<AIConnection>> GetAllAsync() => aiService.GetConnectionsAsync();
    protected override Guid GetKey(AIConnection item) => item.Id;
    protected override string GetName(AIConnection item) => item.Name;
}

public class AIGuardrailItemManager(SyncAIService aiService) : SyncAIItemManagerBase<AIGuardrail>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Guardrail;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Guardrail;
    protected override string Icon => "icon-shield";
    protected override Task<AIGuardrail?> GetAsync(Guid key) => aiService.GetGuardrailAsync(key);
    protected override Task<IEnumerable<AIGuardrail>> GetAllAsync() => aiService.GetGuardrailsAsync();
    protected override Guid GetKey(AIGuardrail item) => item.Id;
    protected override string GetName(AIGuardrail item) => item.Name;
}

public class AIContextItemManager(SyncAIService aiService) : SyncAIItemManagerBase<AIContext>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Context;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Context;
    protected override string Icon => "icon-book-alt";
    protected override Task<AIContext?> GetAsync(Guid key) => aiService.GetContextAsync(key);
    protected override Task<IEnumerable<AIContext>> GetAllAsync() => aiService.GetContextsAsync();
    protected override Guid GetKey(AIContext item) => item.Id;
    protected override string GetName(AIContext item) => item.Name;
}

public class AIProfileItemManager(SyncAIService aiService) : SyncAIItemManagerBase<AIProfile>
{
    protected override string UdiEntityType => uSyncAI.EntityTypes.Profile;
    protected override string ClientEntityType => uSyncAI.ClientEntityTypes.Profile;
    protected override string Icon => "icon-settings-alt";
    protected override Task<AIProfile?> GetAsync(Guid key) => aiService.GetProfileAsync(key);
    protected override Task<IEnumerable<AIProfile>> GetAllAsync() => aiService.GetProfilesAsync();
    protected override Guid GetKey(AIProfile item) => item.Id;
    protected override string GetName(AIProfile item) => item.Name;
}
