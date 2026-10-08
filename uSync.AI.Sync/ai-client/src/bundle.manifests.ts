import { manifests as localizations } from "./lang/manifest.js";

// Job of the bundle is to collate all the manifests from different parts of the extension.
// We load this bundle from the server manifest reader (uSyncAIManifestReader.cs).
export const manifests: Array<UmbExtensionManifest> = [...localizations];
