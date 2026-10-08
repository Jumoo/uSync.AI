using Microsoft.Extensions.DependencyInjection;
using Umbraco.AI.Agent.Core.Agents;
using Umbraco.AI.Agent.Startup.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using uSync.AI.Agent.Handlers;
using uSync.AI.Agent.Services;
using uSync.AI.Sync;
using uSync.BackOffice;
using uSync.Core.Extensions;

namespace uSync.AI.Agent;

/// <summary>
/// Composes after Umbraco.AI.Agent so its services are registered first.
/// </summary>
[ComposeAfter(typeof(UmbracoAIAgentComposer))]
public class uSyncAIAgentComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AdduSyncAIAgent();
}

public static class BuilderuSyncAIAgentExtensions
{
    public static IUmbracoBuilder AdduSyncAIAgent(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        builder.AdduSync();
        builder.AdduSyncAICore();

        builder.Services.AddSingleton<SyncAIAgentService>();

        builder
            .AddNotificationAsyncHandler<AIAgentSavedNotification, AIAgentHandler>()
            .AddNotificationAsyncHandler<AIAgentDeletingNotification, AIAgentHandler>()
            .AddNotificationAsyncHandler<AIAgentDeletedNotification, AIAgentHandler>();

        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Agent, UdiType.GuidUdi);

        return builder;
    }
}
