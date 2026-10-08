using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Contexts;
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
/// Syncs Umbraco.AI <see cref="AIContext"/> items.
/// </summary>
[SyncHandler(
    "aiContextHandler",
    "AI Contexts",
    "AI-Contexts",
    uSyncAIPriorities.Contexts,
    Icon = "icon-book-alt",
    EntityType = uSyncAI.EntityTypes.Context)]
public class AIContextHandler : SyncAIHandlerBase<AIContext>, ISyncHandler,
    INotificationAsyncHandler<AIContextSavedNotification>,
    INotificationAsyncHandler<AIContextDeletingNotification>,
    INotificationAsyncHandler<AIContextDeletedNotification>
{
    private readonly SyncAIService _aiService;

    public AIContextHandler(
        ILogger<SyncHandlerRoot<AIContext, AIContext>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        SyncAIPendingDeletes pendingDeletes,
        SyncAIService aiService)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory, pendingDeletes)
    {
        _aiService = aiService;
    }

    protected override Task<IEnumerable<AIContext>> GetAllAsync() => _aiService.GetContextsAsync();

    protected override Task<AIContext?> GetAsync(Guid key) => _aiService.GetContextAsync(key);

    protected override Guid GetKey(AIContext item) => item.Id;

    protected override string GetAlias(AIContext item) => item.Alias;

    protected override string GetItemName(AIContext item) => item.Name;

    public Task HandleAsync(AIContextSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIContextDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIContextDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
