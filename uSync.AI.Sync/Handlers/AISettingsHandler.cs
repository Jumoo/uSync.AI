using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Settings;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using uSync.AI.Sync.Notifications;
using uSync.AI.Sync.Serializers;
using uSync.AI.Sync.Services;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;

namespace uSync.AI.Sync.Handlers;

/// <summary>
/// Syncs the Umbraco.AI <see cref="AISettings"/> singleton (default profiles, disclosure notice).
/// There is always exactly one, with a fixed Id, and it cannot be deleted.
/// </summary>
[SyncHandler(
    "aiSettingsHandler",
    "AI Settings",
    "AI-Settings",
    uSyncAIPriorities.Settings,
    Icon = "icon-settings",
    EntityType = uSyncAI.EntityTypes.Settings)]
public class AISettingsHandler : SyncAIHandlerBase<AISettings>, ISyncHandler,
    INotificationAsyncHandler<AISettingsSavedNotification>
{
    private readonly SyncAIService _aiService;

    public AISettingsHandler(
        ILogger<SyncHandlerRoot<AISettings, AISettings>> logger,
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

    protected override async Task<IEnumerable<AISettings>> GetAllAsync() => [await _aiService.GetSettingsAsync()];

    protected override async Task<AISettings?> GetAsync(Guid key)
        => key == AISettings.SettingsId ? await _aiService.GetSettingsAsync() : null;

    protected override Guid GetKey(AISettings item) => item.Id;

    protected override string GetAlias(AISettings item) => AISettingsSerializer.SettingsAlias;

    protected override string GetItemName(AISettings item) => "AI Settings";

    public Task HandleAsync(AISettingsSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);
}
