using System.Collections.Concurrent;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace uSync.AI.Sync.Notifications;

/// <summary>
/// Umbraco.AI's notifications derive from <c>AIEntitySavedNotification&lt;T&gt;</c> /
/// <c>AIEntityDeletedNotification&lt;T&gt;</c>, not uSync's <see cref="SavedNotification{T}"/> -
/// and that is abstract, so it can't be constructed directly either. These concrete shims let a
/// handler forward an AI notification into uSync's own export-on-save / delete-marker logic
/// (<c>SyncHandlerRoot.HandleAsync</c>) instead of reimplementing it.
/// </summary>
internal sealed class AISavedNotification<T> : SavedNotification<T>
{
    public AISavedNotification(T target, EventMessages messages) : base(target, messages) { }
}

internal sealed class AIDeletedNotification<T> : DeletedNotification<T>
{
    public AIDeletedNotification(T target, EventMessages messages) : base(target, messages) { }
}

/// <summary>
/// Published whenever an AI item is saved or deleted - in the backoffice, through the API, or
/// by a uSync import - so anything that caches what it knows about the item can forget it.
/// uSync.AI.Complete uses it to clear uSync.Complete's dependency cache, which only listens for
/// Umbraco's own entities.
/// </summary>
public sealed class SyncAIItemChangedNotification : INotification
{
    public SyncAIItemChangedNotification(Udi udi) => Udi = udi;

    /// <summary>The item that changed, as the UDI its uSync handler and checkers use.</summary>
    public Udi Udi { get; }
}

/// <summary>
/// Umbraco.AI's deleted notifications carry only the entity's Id, but uSync needs the entity
/// (its alias names the file) to write a delete marker. The matching <c>Deleting</c> notification
/// fires while the entity still exists, so handlers stash it here then and collect it on
/// <c>Deleted</c>. An entry left behind by a cancelled delete is replaced the next time that
/// Id is deleted, and is otherwise harmless.
/// </summary>
public sealed class SyncAIPendingDeletes
{
    private readonly ConcurrentDictionary<Guid, object> _items = new();

    public void Stash(Guid id, object entity) => _items[id] = entity;

    public T? Take<T>(Guid id) where T : class
        => _items.TryRemove(id, out var entity) ? entity as T : null;
}
