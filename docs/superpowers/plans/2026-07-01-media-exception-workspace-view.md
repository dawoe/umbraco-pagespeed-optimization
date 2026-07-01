# Media Exception Workspace View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the media exception workspace view — a workspace view on Media items of type "Image" (only when `ImageOptimizationSettings.Enabled` is true) that lets an editor override image quality/WebP settings per item, backed by the existing management API.

**Architecture:** A small backend change exposes `ImageOptimizationSettings.Enabled` via the existing `GetDefaultValues` endpoint. Two workspace conditions (one built-in, one custom) gate the view's visibility. The view element itself owns all load/save logic directly against the generated `PageSpeedOptimizer` SDK — there is no host-workspace save hook to plug into, so it has its own Save button.

**Tech Stack:** C# / ASP.NET Core (backend), TypeScript + Lit + `@umbraco-cms/backoffice` (client), NUnit + Moq (backend tests), `@hey-api/openapi-ts` (generated client).

## Global Constraints

- Quality range: 1–100, step 1 (spec: [2026-07-01-media-exception-workspace-view-design.md](../specs/2026-07-01-media-exception-workspace-view-design.md)).
- Quality/ForceWebP controls are rendered only when the override toggle is on — never merely disabled.
- The workspace view's Save button is independent from Media's page-level Save; a failure here must never block the Media item's own save.
- All API calls from the client go through `tryExecute` (never raw `fetch`), per `umbraco-openapi-client` skill.
- StyleCop (warnings-as-errors) applies to all C# changes: `using` inside namespace, XML doc comments on public members, copyright header on every file (see any existing file in this repo for the exact header line).

---

### Task 1: Backend — expose `Enabled` on `DefaultValuesResponseModel`

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/DefaultValuesResponseModel.cs`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`
- Modify: `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs`

**Interfaces:**
- Produces: `DefaultValuesResponseModel.Enabled` (`bool`, required) — consumed by Task 2's regenerated TS client and by Task 3's condition.

- [ ] **Step 1: Extend the existing test to assert the new field (will fail to compile)**

In `MediaExceptionManagementApiControllerTests.cs`, replace the `GetDefaultValues_Returns_200_With_Settings_Values` test:

```csharp
    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetDefaultValues"/> returns 200 OK with the values from settings.
    /// </summary>
    [Test]
    public void GetDefaultValues_Returns_200_With_Settings_Values()
    {
        var settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { DefaultImageQuality = 70, ForceWebP = true, Enabled = true },
        };
        this.settingsMock.Setup(s => s.Value).Returns(settings);

        var result = this.controller.GetDefaultValues();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as DefaultValuesResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.DefaultImageQuality, Is.EqualTo(70));
            Assert.That(response.ForceWebP, Is.True);
            Assert.That(response.Enabled, Is.True);
        });
    }
```

- [ ] **Step 2: Run the tests to verify a compile failure**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ --filter GetDefaultValues_Returns_200_With_Settings_Values`
Expected: build error `CS0117: 'DefaultValuesResponseModel' does not contain a definition for 'Enabled'`.

- [ ] **Step 3: Add the `Enabled` property to the response model**

In `DefaultValuesResponseModel.cs`, add after `ForceWebP`:

```csharp
    /// <summary>
    /// Gets or sets a value indicating whether image optimization is enabled globally.
    /// </summary>
    public required bool Enabled { get; set; }
