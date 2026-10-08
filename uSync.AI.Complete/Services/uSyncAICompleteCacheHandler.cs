using Umbraco.Cms.Core.Events;
using uSync.AI.Sync.Notifications;
using uSync.Expansions.Core.Cache;

namespace uSync.AI.Complete.Services;

/// <summary>
/// Clears an AI item from uSync.Complete's dependency cache when it is saved or deleted.
/// </summary>
/// <remarks>
/// uSync.Complete caches what each item depends on, and keeps that cache across restarts. It
/// clears an entry when one of Umbraco's own entities is saved, but knows nothing of Umbraco.AI's,
/// so without this a push keeps sending an AI item's dependencies as they were the first time it
/// was pushed - a context added to a prompt since then is left behind.
/// </remarks>
internal sealed class uSyncAICompleteCacheHandler : INotificationHandler<SyncAIItemChangedNotification>
{
    // the cache set the publisher uses - as uSync.Complete's own cache handler.
    private const string PublisherCacheSet = "Publisher";

    private readonly SyncCacheService _cacheService;

    public uSyncAICompleteCacheHandler(SyncCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public void Handle(SyncAIItemChangedNotification notification)
    {
        // new options each time: uSync.Complete sets flags on the ones it is handed.
        var options = new SyncCacheOptions { SetName = PublisherCacheSet, Save = true };

        _cacheService.ClearCaches([notification.Udi], options);

        // the cache is kept on disk, and reloaded at startup - save it (in the background) so a
        // restart doesn't bring the old entry back.
        _cacheService.SaveCaches(options);
    }
}
