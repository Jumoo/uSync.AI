import { manifests as actions } from "./actions/manifest.js";

// Job of the bundle is to collate all the manifests from different parts of the extension.
// We load this bundle from the server manifest reader (uSyncAICompleteManifestReader.cs).
export const manifests: Array<UmbExtensionManifest> = [...actions];
