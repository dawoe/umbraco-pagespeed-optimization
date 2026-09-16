# Umbraco 18 Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade `Umbraco.Community.PagespeedOptimizer` to Umbraco 18, release it as version `18.0.0`, and replace the V17 manual-test site with a new Umbraco 18 site carrying the same package configuration.

**Architecture:** The package is a four-project .NET class library set plus a Lit/Vite backoffice client. The only C# behaviour change is in `BackOfficeComposer.cs`, where Swashbuckle's `AddSwaggerGen` is replaced by Umbraco 18's `AddBackOfficeOpenApiDocument`. Everything else is dependency retargeting, a new test site, and regenerating the TypeScript API client from the new OpenAPI 3.1 document.

**Tech Stack:** .NET 10 / C#, Umbraco CMS 18.1.1, `Microsoft.AspNetCore.OpenApi`, EF Core 10, NUnit + Moq, Lit 3 + Vite 8, `@hey-api/openapi-ts`, `@umbraco-cms/backoffice` 18.

**Spec:** [`docs/superpowers/specs/2026-09-16-umbraco-18-support-design.md`](../specs/2026-09-16-umbraco-18-support-design.md)

## Global Constraints

- Target framework stays `net10.0` everywhere. Do not change it.
- Umbraco package version range is exactly `[18.1.1,19.0.0)`. The V18 test site pins `18.1.1`. The floor was raised from `[18.0.0,19.0.0)` because the backoffice client is built against `@umbraco-cms/backoffice` `^18.1.1`.
- Package version is exactly `18.0.0` (`AssemblyVersion`, `VersionPrefix`, `InformationalVersion`).
- StyleCop.Analyzers runs with warnings-as-errors. `Nullable` is also warnings-as-errors.
- StyleCop config (`src/stylecop.json`) sets `usingDirectivesPlacement: outsideNamespace` — `using` directives go **outside** the namespace. (Note: `CLAUDE.md` currently states the opposite; the JSON is authoritative.)
- Every C# file needs the exact copyright header: `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`
- XML documentation comments are required on all members, including internal ones (`documentInternalElements: true`).
- Central Package Management is on. Versions go in `src/Directory.Packages.props`, never inline in a `.csproj` inside `src/`. (Test sites under `test-sites/` are outside this and use inline versions.)
- `RestorePackagesWithLockFile` is enabled. All six `packages.lock.json` files are regenerated and committed whenever dependencies change.
- Do not change the package's public C# API, HTTP routes, configuration schema, or database schema.
- Do not add Swashbuckle back in any form.

## File Structure

**Modified:**
- `src/Umbraco.Community.PagespeedOptimizer.slnx` — swap the V17 test-site entry for V18
- `src/Directory.Build.props` — version `17.2.1` → `18.0.0`
- `src/Directory.Packages.props` — Umbraco ranges to `[18.0.0,19.0.0)`, drop Swashbuckle
- `src/code/…BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj` — drop Swashbuckle, add `InterceptorsNamespaces`
- `src/code/…BackOffice/BackOfficeComposer.cs` — the OpenAPI migration
- `src/code/…BackOffice.Client/package.json` + `package-lock.json` — backoffice 18, pinned `openapi-ts`
- `src/code/…BackOffice.Client/swagger.json` — regenerated as OpenAPI 3.1
- `src/code/…BackOffice.Client/src/api/**` — regenerated
- `src/code/…BackOffice.Client/src/workspaces/media-exception/element.ts` — SDK call sites
- `src/code/…BackOffice.Client/src/conditions/image-optimization-enabled/condition.ts` — SDK call site
- `test-sites/Website-V17/Website-V17.csproj` — `ProjectReference` → `PackageReference` 17.2.1
- `CLAUDE.md` — record the V17 site's new status
- All six `packages.lock.json` files

**Created:**
- `test-sites/Website-V18/**` — new Umbraco 18 + Clean 8.0.1 site

**Unchanged (deliberately):** every file under `src/code/…Core`, `src/code/…Infrastructure`, `src/code/…PagespeedOptimizer`, both test projects, `MediaExceptionManagementApiController.cs`, `Constants.cs`, and all three CI workflows.

---

## Task 1: Detach the V17 test site from the solution

The V17 site has a `ProjectReference` to the package. Once the package requires Umbraco 18, NuGet cannot resolve a solution that also contains a site pinned to Umbraco 17.0.1. This must happen **before** Task 2 or `dotnet restore src/` will fail.

**Files:**
- Modify: `src/Umbraco.Community.PagespeedOptimizer.slnx:16`
- Modify: `test-sites/Website-V17/Website-V17.csproj:21`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a solution containing only the four `src/code` projects and the two `src/test` projects.

- [ ] **Step 1: Verify the current state builds, so a later failure is attributable**

```bash
dotnet restore src/ && dotnet build -c Release --no-restore src/
```

Expected: `Build succeeded`. If this already fails, stop and report — nothing in this plan caused it.

- [ ] **Step 2: Remove the V17 project from the solution**

