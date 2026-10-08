const localizations: UmbExtensionManifest = {
  type: "localization",
  alias: "uSync.AI.Complete.localization.en",
  name: "uSync AI Complete English Localization",
  weight: 100,
  meta: {
    culture: "en",
  },
  js: () => import("./en.js"),
};

export const manifests: Array<UmbExtensionManifest> = [localizations];
