import {
  html,
  LitElement,
  customElement,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

@customElement("media-exception-workspace-view")
export class MediaExceptionWorkspaceView extends UmbElementMixin(LitElement) {
  constructor() {
    super();
  }

  override render() {
    return html` <p>Workspace will go here</p> `;
  }
}

export default MediaExceptionWorkspaceView;

declare global {
  interface HTMLElementTagNameMap {
    "media-exception-workspace-view": MediaExceptionWorkspaceView;
  }
}
