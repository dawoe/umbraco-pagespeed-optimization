import { PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS } from './constants';

const conditionManifest: UmbExtensionManifest = {
  type: 'condition',
  name: 'PagespeedOptimizer Image Optimization Enabled Condition',
  alias: PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS,
  api: () => import('./condition'),
};

export const manifests = [conditionManifest];
