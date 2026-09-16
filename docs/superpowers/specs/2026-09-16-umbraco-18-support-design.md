# Umbraco 18 Support — Design

**Date:** 2026-09-16
**Branch:** `feature/v18-support`
**Status:** Approved

## Goal

Upgrade `Umbraco.Community.PagespeedOptimizer` from Umbraco 17 to Umbraco 18, release it as
version `18.0.0`, and replace the V17 manual-test site with a new Umbraco 18 site carrying the
same package configuration.

The dominant breaking change is that Umbraco 18 replaces Swashbuckle with
`Microsoft.AspNetCore.OpenApi` for OpenAPI document generation.

## Decisions

| Decision | Choice |
| --- | --- |
| Umbraco 17 support | Ends at the released 17.2.1. This branch is v18-only; no multi-targeting. |
| V17 test site | Stays on disk as a standalone v17 reference site, removed from the solution. |
| V18 test site | `dotnet new umbraco` (18.1.1) + `Clean` 8.0.1 starter kit, mirroring the V17 site. |
| `swagger.json` | Regenerated from a running V18 site, not hand-edited. |
| Operation IDs | Accept Umbraco 18 defaults; delete the custom handler and update the TS call sites. |
| Umbraco version range | `[18.1.1,19.0.0)` for package references; test site pinned to 18.1.1. Raised from `[18.0.0,19.0.0)` because the backoffice client is built against `@umbraco-cms/backoffice` 18.1.1. |

## Verified Umbraco 18 API surface

Extracted from `Umbraco.Cms.Api.Common` / `Umbraco.Cms.Api.Management` 18.1.1:

- `IUmbracoBuilder.AddBackOfficeOpenApiDocument(string documentName, Action<BackOfficeOpenApiDocumentBuilder> configure)`
  — documented as: *"The document name. Matches the `[MapToApi]` value on controllers to include."*
- `BackOfficeOpenApiDocumentBuilder`: `WithTitle`, `WithUiTitle`, `ExcludeFromUi`,
  `ConfigureOpenApiOptions`, `WithJsonOptions`, `Build`
- `BackOfficeOpenApiDocumentBuilderExtensions.WithBackOfficeAuthentication()`
- `OpenApiOptionsExtensions.AddBackofficeSecurityRequirements(OpenApiOptions)`
- `UmbracoOperationIdTransformer`, `UmbracoSchemaIdGenerator`, `ConfigureUmbracoOpenApiOptionsBase.ShouldInclude`

**Removed in 18:** `IOperationIdHandler`, `OperationIdHandler`,
`BackOfficeSecurityRequirementsOperationFilterBase`.

**URL changes:** `/umbraco/swagger` → `/umbraco/openapi`;
`/umbraco/swagger/{documentName}/swagger.json` → `/umbraco/openapi/{documentName}.json`.

**Spec version:** generated documents are OpenAPI 3.1 (was 3.0.4).

Target framework is unchanged: Umbraco 18 is still `net10.0`.

## 1. Versioning

`src/Directory.Build.props` — set `AssemblyVersion`, `VersionPrefix`, and `InformationalVersion`
to `18.0.0`.

`src/Directory.Packages.props` — move these five ranges from `[17.0.0,18.0.0)` to `[18.1.1,19.0.0)`:

- `Umbraco.Cms.Web.Website`
- `Umbraco.Cms.Core`
- `Umbraco.Cms.Api.Management`
- `Umbraco.Cms.Imaging.ImageSharp`
- `Umbraco.Cms.Persistence.EFCore`

The floor is `18.1.1`, not `18.0.0`, because the backoffice client is built against
`@umbraco-cms/backoffice` `^18.1.1`. Allowing the package to install on Umbraco 18.0.0 would ship
a bundle type-checked against a newer backoffice than the host actually provides, so the C# floor
is raised to match the client's floor rather than lowering the client's.

Remove the `Swashbuckle.AspNetCore.SwaggerGen` `PackageVersion` entry. EF Core 10.0.6, StyleCop,
and the NUnit/Moq/coverlet test stack are unchanged.

## 2. Build configuration

`Umbraco.Community.PagespeedOptimizer.BackOffice.csproj`:

- Remove the `Swashbuckle.AspNetCore.SwaggerGen` `PackageReference`.
- Add `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>`,
  required for class libraries so the OpenAPI source generator compiles.
