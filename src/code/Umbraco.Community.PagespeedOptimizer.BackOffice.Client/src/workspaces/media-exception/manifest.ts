import { UMB_WORKSPACE_CONDITION_ALIAS } from '@umbraco-cms/backoffice/workspace';

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
  ],
}

export const manifests = [workspaceView];