```

- [ ] **Step 4: Populate it in the controller**

In `MediaExceptionManagementApiController.cs`, update `GetDefaultValues()`:

```csharp
    public IActionResult GetDefaultValues()
    {
        var imageSettings = this.settings.Value.ImageOptimization;
        return this.Ok(new DefaultValuesResponseModel
        {
            DefaultImageQuality = imageSettings.DefaultImageQuality,
            ForceWebP = imageSettings.ForceWebP,
            Enabled = imageSettings.Enabled,
        });
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ --filter GetDefaultValues_Returns_200_With_Settings_Values`
Expected: PASS (1 test passed).

- [ ] **Step 6: Run the full backend test suite to check for regressions**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/`
Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/DefaultValuesResponseModel.cs src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs
git commit -m "feat: expose ImageOptimizationSettings.Enabled via default-values endpoint"
```

---

### Task 2: Regenerate the TypeScript API client

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/swagger.json`
- Modify (generated): `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/api/types.gen.ts`
- Modify (generated, if changed): `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/api/sdk.gen.ts`

**Interfaces:**
- Consumes: `Task 1`'s `Enabled` field (must appear in the schema as `enabled: boolean`, matching the C# JSON serialization of `bool Enabled` → camelCase `enabled`).
- Produces: `DefaultValuesResponseModel.enabled` (TS type, in `types.gen.ts`) and `PageSpeedOptimizer.getDefaultValues()` returning it — consumed by Task 3 (condition) and Task 7 (element save/load logic).

This project generates its client from a checked-in `swagger.json` snapshot (see `openapi-ts.config.ts`: `input: 'swagger.json'`), not a live fetch — so update the snapshot by hand to match Task 1's C# change, then run the generator.

- [ ] **Step 1: Update the `DefaultValuesResponseModel` schema in `swagger.json`**

Find the `DefaultValuesResponseModel` schema (search for `"DefaultValuesResponseModel":`) and replace it with:

```json
      "DefaultValuesResponseModel": {
        "required": [
          "defaultImageQuality",
          "enabled",
          "forceWebP"
        ],
        "type": "object",
        "properties": {
          "defaultImageQuality": {
            "type": "integer",
            "format": "int32"
          },
          "enabled": {
            "type": "boolean"
          },
          "forceWebP": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
```

- [ ] **Step 2: Regenerate the client**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`):

```bash
npm run generate-api
```

Expected: command completes without error; `src/api/types.gen.ts` and `src/api/sdk.gen.ts` are rewritten.

- [ ] **Step 3: Verify the generated type includes the new field**

Run: `grep -A4 "export type DefaultValuesResponseModel" src/api/types.gen.ts`
Expected output includes:
```ts
export type DefaultValuesResponseModel = {
    defaultImageQuality: number;
    enabled: boolean;
    forceWebP: boolean;
};
```

- [ ] **Step 4: Type-check the client project**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/swagger.json src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/api
git commit -m "chore: regenerate API client with Enabled field on DefaultValuesResponseModel"
```

---

### Task 3: Custom condition — `ImageOptimizationEnabledCondition`

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/conditions/image-optimization-enabled/constants.ts`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/conditions/image-optimization-enabled/condition.ts`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/conditions/image-optimization-enabled/manifest.ts`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/manifests.ts`

**Interfaces:**
- Consumes: `PageSpeedOptimizer.getDefaultValues()` from `../../api` (Task 2), `tryExecute` from `@umbraco-cms/backoffice/resources`, `UmbConditionBase` from `@umbraco-cms/backoffice/extension-registry`.
- Produces: condition alias `pagespeedoptimizer.condition.imageOptimizationEnabled` (exported as `PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS`) — consumed by Task 4's workspace view manifest.

- [ ] **Step 1: Create the alias constant**

`src/conditions/image-optimization-enabled/constants.ts`:

```ts
export const PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS =
  'pagespeedoptimizer.condition.imageOptimizationEnabled';
```

- [ ] **Step 2: Implement the condition**

`src/conditions/image-optimization-enabled/condition.ts`:

```ts
import { PageSpeedOptimizer } from '../../api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

export class ImageOptimizationEnabledCondition
  extends UmbConditionBase<UmbConditionConfigBase>
  implements UmbExtensionCondition
{
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
    super(host, args);

    tryExecute(this, PageSpeedOptimizer.getDefaultValues()).then(({ data }) => {
      this.permitted = data?.enabled ?? false;
    });
  }
}

export default ImageOptimizationEnabledCondition;
```

- [ ] **Step 3: Register the condition manifest**

`src/conditions/image-optimization-enabled/manifest.ts`:

```ts
import { PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS } from './constants';

const conditionManifest: UmbExtensionManifest = {
  type: 'condition',
  name: 'PagespeedOptimizer Image Optimization Enabled Condition',
  alias: PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS,
  api: () => import('./condition'),
};

export const manifests = [conditionManifest];
```

- [ ] **Step 4: Register it in the extension registry**

Replace `src/manifests.ts`:

```ts
import type { UmbBackofficeExtensionRegistry } from "@umbraco-cms/backoffice/extension-registry";
import { manifests as MediaExceptionWorkspaceViewManifests } from "./workspaces/media-exception/manifest";
import { manifests as ImageOptimizationEnabledConditionManifests } from "./conditions/image-optimization-enabled/manifest";

export function registerManifest(registry: UmbBackofficeExtensionRegistry) {
  registry.registerMany([
    ...MediaExceptionWorkspaceViewManifests,
    ...ImageOptimizationEnabledConditionManifests,
  ]);
}
```

- [ ] **Step 5: Type-check**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/conditions src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/manifests.ts
git commit -m "feat: add image-optimization-enabled workspace condition"
```

---

### Task 4: Gate the workspace view with both conditions

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/manifest.ts`

**Interfaces:**
- Consumes: `PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS` (Task 3), built-in `UMB_WORKSPACE_CONTENT_TYPE_ALIAS_CONDITION_ALIAS` from `@umbraco-cms/backoffice/content-type`.

- [ ] **Step 1: Add the two conditions to the manifest**

Replace `src/workspaces/media-exception/manifest.ts`:

```ts
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
```

- [ ] **Step 2: Type-check**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/manifest.ts
git commit -m "feat: gate media exception workspace view on Image type and optimization enabled"
```

---

### Task 5: Element — data loading and state

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts`

**Interfaces:**
- Consumes: `UMB_ENTITY_WORKSPACE_CONTEXT` from `@umbraco-cms/backoffice/workspace` (`.unique` observable, `string | null`), `PageSpeedOptimizer.getByMediaKey` / `.getDefaultValues` from `../../api`, `tryExecute` from `@umbraco-cms/backoffice/resources`.
- Produces (internal state, consumed by Task 6/7 in the same class): `_loading: boolean`, `_override: boolean`, `_quality: number`, `_forceWebp: boolean`, `#existingId: string | undefined`, `#mediaKey: string | undefined`, `#defaultQuality: number`, `#defaultForceWebp: boolean`.

This task loads data and tracks state; rendering stays a minimal placeholder until Task 6.

- [ ] **Step 1: Rewrite `element.ts` with loading + state**

```ts
import { PageSpeedOptimizer } from "../../api";
import {
  html,
  LitElement,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
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

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }

    return html`<p>Override: ${this._override}, Quality: ${this._quality}, ForceWebP: ${this._forceWebp}</p>`;
  }
}

export default MediaExceptionWorkspaceView;

declare global {
  interface HTMLElementTagNameMap {
    "media-exception-workspace-view": MediaExceptionWorkspaceView;
  }
}
```

- [ ] **Step 2: Type-check**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts
git commit -m "feat: load media exception data and defaults in workspace view"
```

---

### Task 6: Element — rendering and toggle transitions

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts`

**Interfaces:**
- Consumes: state fields from Task 5.
- Produces: `#onOverrideChange`, `#onQualityChange`, `#onForceWebpChange` handlers — the shape of `#onOverrideChange`'s reset behavior is reused verbatim by Task 7 (no changes needed there).

- [ ] **Step 1: Add the full render + toggle handlers**

Update the imports at the top of `element.ts`:

```ts
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
```

Add these handler methods to the class (after `#loadData`):

```ts
  #onOverrideChange(e: Event) {
    this._override = (e.target as UUIToggleElement).checked;

    if (!this.#existingId) {
      this._quality = this.#defaultQuality;
      this._forceWebp = this.#defaultForceWebp;
    }
  }

  #onQualityChange(e: UUISliderEvent) {
    this._quality = Number((e.target as HTMLInputElement).value);
  }

  #onForceWebpChange(e: Event) {
    this._forceWebp = (e.target as UUIToggleElement).checked;
  }
```

Replace `render()`:

```ts
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
```

- [ ] **Step 2: Type-check**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts
git commit -m "feat: render override toggle, quality slider and force webp toggle"
```

---

### Task 7: Element — Save button, persistence and notifications

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts`

**Interfaces:**
- Consumes: `PageSpeedOptimizer.createMediaException` / `.updateMediaException` / `.deleteMediaException` from `../../api`, `UMB_NOTIFICATION_CONTEXT` from `@umbraco-cms/backoffice/notification`.
- Produces: final, complete `element.ts` — no further tasks depend on its internals.

- [ ] **Step 1: Add notification context, save state and the Save handler**

Add to the imports:

```ts
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import type { UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
```

Add a field and consume the notification context in the constructor (after the existing `consumeContext` call):

```ts
  @state()
  private _saving = false;

  #notificationContext?: UmbNotificationContext;
```

```ts
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
      this.#notificationContext = context;
    });
```

Add the save handler and its notification helper (after the toggle/slider handlers):

```ts
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
```

- [ ] **Step 2: Add the Save button to `render()`**

Update the closing of `<uui-box>` in `render()` to include the button after the conditional block:

```ts
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

        <uui-button
          label="Save"
          look="primary"
          style="margin-top: var(--uui-size-space-4);"
          ?disabled=${this._saving}
          @click=${this.#onSave}
        ></uui-button>
      </uui-box>
    `;
  }
