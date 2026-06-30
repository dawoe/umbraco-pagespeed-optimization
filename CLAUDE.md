# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a NuGet package for Umbraco CMS (v17.x, targeting .NET 10) that provides three page speed optimizations:
- **Static asset caching** — cache-control headers for browser caching
- **Image optimization** — quality/format conversion with WebP support
- **Response compression** — Gzip/Brotli via ASP.NET Core middleware

## Solution Structure

```
src/
├── code/
│   ├── Umbraco.Community.PagespeedOptimizer/          # Entry point, WebApplication extensions
│   ├── Umbraco.Community.PagespeedOptimizer.Core/     # Configuration POCOs
│   └── Umbraco.Community.PagespeedOptimizer.Infrastructure/  # Composers, middleware, URL generators
├── test/
│   └── Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/
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

## Code Style

StyleCop.Analyzers is enforced with warnings-as-errors. Key rules from `src/stylecop.json`:
- `using` directives go **inside** the namespace
- XML documentation comments are required on public members
- All files must have a copyright header
- Nullable reference types are enabled

## Architecture Notes

- **`InfrastructureComposer`** is the Umbraco composer that wires up all three features via `IUmbracoBuilder` extensions in `UmbracoBuilderExtensions.cs`.
- **`WebApplicationExtensions`** exposes the `UsePageSpeedOptimizer()` extension called in `Program.cs` of the consuming site.
- **`StaticFileOptionsConfiguration`** configures cache headers on static files via `IPostConfigureOptions<StaticFileOptions>`.
- **`OptimizedImageUrlGenerator`** wraps Umbraco's `IImageUrlGenerator` to inject quality/format parameters.
- All features are independently toggled and configured via `appsettings.json` under the `PageSpeedOptimizer` key.

## Test Site

`test-sites/Website-V17/` is a full Umbraco v17 site used for manual testing. It is not part of the solution build.

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
