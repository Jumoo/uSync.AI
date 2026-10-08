using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Connections;
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
/// Syncs Umbraco.AI <see cref="AIConnection"/> items.
/// </summary>
[SyncHandler(
    "aiConnectionHandler",
    "AI Connections",
    "AI-Connections",
    uSyncAIPriorities.Connections,
    Icon = "icon-plug",
    EntityType = uSyncAI.EntityTypes.Connection)]
public class AIConnectionHandler : SyncAIHandlerBase<AIConnection>, ISyncHandler,
    INotificationAsyncHandler<AIConnectionSavedNotification>,
    INotificationAsyncHandler<AIConnectionDeletingNotification>,
    INotificationAsyncHandler<AIConnectionDeletedNotification>
{
    private readonly SyncAIService _aiService;

    public AIConnectionHandler(
        ILogger<SyncHandlerRoot<AIConnection, AIConnection>> logger,
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

    protected override Task<IEnumerable<AIConnection>> GetAllAsync() => _aiService.GetConnectionsAsync();

    protected override Task<AIConnection?> GetAsync(Guid key) => _aiService.GetConnectionAsync(key);

    protected override Guid GetKey(AIConnection item) => item.Id;

    protected override string GetAlias(AIConnection item) => item.Alias;

    protected override string GetItemName(AIConnection item) => item.Name;

    public Task HandleAsync(AIConnectionSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIConnectionDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIConnectionDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
