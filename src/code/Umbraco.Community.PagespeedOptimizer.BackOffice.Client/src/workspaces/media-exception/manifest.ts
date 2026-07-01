import { UMB_WORKSPACE_CONDITION_ALIAS } from '@umbraco-cms/backoffice/workspace';

const workspaceView : UmbExtensionManifest  = {
  type: 'workspaceView',
  name: 'Example Counter Workspace View',
  alias: 'example.workspaceView.counter',
  element: () => import('./element.ts'),
  weight: 900,
  meta: {
  label: 'PagespeedOptimizer',
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