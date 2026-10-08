using Umbraco.Cms.Core;
using uSync.Core.Dependency;
using uSync.Core.Sync;

namespace uSync.AI.Sync.Publishing;

/// <summary>
/// Base item manager for a flat Umbraco.AI entity. Item managers are how uSync.Complete turns
/// the thing a user right-clicked in the backoffice into the items to push or pull. They live in
/// this free package because the interface is part of uSync.Core; without uSync.Complete
/// installed nothing calls them.
/// </summary>
/// <remarks>
/// Each manager answers to two entity types. The backoffice sends Umbraco.AI's own tree entity
/// type (<c>uai:connection</c>), which is not a valid UDI entity type, so the manager hands back
/// UDIs of the type the uSync handler is registered for (<c>umbraco-ai-connection</c>) and is
/// then asked about those.
/// </remarks>
public abstract class SyncAIItemManagerBase<TObject> : SyncItemManagerBase, ISyncItemManager
    where TObject : class
{
    /// <summary>The UDI entity type, matching the uSync handler's <c>EntityType</c>.</summary>
    protected abstract string UdiEntityType { get; }

    /// <summary>The entity type Umbraco.AI's backoffice uses for this item.</summary>
    protected abstract string ClientEntityType { get; }

    protected abstract string Icon { get; }

    protected abstract Task<TObject?> GetAsync(Guid key);

    protected abstract Task<IEnumerable<TObject>> GetAllAsync();

    protected abstract Guid GetKey(TObject item);

    protected abstract string GetName(TObject item);

    // the UDI type first: the base class builds the root item from EntityTypes[0].
    public override string[] EntityTypes => [UdiEntityType, ClientEntityType];

    public async Task<SyncEntity?> GetSyncEntityAsync(string key)
    {
        if (!Guid.TryParse(key, out var guid)) return null;

        return await GetAsync(guid) is { } item ? ToSyncItem(item, DependencyFlags.None) : null;
    }

    /// <summary>These items have no children, so the only descendants are everything under the root.</summary>
    protected override async Task<IEnumerable<SyncItem>> GetDescendantsAsync(SyncItem item, DependencyFlags flags)
    {
        if (!item.Udi.IsRoot) return [];

        return (await GetAllAsync()).Select(x => ToSyncItem(x, flags & ~DependencyFlags.IncludeChildren));
    }

    private SyncItem ToSyncItem(TObject item, DependencyFlags flags) => new(flags)
    {
        Name = GetName(item),
        Icon = Icon,
        Udi = Udi.Create(UdiEntityType, GetKey(item)),
    };
}
