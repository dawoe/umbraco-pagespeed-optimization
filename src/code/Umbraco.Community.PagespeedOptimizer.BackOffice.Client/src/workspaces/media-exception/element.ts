import { PageSpeedOptimizer } from "../../api";
import {
  css,
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
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";

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

  @state()
  private _saving = false;

  #notificationContext?: UmbNotificationContext;

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

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notificationContext = context;
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

  async #onSave() {
    if (!this.#mediaKey) return;

    this._saving = true;

    if (this._override) {
      if (this.#existingId) {
        const { error } = await tryExecute(
          this,
          PageSpeedOptimizer.updateMediaException({
            path: { id: this.#existingId },
            body: { quality: this._quality, forceWebp: this._forceWebp },
          }),
        );
        if (!error) this.#notifySuccess();
      } else {
        const { data, error } = await tryExecute(
          this,
          PageSpeedOptimizer.createMediaException({
            body: { mediaKey: this.#mediaKey, quality: this._quality, forceWebp: this._forceWebp },
          }),
        );
        if (!error && data) {
          this.#existingId = data.id;
          this.#notifySuccess();
        }
      }
    } else if (this.#existingId) {
      const { error } = await tryExecute(
        this,
        PageSpeedOptimizer.deleteMediaException({ path: { id: this.#existingId } }),
      );
      if (!error) {
        this.#existingId = undefined;
        this.#notifySuccess();
      }
    } else {
      this.#notifySuccess();
    }

    this._saving = false;
  }

  #notifySuccess() {
    this.#notificationContext?.peek('positive', { data: { message: 'Image optimization settings saved.' } });
  }

  static override styles = [
    css`
      :host {
        display: block;
        margin: var(--uui-size-layout-1);
        padding-bottom: var(--uui-size-layout-1);
      }

      .content {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-4);
        align-items: flex-start;
      }
    `,
  ];

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`
      <uui-box>
        <div class="content">
          <uui-toggle
            label="Override image optimizations"
            ?checked=${this._override}
            @change=${this.#onOverrideChange}
          ></uui-toggle>

          ${this._override
            ? html`
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
              `
            : nothing}

          <uui-button
            label="Save"
            look="primary"
            ?disabled=${this._saving}
            @click=${this.#onSave}
          ></uui-button>
        </div>
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
