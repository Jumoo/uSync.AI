using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace uSync.AI.Sync;

/// <summary>
/// Server-driven backoffice manifest for uSync.AI - a single bundle that ships the localization
/// in <c>ai-client/src/lang/en.ts</c> so item type names render correctly in the uSync dashboard.
/// </summary>
internal class uSyncAIManifestReader : IPackageManifestReader
{
    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        PackageManifest manifest = new()
        {
            Id = "uSync.AI.Sync",
            Name = "uSync.AI",
            AllowTelemetry = true,
            Version = typeof(uSyncAIManifestReader).Assembly.GetName().Version?.ToString(3) ?? "17.0.0",
            Extensions =
            [
                new JsonObject
                {
                    ["name"] = "uSync.AI bundle",
                    ["alias"] = "uSync.AI.Sync.Bundle",
                    ["type"] = "bundle",
                    ["js"] = "/App_Plugins/uSync.AI.Sync/bundle.js",
                },
            ],
        };

        return Task.FromResult<IEnumerable<PackageManifest>>([manifest]);
    }
}
