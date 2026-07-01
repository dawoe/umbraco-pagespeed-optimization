import { UMB_WORKSPACE_CONDITION_ALIAS } from '@umbraco-cms/backoffice/workspace';
import { UMB_WORKSPACE_CONTENT_TYPE_ALIAS_CONDITION_ALIAS } from '@umbraco-cms/backoffice/content-type';
import { PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS } from '../../conditions/image-optimization-enabled/constants';

const workspaceView : UmbExtensionManifest  = {
  type: 'workspaceView',
  name: 'PagespeedOptimizer Media Exception Workspace View',
  alias: 'pagespeedoptimizer.workspaceView.mediaException',
  element: () => import('./element.ts'),
  // Views sort by descending weight; Media's built-in Details tab is 200 and Info tab is 100,
  // so 150 places this view directly after Details.
  weight: 150,
  meta: {
  label: 'Pagespeed Optimizer',
  pathname: 'pagespeed-optimizer',
  icon: 'icon-scan',
  },
  conditions: [
  {
    alias: UMB_WORKSPACE_CONDITION_ALIAS,
    match: 'Umb.Workspace.Media',
  },
  {
    alias: UMB_WORKSPACE_CONTENT_TYPE_ALIAS_CONDITION_ALIAS,
    match: 'Image',
  },
  {
    alias: PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS,
  },
  ],
}

export const manifests = [workspaceView];