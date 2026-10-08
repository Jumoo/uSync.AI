using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.AI.Core.Connections;
using Umbraco.AI.Core.Contexts;
using Umbraco.AI.Core.Guardrails;
using Umbraco.AI.Core.Profiles;
using Umbraco.AI.Core.Settings;
using Umbraco.AI.Startup.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Manifest;
using uSync.AI.Sync.Handlers;
using uSync.AI.Sync.Notifications;
using uSync.AI.Sync.Publishing;
using uSync.AI.Sync.Services;
using uSync.BackOffice;
using uSync.Core.Extensions;

namespace uSync.AI.Sync;

/// <summary>
/// Composes after Umbraco.AI so its services are registered first -
/// <see cref="SyncAIService"/> depends on them.
/// </summary>
[ComposeAfter(typeof(UmbracoAIComposer))]
public class uSyncAIComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AdduSyncAI();
}

public static class BuilderuSyncAIExtensions
{
    public static IUmbracoBuilder AdduSyncAI(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        // idempotent - short-circuits if uSync's own ISyncConfigService is already registered
        builder.AdduSync();

        builder.AdduSyncAICore();

        // Export-on-save/delete. Umbraco.AI's notifications aren't uSync's SavedNotification<T> /
        // DeletedNotification<T> - each handler adapts via a shim, see Notifications/.
        builder
            .AddNotificationAsyncHandler<AIConnectionSavedNotification, AIConnectionHandler>()
            .AddNotificationAsyncHandler<AIConnectionDeletingNotification, AIConnectionHandler>()
            .AddNotificationAsyncHandler<AIConnectionDeletedNotification, AIConnectionHandler>()
            .AddNotificationAsyncHandler<AIGuardrailSavedNotification, AIGuardrailHandler>()
            .AddNotificationAsyncHandler<AIGuardrailDeletingNotification, AIGuardrailHandler>()
            .AddNotificationAsyncHandler<AIGuardrailDeletedNotification, AIGuardrailHandler>()
            .AddNotificationAsyncHandler<AIContextSavedNotification, AIContextHandler>()
            .AddNotificationAsyncHandler<AIContextDeletingNotification, AIContextHandler>()
            .AddNotificationAsyncHandler<AIContextDeletedNotification, AIContextHandler>()
            .AddNotificationAsyncHandler<AIProfileSavedNotification, AIProfileHandler>()
            .AddNotificationAsyncHandler<AIProfileDeletingNotification, AIProfileHandler>()
            .AddNotificationAsyncHandler<AIProfileDeletedNotification, AIProfileHandler>()
            .AddNotificationAsyncHandler<AISettingsSavedNotification, AISettingsHandler>();

        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Connection, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Guardrail, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Context, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Profile, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Settings, UdiType.GuidUdi);

        builder.Services.AddSingleton<IPackageManifestReader, uSyncAIManifestReader>();

        return builder;
    }

    /// <summary>
    /// The services uSync.AI.Prompt and uSync.AI.Agent share with this package. Safe to call
    /// from each of their composers, whatever order they run in.
    /// </summary>
    public static IUmbracoBuilder AdduSyncAICore(this IUmbracoBuilder builder)
    {
        builder.Services.TryAddSingleton<SyncAIService>();
        builder.Services.TryAddSingleton<SyncAIPendingDeletes>();
        builder.Services.TryAddSingleton<SyncAIDependencies>();
        builder.Services.AddOptions<uSyncAIOptions>().Bind(builder.Config.GetSection(uSyncAIOptions.Section));

        // uSyncConstants.Groups.Icons is a public mutable dictionary with no "AI" entry; TryAdd so
        // a future uSync release that adds its own always wins.
        uSyncConstants.Groups.Icons.TryAdd(uSyncAI.GroupName, uSyncAI.GroupIcon);

        return builder;
    }
}
