using Microsoft.Extensions.Logging;
using Umbraco.AI.Prompt.Core.Prompts;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using uSync.AI.Sync.Notifications;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync;
using uSync.AI.Sync.Handlers;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;

namespace uSync.AI.Prompt.Handlers;

/// <summary>
/// Syncs Umbraco.AI.Prompt <see cref="AIPrompt"/> items.
/// </summary>
[SyncHandler(
    "aiPromptHandler",
    "AI Prompts",
    "AI-Prompts",
    uSyncAIPriorities.Prompts,
    Icon = "icon-chat",
    EntityType = uSyncAI.EntityTypes.Prompt)]
public class AIPromptHandler : SyncAIHandlerBase<AIPrompt>, ISyncHandler,
    INotificationAsyncHandler<AIPromptSavedNotification>,
    INotificationAsyncHandler<AIPromptDeletingNotification>,
    INotificationAsyncHandler<AIPromptDeletedNotification>
{
    private readonly SyncAIPromptService _promptService;

    public AIPromptHandler(
        ILogger<SyncHandlerRoot<AIPrompt, AIPrompt>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        SyncAIPendingDeletes pendingDeletes,
        SyncAIPromptService promptService)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory, pendingDeletes)
    {
        _promptService = promptService;
    }

    protected override Task<IEnumerable<AIPrompt>> GetAllAsync() => _promptService.GetPromptsAsync();

    protected override Task<AIPrompt?> GetAsync(Guid key) => _promptService.GetPromptAsync(key);

    protected override Guid GetKey(AIPrompt item) => item.Id;

    protected override string GetAlias(AIPrompt item) => item.Alias;

    protected override string GetItemName(AIPrompt item) => item.Name;

    public Task HandleAsync(AIPromptSavedNotification notification, CancellationToken cancellationToken)
        => OnSavedAsync(notification.Entity, notification.Messages, cancellationToken);

    public Task HandleAsync(AIPromptDeletingNotification notification, CancellationToken cancellationToken)
        => OnDeletingAsync(notification.EntityId);

    public Task HandleAsync(AIPromptDeletedNotification notification, CancellationToken cancellationToken)
        => OnDeletedAsync(notification.EntityId, notification.Messages, cancellationToken);
}