- If `Microsoft.AspNetCore.OpenApi` does not flow transitively from `Umbraco.Cms.Api.Management`
  in a way the source generator picks up, add it as an explicit `PackageReference` with a
  matching `PackageVersion` entry in `Directory.Packages.props`.

`RestorePackagesWithLockFile` is enabled, so every `packages.lock.json` in the solution is
regenerated and committed as part of this change.

## 3. OpenAPI migration

`src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs` is the only C#
file that changes. Its body reduces to approximately:

```csharp
builder.AddBackOfficeOpenApiDocument(
    Constants.ApiName,
    doc => doc
        .WithTitle("PageSpeed Optimizer Management API")
        .WithBackOfficeAuthentication());
```

Specifically:

- Delete the nested `OperationSecurityFilter` class; `.WithBackOfficeAuthentication()` replaces it.
- Delete the nested `CustomOperationHandler` class and the
  `builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>()` registration;
  the built-in `UmbracoOperationIdTransformer` names operations instead.
- Delete the `AddSwaggerGen` / `SwaggerDoc` call.
- Remove the now-unused `using` directives (`Asp.Versioning`,
  `Microsoft.AspNetCore.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Mvc.Controllers`,
  `Microsoft.Extensions.Options`, `Microsoft.OpenApi`). StyleCop enforces this.

`MediaExceptionManagementApiController` is unchanged:

- `[MapToApi(Constants.ApiName)]` stays — `AddBackOfficeOpenApiDocument` matches on it.
- `[ApiExplorerSettings(GroupName = "Page Speed Optimizer")]` stays — it drives the generated
  SDK class name `PageSpeedOptimizer`.

**Contingency:** if the generated document turns out to contain Umbraco's own endpoints rather
than only the five media-exception operations, add an explicit `ShouldInclude` filter via
`ConfigureOpenApiOptions`. Verification step 4 catches this.

## 4. Backoffice client

`src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client`:

1. `package.json`: bump `@umbraco-cms/backoffice` to `^18.1.1`. Add `@hey-api/openapi-ts` as an
   explicit pinned devDependency — it is currently invoked through bare `npx` with no version,
   so local and CI output can diverge.
2. Regenerate `package-lock.json`. CI runs `npm ci`, so the lock file must be committed and in
   sync with `package.json`.
3. With the V18 test site running, fetch
   `/umbraco/openapi/pagespeed-optimizer-management-api.json` and overwrite `swagger.json`.
   The result will declare `"openapi": "3.1.x"`.
4. Run `npm run generate-api` to regenerate `src/api/`.
5. The generated `PageSpeedOptimizer` class keeps its name, but its five method names
   (`createMediaException`, `updateMediaException`, `deleteMediaException`, `getByMediaKey`,
   `getDefaultValues`) may change under the default operation-ID transformer. Update the call
   sites in `src/workspaces/media-exception/element.ts` and
   `src/conditions/image-optimization-enabled/condition.ts` to match.
6. `npm run build` (`tsc && vite build`) must pass.

This is internal only — no public C# API and no HTTP route changes.

## 5. New test site: `test-sites/Website-V18`

Scaffolded with `dotnet new umbraco` targeting Umbraco 18.1.1, plus the `Clean` 8.0.1 starter kit
and `Umbraco.Cms.DevelopmentMode.Backoffice`. It mirrors the V17 site:

**`Website-V18.csproj`** — `net10.0`, `ImplicitUsings`/`Nullable` enabled,
`RootNamespace` `Website_V18`, `CompressionEnabled=false`,
`CopyRazorGenerateFilesToPublishDirectory=true`, `RazorCompileOnBuild=false`,
`RazorCompileOnPublish=false`, app-local ICU (`Microsoft.ICU.ICU4C.Runtime` 72.1.0.3 plus the
`RuntimeHostConfigurationOption`), `Microsoft.EntityFrameworkCore.Design` 10.0.6 with
`PrivateAssets=all`, and a `ProjectReference` to
`src/code/Umbraco.Community.PagespeedOptimizer/Umbraco.Community.PagespeedOptimizer.csproj`.

**`Program.cs`** — copied verbatim from the V17 site, including `app.EnableResponseCompression()`
placed before `await app.BootUmbracoAsync()`.

