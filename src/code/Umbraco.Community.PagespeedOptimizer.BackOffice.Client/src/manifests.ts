import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as MediaExceptionWorkspaceViewManifests } from "./workspaces/media-exception/manifest";
import { manifests as ImageOptimizationEnabledConditionManifests } from "./conditions/image-optimization-enabled/manifest";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([
    ...MediaExceptionWorkspaceViewManifests,
    ...ImageOptimizationEnabledConditionManifests,
  ]);
}