In `src/Umbraco.Community.PagespeedOptimizer.slnx`, delete this line:

```xml
  <Project Path="../test-sites/Website-V17/Website-V17.csproj" />
```

Leave the `<Configurations>`, `<Folder Name="/_BuildProps/">` blocks and the six remaining `<Project>` entries untouched.

- [ ] **Step 3: Repoint the V17 site at the published 17.2.1 package**

In `test-sites/Website-V17/Website-V17.csproj`, replace this line:

```xml
    <ProjectReference Include="..\..\src\code\Umbraco.Community.PagespeedOptimizer\Umbraco.Community.PagespeedOptimizer.csproj" />
```

with:

```xml
    <PackageReference Include="Umbraco.Community.PagespeedOptimizer" Version="17.2.1" />
```

Keep it inside the same `<ItemGroup>` that holds `Microsoft.ICU.ICU4C.Runtime` and the `RuntimeHostConfigurationOption`.

- [ ] **Step 4: Verify the V17 site still restores on its own**

```bash
dotnet restore test-sites/Website-V17/Website-V17.csproj
```

Expected: `Restored …Website-V17.csproj`. No `NU1605`/`NU1107` version-conflict errors.

- [ ] **Step 5: Verify the solution still builds without it**

```bash
dotnet restore src/ && dotnet build -c Release --no-restore src/
```

Expected: `Build succeeded`, and the build output no longer mentions `Website-V17`.

- [ ] **Step 6: Record the V17 site's status in CLAUDE.md**

In `CLAUDE.md`, replace the `## Test Site` section:

```markdown
## Test Site

`test-sites/Website-V17/` is a full Umbraco v17 site used for manual testing. It is not part of the solution build.
```

with:

```markdown
## Test Sites

`test-sites/Website-V18/` is a full Umbraco v18 site (Clean 8.0.1 starter kit) used for manual testing. It is in the solution but is not packed or published.

`test-sites/Website-V17/` is the previous Umbraco v17 site. It is deliberately **outside** the solution and references the published `Umbraco.Community.PagespeedOptimizer` 17.2.1 NuGet package rather than the local sources, which now target Umbraco 18. Keep it that way — adding it back to the solution will break `dotnet restore src/`.
```

Note: the `Website-V18` sentence describes the site created in Task 3. Writing it now keeps CLAUDE.md edits in one place; the directory appears two tasks later.

- [ ] **Step 7: Commit**

```bash
git add src/Umbraco.Community.PagespeedOptimizer.slnx test-sites/Website-V17/Website-V17.csproj CLAUDE.md
git commit -m "Detach Website-V17 from the solution ahead of the Umbraco 18 retarget

The V17 site pins Umbraco 17.0.1 and cannot coexist in one restore graph
with a package that requires Umbraco 18. It now references the published
17.2.1 package so it remains a working standalone v17 reference site.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Retarget to Umbraco 18 and migrate BackOfficeComposer off Swashbuckle

These changes are inseparable: removing the Swashbuckle package reference breaks `BackOfficeComposer.cs` at compile time, and the new `AddBackOfficeOpenApiDocument` API does not exist in Umbraco 17. The deliverable is a solution that builds and whose existing test suite passes against Umbraco 18.

**Files:**
- Modify: `src/Directory.Build.props:44-46`
- Modify: `src/Directory.Packages.props:8-15`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs` (full rewrite)
- Regenerate: all six `packages.lock.json` files

**Interfaces:**
- Consumes: `Constants.ApiName` (`"pagespeed-optimizer-management-api"`) from `src/code/…BackOffice/Constants.cs`, unchanged.
- Produces: an OpenAPI document named `pagespeed-optimizer-management-api`, served at `/umbraco/openapi/pagespeed-optimizer-management-api.json`, containing exactly the five endpoints of `MediaExceptionManagementApiController`. Task 4 consumes that document.

**Umbraco 18 API reference** (verified against `Umbraco.Cms.Api.Common` / `Umbraco.Cms.Api.Management` 18.1.1 XML docs):

- `Umbraco.Cms.Api.Common.OpenApi.UmbracoBuilderOpenApiExtensions.AddBackOfficeOpenApiDocument(IUmbracoBuilder, string documentName, Action<BackOfficeOpenApiDocumentBuilder> configure)` → returns `IUmbracoBuilder`
- `Umbraco.Cms.Api.Common.OpenApi.BackOfficeOpenApiDocumentBuilder.WithTitle(string)` → returns the builder
- `Umbraco.Cms.Api.Management.OpenApi.BackOfficeOpenApiDocumentBuilderExtensions.WithBackOfficeAuthentication(BackOfficeOpenApiDocumentBuilder)` → returns the builder

`AddBackOfficeOpenApiDocument` already applies these defaults, so none of them need to be written by hand:
filtering endpoints by `[MapToApi(documentName)]`; schema reference IDs via `UmbracoSchemaIdGenerator`; operation IDs via `UmbracoOperationIdTransformer`; tagging operations by the controller's API group name; sorting tags and paths for stable output; stripping redundant JSON-equivalent media types.