```

- [ ] **Step 3: Type-check**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 4: Build the client bundle**

Run (from `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`): `npm run build`
Expected: build succeeds; output written to `../Umbraco.Community.PagespeedOptimizer.BackOffice/wwwroot/App_Plugins/pagespeedoptimizer`.

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts
git commit -m "feat: persist media exception overrides via a dedicated Save button"
```

---

### Task 8: Manual end-to-end verification

**Files:** none (verification only).

No automated test harness exists for this Client project (confirmed during design), so this feature is verified manually against `test-sites/Website-V17/`, per the spec's Testing section.

- [ ] **Step 1: Build and run the backend + test site**

Run: `dotnet build -c Debug src/` then start `test-sites/Website-V17/` (e.g. `dotnet run` from that folder) and log into the backoffice.

- [ ] **Step 2: Verify visibility gating — disabled globally**

In `test-sites/Website-V17/appsettings.json`, ensure `Umbraco:Community:PageSpeedOptimizer:ImageOptimization:Enabled` is `false` (or absent). Open any Media item of type "Image". Confirm the "Pagespeed Optimizer" workspace view tab does **not** appear.

- [ ] **Step 3: Verify visibility gating — wrong media type**

Set `Enabled` to `true` and restart. Open a Media item that is **not** of type "Image" (e.g. a Folder). Confirm the workspace view tab does **not** appear.

