import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as MediaExceptionWorkspaceViewManifests } from "./workspaces/media-exception/manifest";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([
    ...MediaExceptionWorkspaceViewManifests,
  ]);
}