import { UMB_WORKSPACE_CONDITION_ALIAS } from '@umbraco-cms/backoffice/workspace';
import { UMB_WORKSPACE_CONTENT_TYPE_ALIAS_CONDITION_ALIAS } from '@umbraco-cms/backoffice/content-type';
import { PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS } from '../../conditions/image-optimization-enabled/constants';

const workspaceView : UmbExtensionManifest  = {
  type: 'workspaceView',
  name: 'PagespeedOptimizer Media Exception Workspace View',
  alias: 'pagespeedoptimizer.workspaceView.mediaException',
  element: () => import('./element.ts'),
  weight: 900,
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