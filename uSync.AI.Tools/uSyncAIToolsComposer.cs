using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using uSync.AI.Tools.Security;
using uSync.AI.Tools.Services;
using uSync.BackOffice;
using uSync.Core.Extensions;

namespace uSync.AI.Tools;

/// <summary>
/// The tools and tool scopes are found by Umbraco.AI's type scan, so nothing registers them
/// here. This composer only wires up the services they are built from.
/// </summary>
public class uSyncAIToolsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AdduSyncAITools();
}

public static class BuilderuSyncAIToolsExtensions
{
    public static IUmbracoBuilder AdduSyncAITools(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        // idempotent - short-circuits if uSync is already registered
        builder.AdduSync();

        builder.Services.AddOptions<uSyncAIToolsOptions>().Bind(builder.Config.GetSection(uSyncAIToolsOptions.Section));

        // TryAdd so uSync.AI.Complete's publisher tools share the one gate and authorizer.
        builder.Services.TryAddSingleton<IuSyncOperationGate, uSyncOperationGate>();
        builder.Services.TryAddSingleton<IuSyncToolAuthorizer, uSyncToolAuthorizer>();
        builder.Services.TryAddSingleton<uSyncToolRunner>();

        return builder;
    }
}
