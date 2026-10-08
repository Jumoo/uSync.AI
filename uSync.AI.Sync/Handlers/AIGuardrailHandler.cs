using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Guardrails;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using uSync.AI.Sync.Notifications;
using uSync.AI.Sync.Services;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;

namespace uSync.AI.Sync.Handlers;

/// <summary>
/// Syncs Umbraco.AI <see cref="AIGuardrail"/> items.
/// </summary>
[SyncHandler(
    "aiGuardrailHandler",
    "AI Guardrails",
    "AI-Guardrails",
    uSyncAIPriorities.Guardrails,
    Icon = "icon-shield",
    EntityType = uSyncAI.EntityTypes.Guardrail)]
public class AIGuardrailHandler : SyncAIHandlerBase<AIGuardrail>, ISyncHandler,
    INotificationAsyncHandler<AIGuardrailSavedNotification>,
    INotificationAsyncHandler<AIGuardrailDeletingNotification>,
    INotificationAsyncHandler<AIGuardrailDeletedNotification>
{
    private readonly SyncAIService _aiService;

    public AIGuardrailHandler(
        ILogger<SyncHandlerRoot<AIGuardrail, AIGuardrail>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        SyncAIPendingDeletes pendingDeletes,
        IEventAggregator eventAggregator,
        SyncAIService aiService)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory, pendingDeletes, eventAggregator)
    {
        _aiService = aiService;
    }

    protected override Task<IEnumerable<AIGuardrail>> GetAllAsync() => _aiService.GetGuardrailsAsync();

    protected override Task<AIGuardrail?> GetAsync(Guid key) => _aiService.GetGuardrailAsync(key);

    protected override Guid GetKey(AIGuardrail item) => item.Id;

    protected override string GetAlias(AIGuardrail item) => item.Alias;

    protected override string GetItemName(AIGuardrail item) => item.Name;

    public Task HandleAsync(AIGuardrailSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIGuardrailDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIGuardrailDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