**`appsettings.json`** — copied from the V17 site, with a newly generated `Umbraco:CMS:Global:Id`
GUID, plus one deliberate divergence from V17: the `Umbraco:CMS:Imaging:HMACSecretKey` block that
the Umbraco 18 project template generates is **kept**. It enables signed image URLs, an Umbraco 18
default that many production sites will run with. Because `OptimizedImageUrlGenerator` mutates
`ImageUrlGenerationOptions` and then delegates to Umbraco's own generator, the signature is computed
over the final URL including the package's injected quality and format parameters — so the test site
exercises signed-URL compatibility rather than avoiding it. The package configuration block is
carried over unchanged:

```json
"Umbraco": {
  "Community": {
    "PageSpeedOptimizer": {
      "StaticAssetsCache": { "Enabled": true, "CacheBackOffice": true },
      "ResponseCompression": { "Enabled": true },
      "ImageOptimization": { "Enabled": true, "DefaultImageQuality": 70, "ForceWebP": true }
    }
  }
}
```

The `Umbraco:CMS` block (Global, Content, Unattended, Security) is carried over as-is.

**`.gitignore`** — same as the V17 site, so the SQLite database and generated `umbraco/` runtime
folders stay untracked.

**Solution** — add `../test-sites/Website-V18/Website-V18.csproj` to
`src/Umbraco.Community.PagespeedOptimizer.slnx`, replacing the `Website-V17` entry.

## 6. Old test site: `test-sites/Website-V17`

- Removed from `src/Umbraco.Community.PagespeedOptimizer.slnx`.
- Its `ProjectReference` to the package is replaced by a
  `<PackageReference Include="Umbraco.Community.PagespeedOptimizer" Version="17.2.1" />`, so the
  folder stays a working standalone Umbraco 17 reference site instead of a broken project
  pointing at v18-only sources.
- `CLAUDE.md` gains a short note recording that the folder is intentionally outside the solution
  and pinned to the last v17 package release.

## 7. Tests and CI

The existing test suites do not reference Swagger or OpenAPI, so they should compile and pass
against Umbraco 18 unchanged:

- `Umbraco.Community.PagespeedOptimizer.BackOffice.Tests` — `MediaExceptionManagementApiControllerTests`
- `Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests` — caching, EF/DbContext, image URL
  generator, static file options, `InfrastructureComposerTests`

`InfrastructureComposerTests` exercises DI registration and is the most likely to surface any
Umbraco 18 API shift; treat it as the canary.

No new unit tests are added for the composer change. The meaningful assertion is the shape of
the generated OpenAPI document, which is covered by verification step 4 rather than by a unit
test — asserting against a mocked `IServiceCollection` would not catch a wrongly scoped document.

CI workflows (`pr-validation.yml`, `beta-release.yml`, `release.yml`) need no structural change:
they already use node 24.13.1 and dotnet 10.x, and the npm/restore/build/test/pack sequence is
unchanged.

## 8. Verification

Automated:

1. `dotnet restore src/`, `dotnet build -c Release --no-restore src/`, and
   `dotnet test -c Release --no-restore --no-build src/` all succeed, with no StyleCop or
   nullable warnings (both are warnings-as-errors).
2. `npm ci && npm run build` succeeds in
   `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client`.
3. `dotnet pack -c Release src/` produces `Umbraco.Community.PagespeedOptimizer.18.0.0.nupkg`.

Requires a running site:

4. Website-V18 boots; `/umbraco/openapi` lists the "PageSpeed Optimizer Management API"
   document; `/umbraco/openapi/pagespeed-optimizer-management-api.json` contains exactly the five
   media-exception operations and no Umbraco core endpoints.

Manual (browser, requires uploading a media item):

5. The media-exception workspace view renders on an Image media item, loads default values,
   saves an override, and the override changes the delivered image URL and format on the front
   end. Static asset cache headers and response compression are also confirmed in the browser's
   network tab.

Step 5 is explicitly manual and is not automated as part of this work.

## Out of scope

- Umbraco 17 back-ports or multi-targeting.
- Any change to the package's public C# API, HTTP routes, configuration schema, or database
  schema.
- Migrating to Swashbuckle-on-the-side (Umbraco 18 still permits it, but the package has no
  reason to carry that dependency).
- Refactoring unrelated to the upgrade.
