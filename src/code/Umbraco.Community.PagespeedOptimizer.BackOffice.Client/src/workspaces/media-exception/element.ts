import { PageSpeedOptimizer } from "../../api";
import {
  html,
  nothing,
  LitElement,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import type { UUIToggleElement, UUISliderEvent } from "@umbraco-cms/backoffice/external/uui";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_ENTITY_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import { tryExecute } from "@umbraco-cms/backoffice/resources";

@customElement("media-exception-workspace-view")
export class MediaExceptionWorkspaceView extends UmbElementMixin(LitElement) {
  @state()
  private _loading = true;

  @state()
  private _override = false;

  @state()
  private _quality = 85;

  @state()
  private _forceWebp = false;

  #mediaKey?: string;
  #existingId?: string;
  #defaultQuality = 85;
  #defaultForceWebp = false;

  constructor() {
    super();

    this.consumeContext(UMB_ENTITY_WORKSPACE_CONTEXT, (context) => {
      this.observe(context?.unique, (unique) => {
        if (unique && unique !== this.#mediaKey) {
          this.#mediaKey = unique;
          this.#loadData(unique);
        }
      });
    });
  }

  async #loadData(mediaKey: string) {
    this._loading = true;

    const [byKeyResult, defaultsResult] = await Promise.all([
      tryExecute(this, PageSpeedOptimizer.getByMediaKey({ path: { mediaKey } })),
      tryExecute(this, PageSpeedOptimizer.getDefaultValues()),
    ]);

    if (defaultsResult.data) {
      this.#defaultQuality = defaultsResult.data.defaultImageQuality;
      this.#defaultForceWebp = defaultsResult.data.forceWebP;
    }

    if (byKeyResult.data) {
      this.#existingId = byKeyResult.data.id;
      this._override = true;
      this._quality = byKeyResult.data.quality;
      this._forceWebp = byKeyResult.data.forceWebp;
    } else {
      this.#existingId = undefined;
      this._override = false;
      this._quality = this.#defaultQuality;
      this._forceWebp = this.#defaultForceWebp;
    }

    this._loading = false;
  }

  #onOverrideChange(e: Event) {
    this._override = (e.target as UUIToggleElement).checked;

    if (!this.#existingId) {
      this._quality = this.#defaultQuality;
      this._forceWebp = this.#defaultForceWebp;
    }
  }

  #onQualityChange(e: UUISliderEvent) {
    this._quality = Number(e.target.value);
  }

  #onForceWebpChange(e: Event) {
    this._forceWebp = (e.target as UUIToggleElement).checked;
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`
      <uui-box>
        <uui-toggle
          label="Override image optimizations"
          ?checked=${this._override}
          @change=${this.#onOverrideChange}
        ></uui-toggle>

        ${this._override
          ? html`
              <div style="margin-top: var(--uui-size-space-4);">
                <uui-label>Quality: ${this._quality}</uui-label>
                <uui-slider
                  min="1"
                  max="100"
                  step="1"
                  .value=${this._quality.toString()}
                  @change=${this.#onQualityChange}
                ></uui-slider>

                <uui-toggle
                  label="Force WebP"
                  ?checked=${this._forceWebp}
                  @change=${this.#onForceWebpChange}
                ></uui-toggle>
              </div>
            `
          : nothing}
      </uui-box>
    `;
  }
}

export default MediaExceptionWorkspaceView;

declare global {
  interface HTMLElementTagNameMap {
    "media-exception-workspace-view": MediaExceptionWorkspaceView;
  }
}
