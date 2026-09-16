# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a NuGet package for Umbraco CMS (v18.x, targeting .NET 10) that provides three page speed optimizations:

- **Static asset caching** — cache-control headers for browser caching
- **Image optimization** — quality/format conversion with WebP support
- **Response compression** — Gzip/Brotli via ASP.NET Core middleware

## Solution Structure

```
src/
├── code/
│   ├── Umbraco.Community.PagespeedOptimizer/          # Entry point, WebApplication extensions
│   ├── Umbraco.Community.PagespeedOptimizer.Core/     # Configuration POCOs
│   ├── Umbraco.Community.PagespeedOptimizer.Infrastructure/  # Composers, middleware, URL generators
│   └── Umbraco.Community.PagespeedOptimizer.BackOffice/      # Razor class library for back-office UI
├── test/
│   ├── Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/
│   └── Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/
└── Directory.Packages.props   # Central package version management
```

## Common Commands

```bash
# Restore
dotnet restore src/

# Build
dotnet build -c Release --no-restore src/

# Test with coverage
dotnet test -c Release --no-restore --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura src/

# Run a single test project
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/

# Pack NuGet
dotnet pack -c Release --no-restore --no-build src/
```

### Regenerating the OpenAPI document / TypeScript client

The management API's OpenAPI document is served at `/umbraco/openapi/pagespeed-optimizer-management-api.json`
(the old `/umbraco/swagger/...` URL is gone in Umbraco 18). To regenerate the committed client:

1. Boot `test-sites/Website-V18` (it must actually be running — the document is generated at runtime).
2. `curl -sk https://localhost:44371/umbraco/openapi/pagespeed-optimizer-management-api.json -o swagger.json` into `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`.
3. Strip the top-level `servers` block before committing — it carries the dev machine's own `https://localhost:44371/` into the generated `client.gen.ts` / `types.gen.ts` as a hard-coded `baseUrl` and would ship in the production bundle.
4. In `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/`, run `npm run generate-api` and rebuild with `npm run build`.

## Code Style

StyleCop.Analyzers is enforced with warnings-as-errors. Key rules from `src/stylecop.json`:

- `using` directives go **outside** the namespace (`usingDirectivesPlacement: outsideNamespace`; also enforced by `src/.editorconfig`'s `csharp_using_directive_placement = outside_namespace:warning`).
- XML documentation comments are required on **public and internal** members (`documentInternalElements: true`).
- All files must have a copyright header.
- Nullable reference types are enabled.

## Architecture Notes

- **`InfrastructureComposer`** is the Umbraco composer that wires up all three features via `IUmbracoBuilder` extensions in `UmbracoBuilderExtensions.cs`.
- **`WebApplicationExtensions`** exposes the `UsePageSpeedOptimizer()` extension called in `Program.cs` of the consuming site.
- **`StaticFileOptionsConfiguration`** configures cache headers on static files via `IPostConfigureOptions<StaticFileOptions>`.
- **`OptimizedImageUrlGenerator`** wraps Umbraco's `IImageUrlGenerator` to inject quality/format parameters (decorator pattern). Before falling back to global `ImageOptimizationSettings`, it consults `IMediaExceptionCache` for a per-media quality/WebP override; the cache is isolated-cache-backed and only ever rebuilt asynchronously (via `MediaExceptionCacheRefresher` plus startup/refresh notification handlers), never inline on the `GetImageUrl()` hot path.
- All features are independently toggled and configured via `appsettings.json` under the `Umbraco:Community:PageSpeedOptimizer` key.
- **`BackOffice` project** is a Razor class library (`Microsoft.NET.Sdk.Razor`) that will host back-office UI for managing the optimizer settings from within the Umbraco admin panel.
- **Media Exception Workspace View** — `pagespeedoptimizer.workspaceView.mediaException` (in `Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/`) shows an "Override image optimizations" toggle, quality slider, and Force WebP toggle on Media items of type "Image", backed by `MediaExceptionManagementApiController`. It is gated by three workspace conditions: `Umb.Workspace.Media`, the built-in `Umb.Condition.WorkspaceContentTypeAlias` (matching `Image`), and a custom `pagespeedoptimizer.condition.imageOptimizationEnabled` condition (in `src/conditions/image-optimization-enabled/`) that calls the `GetDefaultValues` endpoint to check `ImageOptimizationSettings.Enabled`. The view has its own Save button — Umbraco's workspace/save pipeline has no extension point for a `workspaceView` to hook into a host workspace's native save.

## Test Sites

`test-sites/Website-V18/` is a full Umbraco v18 site (Clean 8.0.1 starter kit) used for manual testing. It is in the solution but is not packed or published.

`test-sites/Website-V17/` is the previous Umbraco v17 site. It is deliberately **outside** the solution and references the published `Umbraco.Community.PagespeedOptimizer` 17.2.1 NuGet package rather than the local sources, which now target Umbraco 18. Keep it that way — adding it back to the solution will break `dotnet restore src/`.

## Releasing a new version

`src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/public/umbraco-package.json` carries its own
`version` field and a `?v=` cache-buster on the `entry-point.js` reference. It is copied into the nupkg as a
static web asset and is **not** driven by MSBuild versioning, so it must be bumped by hand on every release,
alongside `AssemblyVersion` / `VersionPrefix` / `InformationalVersion` in `src/Directory.Build.props`.

## Branch Strategy

Feature branches → `develop` → `main` (triggers NuGet release via GitHub Actions).

## Claude Code local setup

`.claude/settings.local.json` is gitignored and must be created manually by each developer. It grants Claude Code read/write access to the local source repositories for TrueLime packages and Umbraco itself, so Claude can browse their source code directly instead of relying on decompiled DLLs or web searches.

Create `.claude/settings.local.json` in the repo root with the paths that match where you have cloned these repositories locally:

```json
{
  "permissions": {
    "additionalDirectories": [
      "C:\\forks\\Umbraco-CMS-release-17.4.2\\Umbraco-CMS-release-17.4.2",      
    ]
  }
}
```

Adjust paths to match where you have cloned these repos. The file is already in `.gitignore`.
