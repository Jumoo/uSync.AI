using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using uSync.AI.Sync.Notifications;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.BackOffice.SyncHandlers;
using uSync.Core;

namespace uSync.AI.Sync.Handlers;

/// <summary>
/// Base for every Umbraco.AI handler. AI entities are flat (no folders, no parents) and are not
/// <see cref="Umbraco.Cms.Core.Models.Entities.IEntity"/>, so this derives from
/// <see cref="SyncHandlerRoot{TObject, TContainer}"/> directly and fills in the tree plumbing once.
/// </summary>
public abstract class SyncAIHandlerBase<TObject> : SyncHandlerRoot<TObject, TObject>
    where TObject : class
{
    private readonly SyncAIPendingDeletes _pendingDeletes;
    private readonly IEventAggregator _eventAggregator;

    public override string Group => uSyncAI.GroupName;

    protected SyncAIHandlerBase(
        ILogger<SyncHandlerRoot<TObject, TObject>> logger,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        ISyncFileService syncFileService,
        ISyncEventService mutexService,
        ISyncConfigService uSyncConfig,
        ISyncItemFactory itemFactory,
        SyncAIPendingDeletes pendingDeletes,
        IEventAggregator eventAggregator)
        : base(logger, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfig, itemFactory)
    {
        _pendingDeletes = pendingDeletes;
        _eventAggregator = eventAggregator;
    }

    protected abstract Task<IEnumerable<TObject>> GetAllAsync();

    protected abstract Task<TObject?> GetAsync(Guid key);

    protected abstract Guid GetKey(TObject item);

    protected abstract string GetAlias(TObject item);

    protected override Task<IEnumerable<uSyncAction>> DeleteMissingItemsAsync(TObject parent, IEnumerable<Guid> keysToKeep, bool reportOnly)
        => Task.FromResult(Enumerable.Empty<uSyncAction>());

    protected override async Task<IEnumerable<TObject>> GetChildItemsAsync(TObject? parent)
        => parent is null ? await GetAllAsync() : [];

    protected override Task<IEnumerable<TObject>> GetFoldersAsync(TObject? parent)
        => Task.FromResult(Enumerable.Empty<TObject>());

    protected override Task<TObject?> GetFromServiceAsync(TObject? item)
        => item is null ? Task.FromResult<TObject?>(null) : GetAsync(GetKey(item));

    protected override string GetItemPath(TObject item, bool useGuid, bool isFlat)
        => useGuid ? GetKey(item).ToString() : GetAlias(item).ToSafeFileName(shortStringHelper);

    /// <summary>Forward an AI saved notification into uSync's export-on-save.</summary>
    protected async Task OnSavedAsync(TObject entity, EventMessages messages, CancellationToken cancellationToken)
    {
        await HandleAsync(new AISavedNotification<TObject>(entity, messages), cancellationToken);
        await PublishChangedAsync(GetKey(entity), cancellationToken);
    }

    /// <summary>Remember the entity while it still exists - see <see cref="SyncAIPendingDeletes"/>.</summary>
    protected async Task OnDeletingAsync(Guid id)
    {
        if (await GetAsync(id) is { } entity) _pendingDeletes.Stash(id, entity);
    }

    /// <summary>Forward an AI deleted notification into uSync's delete-marker logic.</summary>
    protected async Task OnDeletedAsync(Guid id, EventMessages messages, CancellationToken cancellationToken)
    {
        if (_pendingDeletes.Take<TObject>(id) is { } entity)
            await HandleAsync(new AIDeletedNotification<TObject>(entity, messages), cancellationToken);

        await PublishChangedAsync(id, cancellationToken);
    }

    // published whether or not uSync acted on the change (it doesn't during an import), so a
    // cache on a server that receives a push is cleared too.
    private Task PublishChangedAsync(Guid key, CancellationToken cancellationToken)
        => _eventAggregator.PublishAsync(new SyncAIItemChangedNotification(Udi.Create(EntityType, key)), cancellationToken);
}
