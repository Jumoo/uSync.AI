using Microsoft.Extensions.Logging;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using uSync.AI.Sync.Notifications;
using uSync.AI.Agent.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Handlers;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;

namespace uSync.AI.Agent.Handlers;

/// <summary>
/// Syncs Umbraco.AI.Agent <see cref="AIAgent"/> items.
/// </summary>
[SyncHandler(
    "aiAgentHandler",
    "AI Agents",
    "AI-Agents",
    uSyncAIPriorities.Agents,
    Icon = "icon-bot",
    EntityType = uSyncAI.EntityTypes.Agent)]
public class AIAgentHandler : SyncAIHandlerBase<AIAgent>, ISyncHandler,
    INotificationAsyncHandler<AIAgentSavedNotification>,
    INotificationAsyncHandler<AIAgentDeletingNotification>,
    INotificationAsyncHandler<AIAgentDeletedNotification>
{
    private readonly SyncAIAgentService _agentService;

    public AIAgentHandler(
        ILogger<SyncHandlerRoot<AIAgent, AIAgent>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        SyncAIPendingDeletes pendingDeletes,
        SyncAIAgentService agentService)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory, pendingDeletes)
    {
        _agentService = agentService;
    }

    protected override Task<IEnumerable<AIAgent>> GetAllAsync() => _agentService.GetAgentsAsync();

    protected override Task<AIAgent?> GetAsync(Guid key) => _agentService.GetAgentAsync(key);

    protected override Guid GetKey(AIAgent item) => item.Id;

    protected override string GetAlias(AIAgent item) => item.Alias;

    protected override string GetItemName(AIAgent item) => item.Name;

    public Task HandleAsync(AIAgentSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIAgentDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIAgentDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