- [ ] **Step 4: Verify visibility gating — enabled + Image type**

Open a Media item of type "Image" with `Enabled: true`. Confirm the "Pagespeed Optimizer" tab appears and shows the "Override image optimizations" toggle, initially off, with quality slider and Force WebP toggle hidden.

- [ ] **Step 5: Verify create flow**

Turn the override toggle on. Confirm the quality slider (defaulting to the global `DefaultImageQuality`) and Force WebP toggle (defaulting to the global `ForceWebP`) appear. Change the quality to a different value and toggle Force WebP on. Click Save. Confirm a success notification appears.

- [ ] **Step 6: Verify update flow**

Reload the page (or navigate away and back to the same Media item). Confirm the toggle is on and the quality/Force WebP values match what was saved in Step 5. Change the quality again and click Save. Confirm success, and that no duplicate record was created (re-check by reloading again — values reflect the latest save).

- [ ] **Step 7: Verify delete flow**

With override still on and a saved record present, turn the override toggle off (slider/toggle disappear). Click Save. Confirm a success notification. Reload the page — confirm the override toggle is off and, if turned back on, shows the global defaults again (not the previously saved override values).

- [ ] **Step 8: Verify error handling**

Stop the backend (or temporarily block the API route) and click Save with override on. Confirm a standard Umbraco error notification appears and the Media item's own page-level Save is unaffected (still works normally).

---

### Task 9: Update `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Document the new workspace view, condition, and client structure**

Add a new subsection under "## Architecture Notes" in `CLAUDE.md`:

```markdown
- **Media Exception Workspace View** — `pagespeedoptimizer.workspaceView.mediaException` (in `Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/`) shows an "Override image optimizations" toggle, quality slider, and Force WebP toggle on Media items of type "Image", backed by `MediaExceptionManagementApiController`. It is gated by three workspace conditions: `Umb.Workspace.Media`, the built-in `Umb.Condition.WorkspaceContentTypeAlias` (matching `Image`), and a custom `pagespeedoptimizer.condition.imageOptimizationEnabled` condition (in `src/conditions/image-optimization-enabled/`) that calls the `GetDefaultValues` endpoint to check `ImageOptimizationSettings.Enabled`. The view has its own Save button — Umbraco's workspace/save pipeline has no extension point for a `workspaceView` to hook into a host workspace's native save.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: document media exception workspace view and its conditions"
```
