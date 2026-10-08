const localizations: UmbExtensionManifest = {
  type: "localization",
  alias: "uSync.AI.localization.en",
  name: "uSync AI English Localization",
  weight: 100,
  meta: {
    culture: "en",
  },
  js: () => import("./en.js"),
};

export const manifests: Array<UmbExtensionManifest> = [localizations];
