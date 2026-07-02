import type { UmbEntryPointOnInit } from '@umbraco-cms/backoffice/extension-api';
import { registerManifest } from './manifests';

export const onInit: UmbEntryPointOnInit = (_, extensionRegistry) => {
  registerManifest(extensionRegistry);
};