- [ ] **Step 1: Bump the package version**

In `src/Directory.Build.props`, change these three lines:

```xml
      <AssemblyVersion>17.2.1</AssemblyVersion>
      <VersionPrefix>17.2.1</VersionPrefix>
      <InformationalVersion>17.2.1</InformationalVersion>
```

to:

```xml
      <AssemblyVersion>18.0.0</AssemblyVersion>
      <VersionPrefix>18.0.0</VersionPrefix>
      <InformationalVersion>18.0.0</InformationalVersion>
```

- [ ] **Step 2: Retarget the Umbraco package ranges and drop Swashbuckle**

In `src/Directory.Packages.props`, replace the first `<ItemGroup>`:

```xml
  <ItemGroup>
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Api.Management" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Imaging.ImageSharp" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Persistence.EFCore" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerGen" Version="10.1.7" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup>
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[18.1.1,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="[18.1.1,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Api.Management" Version="[18.1.1,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Imaging.ImageSharp" Version="[18.1.1,19.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Persistence.EFCore" Version="[18.1.1,19.0.0)" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
  </ItemGroup>
```

The floor is `18.1.1`, not `18.0.0`, because the backoffice client is built against
`@umbraco-cms/backoffice` `^18.1.1`; the C# floor is raised to match rather than shipping a
bundle type-checked against a newer Umbraco than the package claims to support.

Leave the StyleCop and test-stack `<ItemGroup>`s exactly as they are.

- [ ] **Step 3: Update the BackOffice csproj**

Replace the whole of `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
    <!-- Required for class libraries so the Microsoft.AspNetCore.OpenApi source generator compiles. -->
    <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
  </PropertyGroup>

  <ItemGroup>
    <Folder Include="wwwroot\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Umbraco.Cms.Api.Management" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Umbraco.Community.PagespeedOptimizer.Core\Umbraco.Community.PagespeedOptimizer.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Rewrite BackOfficeComposer.cs**

Replace the whole of `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs` with:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice;

/// <summary>
/// Composer for the Page Speed Optimizer back-office, registering the management API OpenAPI document.
/// </summary>
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddBackOfficeOpenApiDocument(
            Constants.ApiName,
            doc => doc
                .WithTitle("PageSpeed Optimizer Management API")
                .WithBackOfficeAuthentication());
}
```

This deletes the nested `OperationSecurityFilter` class (replaced by `WithBackOfficeAuthentication()`), the nested `CustomOperationHandler` class and its `AddSingleton<IOperationIdHandler, …>` registration (both types no longer exist in Umbraco 18), and the `AddSwaggerGen` / `SwaggerDoc` call. The `using` directives for `Asp.Versioning`, `Microsoft.AspNetCore.Mvc.ApiExplorer`, `Microsoft.AspNetCore.Mvc.Controllers`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, and `Microsoft.OpenApi` all go with them.

Do **not** touch `MediaExceptionManagementApiController.cs`: `[MapToApi(Constants.ApiName)]` is how `AddBackOfficeOpenApiDocument` selects endpoints, and `[ApiExplorerSettings(GroupName = "Page Speed Optimizer")]` drives the generated SDK class name.

- [ ] **Step 5: Regenerate the lock files**

```bash
dotnet restore src/ --force-evaluate
```

Expected: `Restored` for all six projects, and `git status` shows all six `packages.lock.json` files modified. If restore fails with `NU1605` or an unresolvable `Umbraco.Cms.*`, confirm Task 1 landed — a stray `Website-V17` entry in the `.slnx` is the usual cause.

- [ ] **Step 6: Build and read the errors carefully**

```bash
dotnet build -c Release --no-restore src/
```

Expected: `Build succeeded` with 0 warnings.

Two failures are plausible here and are handled, not worked around:

1. **`CS0246: The type or namespace name 'OpenApiOptions' / interceptor errors`** — `Microsoft.AspNetCore.OpenApi` is not flowing transitively in a form the source generator sees. Fix: add `<PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.6" />` to `src/Directory.Packages.props` and `<PackageReference Include="Microsoft.AspNetCore.OpenApi" />` to the BackOffice csproj's package `ItemGroup`, then re-run Steps 5–6.
2. **StyleCop `SA1200`/`SA1633`/`SA1600`** — re-check the file against the Global Constraints (header text exact, usings outside the namespace, XML docs on the class and the method).

- [ ] **Step 7: Run the full test suite**

```bash
dotnet test -c Release --no-restore --no-build src/
```

Expected: all tests pass across `Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests` and `Umbraco.Community.PagespeedOptimizer.BackOffice.Tests`.

`InfrastructureComposerTests` is the canary — it exercises DI registration and is the test most likely to surface an Umbraco 18 API shift. If it fails, read the assertion before changing anything: a genuine Umbraco 18 behaviour change needs the test updated to match, not the production code bent to satisfy an Umbraco 17 expectation. Report any such change rather than silently absorbing it.

