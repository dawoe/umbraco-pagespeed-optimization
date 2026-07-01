# Design: Media Exception Workspace View

**Date:** 2026-07-01
**Branch:** feature/media-exception-workspace
**Status:** Approved

## Overview

Implement the backoffice UI for per-media-item image optimization overrides. A workspace view is shown on Media items of type alias "Image" — but only when image optimization is enabled globally (`ImageOptimizationSettings.Enabled`). The view lets an editor override the global image quality and Force WebP settings for that specific media item, backed by the already-existing `MediaExceptionManagementApiController` CRUD API and generated `PageSpeedOptimizer` TypeScript SDK.

## Purpose

Give content editors a way to override page-speed image optimization behavior (quality, WebP forcing) on individual images, without needing developer involvement, while keeping the feature entirely hidden when image optimization is switched off.

## Backend Change

The `ImageOptimizationSettings.Enabled` flag is not currently exposed via the management API. Extend the existing default-values endpoint rather than adding a new one:

**`Umbraco.Community.PagespeedOptimizer.BackOffice/Models/DefaultValuesResponseModel.cs`**
- Add `public required bool Enabled { get; set; }`.

**`Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`**
- In `GetDefaultValues()`, set `Enabled = imageSettings.Enabled` alongside the existing fields.

After this change, regenerate the TypeScript client (existing hey-api generation step) so `DefaultValuesResponseModel.enabled` is available client-side. No other backend changes are required — create/update/delete/get-by-media-key already exist and are sufficient.

## Client-side Conditions

Two conditions are added to the existing workspace view manifest (`src/workspaces/media-exception/manifest.ts`), combined with the existing `Umb.Workspace.Media` condition. All entries in a manifest's `conditions` array are AND-ed together by Umbraco.

### 1. Media type alias = "Image"

No custom code needed — Umbraco ships `Umb.Condition.WorkspaceContentTypeAlias` (confirmed present in Umbraco CMS 17.4.2 source, package `@umbraco-cms/backoffice/content-type`), which accepts a `match` (or `oneOf`) config and observes `UMB_PROPERTY_STRUCTURE_WORKSPACE_CONTEXT` to compare against the current content type's aliases.

### 2. Image optimization enabled

A new custom condition, registered under a new `src/conditions/image-optimization-enabled/` folder in the Client project:

```
src/conditions/image-optimization-enabled/
  constants.ts    # exports PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS
  condition.ts    # ImageOptimizationEnabledCondition extends UmbConditionBase
  manifest.ts     # { type: 'condition', alias, api }
```

`ImageOptimizationEnabledCondition`:
- Extends `UmbConditionBase` (no config needed beyond the base).
- In the constructor, calls `PageSpeedOptimizer.getDefaultValues()` via `tryExecute(this, promise)`.
- Sets `this.permitted = data?.enabled ?? false` once the call resolves (conditions may set `permitted` asynchronously at any time after construction — the framework reacts to the change).

Register the condition manifest in `src/manifests.ts` alongside the existing workspace view manifest registration.

Resulting `manifests.ts` conditions array for the workspace view:

```ts
conditions: [
  { alias: UMB_WORKSPACE_CONDITION_ALIAS, match: 'Umb.Workspace.Media' },
  { alias: UMB_WORKSPACE_CONTENT_TYPE_ALIAS_CONDITION_ALIAS, match: 'Image' },
  { alias: PAGESPEED_OPTIMIZER_IMAGE_OPTIMIZATION_ENABLED_CONDITION_ALIAS },
]
```

## Element Design

Rewrite `src/workspaces/media-exception/element.ts` (currently a stub).

### Data loading

On connect, the element consumes `UMB_ENTITY_WORKSPACE_CONTEXT` to obtain the current media item's `unique` (media key). It then fires two calls in parallel, each via `tryExecute(this, promise, { disableNotifications: false })`:

- `PageSpeedOptimizer.getByMediaKey({ path: { mediaKey } })`
- `PageSpeedOptimizer.getDefaultValues()`

While either call is in flight, render a `<uui-loader>` in place of the form.

Resulting internal state:

| Outcome | `override` | `quality` / `forceWebp` | `id` |
|---|---|---|---|
| `getByMediaKey` → 200 | `true` | from the returned record | the record's `id` |
| `getByMediaKey` → 404 | `false` | from `getDefaultValues()` (`defaultImageQuality` / `forceWebP`) | `undefined` |

The fetched defaults are retained in state for reuse during toggle transitions (see below), regardless of which branch was taken.

### Rendering

- `uui-toggle` labeled **"Override image optimizations"**, bound to `override`.
- Only when `override` is `true`:
  - `uui-slider`, min `1`, max `100`, step `1`, bound to `quality`, with the numeric value displayed alongside the slider.
  - `uui-toggle` labeled **"Force WebP"**, bound to `forceWebp`.
  - When `override` is `false`, neither control is rendered at all (not merely disabled).
- A **Save** button at the bottom of the view. This is this view's own save action, entirely separate from Media's page-level Save button — research into `UmbMediaWorkspaceContext`'s save pipeline (via `UmbContentDetailWorkspaceContextBase` → `UmbMediaDetailRepository` → `UmbMediaServerDataSource`) confirmed there is no extension point for a `workspaceView` to hook into the host workspace's submit/save flow; the only push-based extensibility on the workspace context is validation-context registration, which does not apply to persistence.

### Toggle transitions

- Turning `override` **on**: if `id` is `undefined` (no existing persisted record), (re-)populate `quality`/`forceWebp` from the previously-fetched global defaults as the starting point for editing.
- Turning `override` **off**: if `id` is `undefined`, reset `quality`/`forceWebp` back to the global defaults. If `id` is set (a persisted record exists), leave `quality`/`forceWebp` untouched in memory — nothing is deleted client-side; deletion only happens on Save. This means toggling off and back on again before saving restores exactly what was loaded.

## Save / Persistence

Save button click handler, based on current state:

| `override` | `id` | Action |
|---|---|---|
| `true` | set | `PageSpeedOptimizer.updateMediaException({ path: { id }, body: { quality, forceWebp } })` |
| `true` | `undefined` | `PageSpeedOptimizer.createMediaException({ body: { mediaKey, quality, forceWebp } })` — store the returned `id` in state |
| `false` | set | `PageSpeedOptimizer.deleteMediaException({ path: { id } })` — clear `id` from state |
| `false` | `undefined` | No-op |

## Error Handling

All API calls go through `tryExecute(this, promise, { disableNotifications: false })` (the current, non-deprecated replacement for `tryExecuteAndNotify`), which automatically shows Umbraco's standard error notification on failure. No bespoke error UI is needed. A failure here never blocks or interacts with the Media item's own page-level save — the two save actions are fully independent.

On a successful save, show a short success notification via the standard notification context, consistent with other backoffice save actions.

## Testing

- **C#:** extend the existing `Umbraco.Community.PagespeedOptimizer.BackOffice.Tests` project with a test covering `GetDefaultValues()` returning the new `Enabled` field correctly from `ImageOptimizationSettings`.
- **Client (TS/Lit):** no existing automated test harness was found for the Client project. Verification will be manual, using `test-sites/Website-V17/`: create/select an Image media item, confirm the view is hidden when `ImageOptimizationSettings.Enabled` is `false` or the media type isn't "Image", and exercise the create/update/delete flows through the UI.

## Out of Scope

- Making the media-type-alias condition reusable/configurable for other aliases (hardcoded to "Image" per requirements).
- Hooking the workspace view's save into Media's native Save button (confirmed not possible; dedicated Save button used instead).
- Automated client-side (TS/Lit) tests.
