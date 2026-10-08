using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Profiles;
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
/// Syncs Umbraco.AI <see cref="AIProfile"/> items.
/// </summary>
[SyncHandler(
    "aiProfileHandler",
    "AI Profiles",
    "AI-Profiles",
    uSyncAIPriorities.Profiles,
    Icon = "icon-settings-alt",
    EntityType = uSyncAI.EntityTypes.Profile)]
public class AIProfileHandler : SyncAIHandlerBase<AIProfile>, ISyncHandler,
    INotificationAsyncHandler<AIProfileSavedNotification>,
    INotificationAsyncHandler<AIProfileDeletingNotification>,
    INotificationAsyncHandler<AIProfileDeletedNotification>
{
    private readonly SyncAIService _aiService;

    public AIProfileHandler(
        ILogger<SyncHandlerRoot<AIProfile, AIProfile>> logger,
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

    protected override Task<IEnumerable<AIProfile>> GetAllAsync() => _aiService.GetProfilesAsync();

    protected override Task<AIProfile?> GetAsync(Guid key) => _aiService.GetProfileAsync(key);

    protected override Guid GetKey(AIProfile item) => item.Id;

    protected override string GetAlias(AIProfile item) => item.Alias;

    protected override string GetItemName(AIProfile item) => item.Name;

    public Task HandleAsync(AIProfileSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIProfileDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIProfileDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
