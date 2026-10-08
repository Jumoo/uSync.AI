using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;
using uSync.Core.Extensions;
using uSync.Publisher;

namespace uSync.AI.Complete;

/// <summary>
/// The item managers and dependency checkers that make AI items pushable live in the free
/// uSync.AI packages and are found by uSync's own type scans. This package adds what only makes
/// sense with uSync.Complete installed: the push and pull menu items.
/// </summary>
[ComposeAfter(typeof(uSyncPublishComposer))]
public class uSyncAICompleteComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.AdduSyncAIComplete();
}

public static class BuilderuSyncAICompleteExtensions
{
    public static IUmbracoBuilder AdduSyncAIComplete(this IUmbracoBuilder builder)
    {
        if (builder.IsUmbracoBackOfficeEnabled() is false) return builder;

        builder.Services.AddSingleton<IPackageManifestReader, uSyncAICompleteManifestReader>();

        return builder;
    }
}

/// <summary>
/// Server-driven backoffice manifest: one bundle that registers the push and pull entity
/// actions in <c>complete-client/src/actions/manifest.ts</c>.
/// </summary>
internal class uSyncAICompleteManifestReader : IPackageManifestReader
{
    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        PackageManifest manifest = new()
        {
            Id = "uSync.AI.Complete",
            Name = "uSync.AI.Complete",
            AllowTelemetry = true,
            Version = typeof(uSyncAICompleteManifestReader).Assembly.GetName().Version?.ToString(3) ?? "17.0.0",
            Extensions =
            [
                new JsonObject
                {
                    ["name"] = "uSync.AI.Complete bundle",
                    ["alias"] = "uSync.AI.Complete.Bundle",
                    ["type"] = "bundle",
                    ["js"] = "/App_Plugins/uSync.AI.Complete/bundle.js",
                },
            ],
        };

        return Task.FromResult<IEnumerable<PackageManifest>>([manifest]);
    }
}
