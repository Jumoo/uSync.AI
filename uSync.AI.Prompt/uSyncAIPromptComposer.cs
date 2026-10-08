using Microsoft.Extensions.DependencyInjection;
using Umbraco.AI.Prompt.Core.Prompts;
using Umbraco.AI.Prompt.Startup.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using uSync.AI.Prompt.Handlers;
using uSync.AI.Prompt.Services;
using uSync.AI.Sync;
using uSync.BackOffice;
using uSync.Core.Extensions;

namespace uSync.AI.Prompt;

/// <summary>
/// Composes after Umbraco.AI.Prompt so its services are registered first.
/// </summary>
[ComposeAfter(typeof(UmbracoAIPromptComposer))]
public class uSyncAIPromptComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AdduSyncAIPrompt();
}

public static class BuilderuSyncAIPromptExtensions
{
    public static IUmbracoBuilder AdduSyncAIPrompt(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        builder.AdduSync();
        builder.AdduSyncAICore();

        builder.Services.AddSingleton<SyncAIPromptService>();

        builder
            .AddNotificationAsyncHandler<AIPromptSavedNotification, AIPromptHandler>()
            .AddNotificationAsyncHandler<AIPromptDeletingNotification, AIPromptHandler>()
            .AddNotificationAsyncHandler<AIPromptDeletedNotification, AIPromptHandler>();

        UdiParser.RegisterUdiType(uSyncAI.EntityTypes.Prompt, UdiType.GuidUdi);

        return builder;
    }
}