- [ ] **Step 8: Confirm Swashbuckle is fully gone**

```bash
grep -rn "Swashbuckle\|AddSwaggerGen\|SwaggerDoc" --include="*.cs" --include="*.csproj" --include="*.props" src/ | grep -v "/obj/" | grep -v "/bin/" | grep -v node_modules
```

Expected: no output.

Do **not** widen this to `--include="*.json"`. The regenerated `packages.lock.json` files legitimately contain `Swashbuckle.AspNetCore.SwaggerUI` as a `"type": "Transitive"` dependency of Umbraco's own `Umbraco.Cms.Core` / `Umbraco.Cms.Web.Website` packages — Umbraco 18 still uses Swashbuckle's UI to render its OpenAPI page. That is Umbraco's dependency, not ours, and cannot be removed from this repo. The constraint is that *this package* adds no Swashbuckle reference, which the command above checks.

- [ ] **Step 9: Commit**

```bash
git add src/Directory.Build.props src/Directory.Packages.props src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/ src/code/*/packages.lock.json src/test/*/packages.lock.json
git commit -m "Retarget to Umbraco 18 and migrate OpenAPI off Swashbuckle

Umbraco package ranges move to [18.0.0,19.0.0) and the package version
becomes 18.0.0. BackOfficeComposer now uses AddBackOfficeOpenApiDocument
with WithBackOfficeAuthentication, replacing AddSwaggerGen, the
BackOfficeSecurityRequirementsOperationFilterBase subclass, and the
custom IOperationIdHandler - none of which exist in Umbraco 18.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Create the Website-V18 test site

**Files:**
- Create: `test-sites/Website-V18/Website-V18.csproj`
- Create: `test-sites/Website-V18/Program.cs`
- Create: `test-sites/Website-V18/appsettings.json`
- Create: `test-sites/Website-V18/appsettings.Development.json`
- Create: `test-sites/Website-V18/Properties/launchSettings.json`
- Create: `test-sites/Website-V18/.gitignore`
- Create: `test-sites/Website-V18/Views/**`, `wwwroot/**` (generated by the Clean starter kit on first build)
- Modify: `src/Umbraco.Community.PagespeedOptimizer.slnx`

**Interfaces:**
- Consumes: `Umbraco.Community.PagespeedOptimizer` via `ProjectReference`; `EnableResponseCompression()` from `Umbraco.Community.PagespeedOptimizer.Extensions.WebApplicationExtensions`.
- Produces: a bootable site at `https://localhost:44371` serving `/umbraco/openapi/pagespeed-optimizer-management-api.json`. Task 4 consumes that URL.

- [ ] **Step 1: Install the Umbraco 18 templates**

```bash
dotnet new install Umbraco.Templates::18.1.1
```

Expected: `Success: Umbraco.Templates::18.1.1 installed the following templates:` followed by a table including `umbraco`.

- [ ] **Step 2: Scaffold the site**

```bash
dotnet new umbraco --name Website-V18 --output test-sites/Website-V18 --force
```

Expected: `The template "Umbraco Project" was created successfully.`

- [ ] **Step 3: Add the Clean starter kit**

```bash
dotnet add test-sites/Website-V18/Website-V18.csproj package clean --version 8.0.1
dotnet add test-sites/Website-V18/Website-V18.csproj package Umbraco.Cms.DevelopmentMode.Backoffice --version 18.1.1
```

Expected: `info : PackageReference for package 'clean' version '8.0.1' added` and the same for the backoffice package.

- [ ] **Step 4: Replace the csproj with the V17-matching version**

Replace the whole of `test-sites/Website-V18/Website-V18.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Website_V18</RootNamespace>
    <CompressionEnabled>false</CompressionEnabled> <!-- Disable compression. E.g. for umbraco backoffice files. These files should be precompressed by node and not let dotnet handle it -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="clean" Version="8.0.1" />
    <PackageReference Include="Umbraco.Cms" Version="18.1.1" />
    <PackageReference Include="Umbraco.Cms.DevelopmentMode.Backoffice" Version="18.1.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <!-- Opt-in to app-local ICU to ensure consistent globalization APIs across different platforms -->
    <PackageReference Include="Microsoft.ICU.ICU4C.Runtime" Version="72.1.0.3" />
    <ProjectReference Include="..\..\src\code\Umbraco.Community.PagespeedOptimizer\Umbraco.Community.PagespeedOptimizer.csproj" />
    <RuntimeHostConfigurationOption Include="System.Globalization.AppLocalIcu" Value="72.1.0.3" Condition="$(RuntimeIdentifier.StartsWith('linux')) or $(RuntimeIdentifier.StartsWith('win')) or ('$(RuntimeIdentifier)' == '' and !$([MSBuild]::IsOSPlatform('osx')))" />
  </ItemGroup>

  <PropertyGroup>
    <!-- Razor files are needed for the backoffice to work correctly -->
    <CopyRazorGenerateFilesToPublishDirectory>true</CopyRazorGenerateFilesToPublishDirectory>
  </PropertyGroup>

  <PropertyGroup>
    <!-- Remove RazorCompileOnBuild and RazorCompileOnPublish when not using ModelsMode InMemoryAuto -->
    <RazorCompileOnBuild>false</RazorCompileOnBuild>
    <RazorCompileOnPublish>false</RazorCompileOnPublish>
  </PropertyGroup>

</Project>
```

Note there is no `RestorePackagesWithLockFile` here — test sites sit outside `src/` and so outside `src/Directory.Build.props`. Do not add a lock file to this project.

- [ ] **Step 5: Replace Program.cs**

Replace the whole of `test-sites/Website-V18/Program.cs` with the V17 site's version verbatim:

```csharp
using Umbraco.Community.PagespeedOptimizer.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();
app.EnableResponseCompression();

await app.BootUmbracoAsync();


app.UseUmbraco()
	.WithMiddleware(u =>
	{
		u.UseBackOffice();
		u.UseWebsite();
	})
	.WithEndpoints(u =>
	{
		u.UseBackOfficeEndpoints();
		u.UseWebsiteEndpoints();
	});

await app.RunAsync();
```

The `EnableResponseCompression()` call must stay before `BootUmbracoAsync()`, matching the V17 site.

If the Umbraco 18 template's generated `Program.cs` differs structurally from this (for example a renamed builder extension), keep the template's structure and add only the `using Umbraco.Community.PagespeedOptimizer.Extensions;` line and the `app.EnableResponseCompression();` call in the equivalent position. Report the difference.

- [ ] **Step 6: Write appsettings.json**

Replace the whole of `test-sites/Website-V18/appsettings.json` with the V17 configuration, using a freshly generated GUID for `Global:Id` (generate one with `[guid]::NewGuid()` in PowerShell or `uuidgen`; do not reuse the value below, it is the V17 site's):

```json
{
  "$schema": "appsettings-schema.json",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    }
  },
  "Umbraco": {
    "CMS": {
        "Global": {
            "Id": "REPLACE-WITH-A-NEWLY-GENERATED-GUID",
            "SanitizeTinyMce": true
        },
        "Content": {
            "AllowEditInvariantFromNonDefault": true,
            "ContentVersionCleanupPolicy": {
                "EnableCleanup": true
            }
        },
        "Unattended": {
            "UpgradeUnattended": true
        },
        "Security": {
            "AllowConcurrentLogins": false
        }
    },
    "Community": {
        "PageSpeedOptimizer": {
            "StaticAssetsCache": {
                "Enabled": true,
                "CacheBackOffice": true
            },
            "ResponseCompression": {
                "Enabled": true
            },
            "ImageOptimization": {
                "Enabled": true,
                "DefaultImageQuality": 70,
                "ForceWebP": true
            }
        }
    }
  }
}
```

`REPLACE-WITH-A-NEWLY-GENERATED-GUID` is the one value you fill in — everything else is copied verbatim.

- [ ] **Step 7: Write appsettings.Development.json**

Replace the whole of `test-sites/Website-V18/appsettings.Development.json` with:

```json
{
  "$schema": "appsettings-schema.json",
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      {
        "Name": "Async",
        "Args": {
          "configure": [
            {
              "Name": "Console"
            }
          ]
        }
      }
    ]
  },
  "ConnectionStrings": {
    "umbracoDbDSN": "Data Source=|DataDirectory|/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True",
    "umbracoDbDSN_ProviderName": "Microsoft.Data.Sqlite"
  },
  "Umbraco": {
    "CMS": {
      "Unattended": {
        "InstallUnattended": true,
        "UnattendedUserName": "admin@example.com",
        "UnattendedUserEmail": "admin@example.com",
        "UnattendedUserPassword": "1234567890"
      },
      "Content": {
        "MacroErrors": "Throw"
      },
      "Hosting": {
        "Debug": true
      },
      "RuntimeMinification": {
        "UseInMemoryCache": true,
        "CacheBuster": "Timestamp"
      }
    }
  }
}
```

Unattended install is what makes the site boot straight into a working backoffice with Clean's content and media, which Task 5 needs.

- [ ] **Step 8: Write launchSettings.json with ports that don't clash with V17**

Replace the whole of `test-sites/Website-V18/Properties/launchSettings.json` with:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:43682",
      "sslPort": 44371
    }
  },
  "profiles": {
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "Website-V18": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:44371;http://localhost:43682",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

The V17 site uses `44370`/`43681`, so both sites can run side by side.

- [ ] **Step 9: Copy the V17 site's .gitignore**

```bash
cp test-sites/Website-V17/.gitignore test-sites/Website-V18/.gitignore
```

This keeps the SQLite database, `umbraco/Logs/`, `umbraco/Data/TEMP/`, `wwwroot/media/`, and the generated `appsettings-schema*.json` files untracked, exactly as for V17.

- [ ] **Step 10: Add the site to the solution**

In `src/Umbraco.Community.PagespeedOptimizer.slnx`, add this line where the `Website-V17` entry used to be — immediately after the `</Folder>` closing tag for `/_BuildProps/test/` and before the `BackOffice` project entry:

```xml
  <Project Path="../test-sites/Website-V18/Website-V18.csproj" />
```

- [ ] **Step 11: Build the solution including the new site**

```bash
dotnet restore src/ && dotnet build -c Release --no-restore src/
```

Expected: `Build succeeded`, with `Website-V18` among the built projects.

If restore reports a version conflict between `clean` 8.0.1's `Umbraco.Cms.Web.Website` 18.0.1 dependency and the site's `Umbraco.Cms` 18.1.1, that is a normal NuGet unification and resolves upward to 18.1.1 — only act if it is a hard `NU1107`, in which case drop the site's `Umbraco.Cms` pin to `18.0.1` and report it.

- [ ] **Step 12: Boot the site and confirm the OpenAPI document**

```bash
dotnet run --project test-sites/Website-V18/Website-V18.csproj --launch-profile Website-V18
```

Wait for `Now listening on: https://localhost:44371` and for the unattended install to finish (`Umbraco CMS has been installed`). Then, in another shell:

```bash
curl -sk https://localhost:44371/umbraco/openapi/pagespeed-optimizer-management-api.json | head -20
```

Expected: JSON beginning with `"openapi": "3.1` and an `"info"` block whose `"title"` is `PageSpeed Optimizer Management API`.

If this 404s, the document name does not match — check `Constants.ApiName` against the URL. If it returns Umbraco core endpoints as well as the five media-exception ones, apply the spec's contingency: add a `ShouldInclude` filter via `ConfigureOpenApiOptions` in `BackOfficeComposer.cs` and report it.

Leave the site running — Task 4 Step 3 needs it.

- [ ] **Step 13: Commit**

```bash
git add test-sites/Website-V18 src/Umbraco.Community.PagespeedOptimizer.slnx
git commit -m "Add Umbraco 18 test site

Website-V18 is a Clean 8.0.1 starter-kit site on Umbraco 18.1.1,
mirroring the V17 site's project layout, Program.cs and appsettings
including the full PageSpeedOptimizer configuration block. It listens on
44371/43682 so it can run alongside the V17 site.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

Check `git status` before committing: the Clean starter kit generates `Views/` and `wwwroot/` content on first build. Commit those (the V17 site has them committed too), but confirm nothing under `umbraco/Data/` or `wwwroot/media/` slipped in — if it did, the `.gitignore` copy in Step 9 failed.

---

## Task 4: Regenerate the OpenAPI document and the TypeScript client

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/package.json`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/package-lock.json`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/swagger.json`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/api/**` (regenerated)
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/workspaces/media-exception/element.ts:62,63,112,121,133`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src/conditions/image-optimization-enabled/condition.ts:18`

**Interfaces:**
- Consumes: `/umbraco/openapi/pagespeed-optimizer-management-api.json` from the Task 3 site.
- Produces: the generated class `PageSpeedOptimizer` exported from `src/api`, whose static methods back the workspace view and the condition.

**Current SDK call sites** (these are the five names that may change):

| File | Line | Call |
| --- | --- | --- |
| `element.ts` | 62 | `PageSpeedOptimizer.getByMediaKey({ path: { mediaKey } })` |
| `element.ts` | 63 | `PageSpeedOptimizer.getDefaultValues()` |
| `element.ts` | 112 | `PageSpeedOptimizer.updateMediaException({ … })` |
| `element.ts` | 121 | `PageSpeedOptimizer.createMediaException({ … })` |
| `element.ts` | 133 | `PageSpeedOptimizer.deleteMediaException({ path: { id: this.#existingId } })` |
| `condition.ts` | 18 | `PageSpeedOptimizer.getDefaultValues()` |

- [ ] **Step 1: Record the current operation IDs for comparison**

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
grep -o '"operationId": "[^"]*"' swagger.json | sort
```

Expected: five lines — `CreateMediaException`, `DeleteMediaException`, `GetByMediaKey`, `GetDefaultValues`, `UpdateMediaException`. Keep this output; Step 5 diffs against it.

- [ ] **Step 2: Bump the npm dependencies**

In `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/package.json`, replace the `devDependencies` block:

```json
  "devDependencies": {
    "@umbraco-cms/backoffice": "^17.0.0",
    "typescript": "~6.0.2",
    "vite": "^8.1.1"
  }
```

with:

```json
  "devDependencies": {
    "@hey-api/openapi-ts": "0.99.0",
    "@umbraco-cms/backoffice": "^18.1.1",
    "typescript": "~6.0.2",
    "vite": "^8.1.1"
  }
```

`@hey-api/openapi-ts` is pinned exactly (no caret) because the `generate-api` script currently calls bare `npx openapi-ts` with no version, so local and CI runs can produce different output from the same spec.

Then regenerate the lock file:

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
npm install
```

Expected: `package-lock.json` updated, `node_modules/@umbraco-cms/backoffice/package.json` reporting an 18.x version.

- [ ] **Step 3: Fetch the new OpenAPI document**

With the Task 3 site running:

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
curl -sk https://localhost:44371/umbraco/openapi/pagespeed-optimizer-management-api.json -o swagger.json
```

Verify it:

```bash
head -c 200 swagger.json
grep -c '"operationId"' swagger.json
```

Expected: the file starts with `{"openapi":"3.1` (or a pretty-printed equivalent), and the operationId count is exactly `5`. If the count is higher, the document is picking up Umbraco core endpoints — go back to Task 3 Step 12's contingency.

If the fetched JSON is minified, pretty-print it so the committed file stays diffable:

```bash
node -e "const f='swagger.json';const j=JSON.parse(require('fs').readFileSync(f,'utf8'));require('fs').writeFileSync(f,JSON.stringify(j,null,2)+'\n')"
```

Before committing, strip the top-level `servers` block. Umbraco bakes the dev machine's own
`https://localhost:<port>/` into it, and that absolute URL would otherwise flow into the
generated `client.gen.ts` / `types.gen.ts` as a hard-coded `baseUrl` and ship in the production
bundle. It is overridden at runtime by `umbHttpClient.getConfig()` in `src/hey-api.ts`, but the
committed spec should not depend on that ordering:

```bash
node -e "const f='swagger.json';const j=JSON.parse(require('fs').readFileSync(f,'utf8'));delete j.servers;require('fs').writeFileSync(f,JSON.stringify(j,null,2)+'\n')"
```

- [ ] **Step 4: Regenerate the API client**

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
npm run generate-api
```

Expected: files under `src/api/` rewritten. `openapi-ts.config.ts` needs no change — it already reads `swagger.json`, writes to `src/api`, and uses the `@hey-api/client-fetch` + `asClass: true` SDK plugins.

If the generator errors on OpenAPI 3.1 specifically, that is a real incompatibility — report it rather than downgrading the spec. `@hey-api/openapi-ts` 0.99.0 supports 3.1.

- [ ] **Step 5: Diff the generated method names**

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
grep -n "^export class" src/api/sdk.gen.ts
grep -o "public static [a-zA-Z]*" src/api/sdk.gen.ts
```

Expected: `export class PageSpeedOptimizer {` (unchanged — it derives from the controller's `[ApiExplorerSettings(GroupName = "Page Speed Optimizer")]`), followed by five `public static` method names.

Compare those five names against the Step 1 list. If they are identical, Step 6 is a no-op — skip to Step 7. If they differ, note the old → new mapping; Step 6 applies it.

If the class name changed, do **not** rename it back by editing generated code — update the imports in `element.ts` and `condition.ts` instead, since `src/api/` is regenerated output.

- [ ] **Step 6: Update the call sites**

Apply the Step 5 mapping to the six call sites listed in the table at the top of this task. Only the method name changes — the argument shapes (`{ path: { mediaKey } }`, `{ path: { id } }`, request bodies) come from the same endpoints and are unchanged.

For example, if `getByMediaKey` became `getMediaExceptionByMediaKey`, then `element.ts:62`:

```typescript
      tryExecute(this, PageSpeedOptimizer.getByMediaKey({ path: { mediaKey } })),
```

becomes:

```typescript
      tryExecute(this, PageSpeedOptimizer.getMediaExceptionByMediaKey({ path: { mediaKey } })),
```

Do not change the surrounding `tryExecute(this, …)` wrapping, the destructuring of `{ data }`, or `condition.ts`'s `this.permitted = data?.enabled ?? false;` — the response model is unchanged.

- [ ] **Step 7: Type-check and build the client**

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
npm run build
```

Expected: `tsc` reports no errors, then Vite writes `entry-point.js` to `../Umbraco.Community.PagespeedOptimizer.BackOffice/wwwroot/App_Plugins/pagespeedoptimizer`.

`tsc` failing on an unknown method name means Step 6 missed a call site — fix it there, not by casting to `any`.

- [ ] **Step 8: Verify a clean-install build, as CI runs it**

```bash
cd src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client
rm -rf node_modules && npm ci && npm run build
```

Expected: `npm ci` succeeds without an `EUSAGE` lock-file-out-of-sync error, and the build passes. This is exactly what `pr-validation.yml` does, so a failure here is a red CI run.

- [ ] **Step 9: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/package.json src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/package-lock.json src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/swagger.json src/code/Umbraco.Community.PagespeedOptimizer.BackOffice.Client/src
git commit -m "Regenerate the backoffice API client against Umbraco 18

swagger.json is refetched from the running V18 site at the new
/umbraco/openapi/{document}.json URL and is now OpenAPI 3.1. The
generated SDK and its call sites in element.ts and condition.ts are
updated to match. @umbraco-cms/backoffice moves to 18.x and
@hey-api/openapi-ts is pinned so CI and local codegen agree.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

`src/code/…BackOffice/wwwroot/App_Plugins/pagespeedoptimizer/` is gitignored, so the Vite output is correctly not part of this commit.

---

## Task 5: Full verification and packaging

No code changes. This task exists so the whole upgrade is confirmed end to end before the branch is opened as a PR.

**Files:** none modified.

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: a verified `Umbraco.Community.PagespeedOptimizer.18.0.0.nupkg` and a signed-off manual check.

- [ ] **Step 1: Clean full build and test**

```bash
dotnet restore src/ && dotnet build -c Release --no-restore src/ && dotnet test -c Release --no-restore --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura src/
```

Expected: `Build succeeded` with 0 warnings, and all tests passing. This is the `pr-validation.yml` sequence.

- [ ] **Step 2: Pack and confirm the version**

```bash
dotnet pack -c Release --no-restore --no-build --output ./nuget-verify src/
ls nuget-verify
```

Expected: `Umbraco.Community.PagespeedOptimizer.18.0.0.nupkg` plus its `.snupkg`, and nupkgs for the Core / Infrastructure / BackOffice projects at the same version. No `17.2.1` anywhere.

- [ ] **Step 3: Confirm the package's Umbraco dependency range**

```bash
unzip -o -q nuget-verify/Umbraco.Community.PagespeedOptimizer.18.0.0.nupkg -d nuget-verify/unpacked && grep -o 'id="Umbraco.Cms[^/]*' nuget-verify/unpacked/Umbraco.Community.PagespeedOptimizer.nuspec
```

Expected: any `Umbraco.Cms.*` dependencies show `version="[18.0.0, 19.0.0)"`. No `[17.0.0, 18.0.0)` and no `Swashbuckle`.

Then clean up (run from the repo root — `nuget-verify/` is a scratch directory and must not be committed):

```bash
rm -rf nuget-verify
```

- [ ] **Step 4: Boot the V18 site and check the OpenAPI UI**

```bash
dotnet run --project test-sites/Website-V18/Website-V18.csproj --launch-profile Website-V18
```

In a browser, open `https://localhost:44371/umbraco/openapi` and sign in as `admin@example.com` / `1234567890`.

Expected: the document dropdown includes **PageSpeed Optimizer Management API**, and selecting it lists exactly five operations, all under the `Page Speed Optimizer` tag, each showing a lock icon (backoffice authentication applied by `WithBackOfficeAuthentication()`).

- [ ] **Step 5: Manual backoffice check — the media exception workspace view**

This step is manual and is not automated.

1. In the backoffice, go to **Media** and upload a JPEG (or open one of Clean's existing images).
2. Confirm the **Override image optimizations** workspace view tab appears on the Image media item. Its absence means the `pagespeedoptimizer.condition.imageOptimizationEnabled` condition failed — which means the `getDefaultValues` call in `condition.ts` is broken, so re-check Task 4 Step 6.
3. Confirm the quality slider and Force WebP toggle load with the configured defaults (quality **70**, Force WebP **on**) from `appsettings.json`.
4. Toggle the override on, set quality to **30**, turn Force WebP off, and press the view's own Save button. Expect a success notification.
5. Reload the page and confirm the saved values persist (quality 30, Force WebP off).
6. Open the image on the front end of the site. In the browser's network tab, confirm the request for that image carries the overridden quality in its URL and is **not** served as WebP.
7. Delete the override and confirm the image reverts to quality 70 / WebP.
8. Confirm the delivered image URLs carry ImageSharp's HMAC signature (an `hmac=` query parameter) and that the images actually render rather than returning 400/404. The V18 site sets `Umbraco:CMS:Imaging:HMACSecretKey`, so image URLs are signed; this confirms the package's URL rewriting stays inside the signature rather than invalidating it.

- [ ] **Step 6: Manual front-end check — caching and compression**

Still on the running site, with the browser network tab open on a front-end page:

1. A static asset from `/wwwroot/assets/` (for example `css/index.css`) carries a `Cache-Control` header with a `max-age` — confirms `StaticAssetsCache`.
2. An HTML or CSS response carries `Content-Encoding: br` or `gzip` — confirms `ResponseCompression` (and that `app.EnableResponseCompression()` in `Program.cs` is wired correctly).

- [ ] **Step 7: Confirm the V17 site is still usable standalone**

```bash
dotnet build test-sites/Website-V17/Website-V17.csproj
```

Expected: `Build succeeded`, resolving `Umbraco.Community.PagespeedOptimizer` 17.2.1 from NuGet rather than from local sources.

- [ ] **Step 8: Report**

Summarise: build/test/pack results, the operation-ID mapping from Task 4 Step 5 (changed or unchanged), any contingency that had to be applied (explicit `Microsoft.AspNetCore.OpenApi` reference, `ShouldInclude` filter, `Umbraco.Cms` pin drop, `InfrastructureComposerTests` change), and the outcome of each manual check in Steps 5–6. State plainly which manual steps were actually performed and which were not.

Do not open a PR as part of this plan — use the `superpowers:finishing-a-development-branch` skill for that.
