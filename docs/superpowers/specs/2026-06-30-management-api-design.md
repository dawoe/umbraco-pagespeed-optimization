# Management API Design

**Date:** 2026-06-30
**Branch:** feature/davew/management-api

## Overview

Add a back-office Management API to `Umbraco.Community.PagespeedOptimizer.BackOffice` for managing `MediaException` records and reading default image optimization settings.

## Project Structure Changes

### New files in `BackOffice/`

```
BackOffice/
├── Controllers/
│   └── MediaExceptionManagementApiController.cs
├── Models/
│   ├── CreateMediaExceptionRequestModel.cs
│   ├── UpdateMediaExceptionRequestModel.cs
│   ├── MediaExceptionResponseModel.cs
│   └── DefaultValuesResponseModel.cs
└── BackOfficeComposer.cs
```

### Project reference changes

- `BackOffice.csproj`: add `PackageReference` to `Umbraco.Cms` (for `ManagementApiControllerBase`, auth policies, Swagger helpers)
- `Umbraco.Community.PagespeedOptimizer.csproj` (main project): add `ProjectReference` to `BackOffice`
- No additional project-to-project references needed — `IMediaExceptionRepository` and `IOptions<PageSpeedOptimizerSettings>` come from `Core`, already referenced by `BackOffice`

## API Endpoints

**Base route:** `/umbraco/management/api/v1/pagespeed-optimizer/media-exception`

**Authorization:** Umbraco back-office authentication + media section access

| Method | Route | Action | Request Body | Success Response |
|--------|-------|--------|--------------|-----------------|
| `POST` | `/` | CreateMediaException | `CreateMediaExceptionRequestModel` | `201` + `MediaExceptionResponseModel` |
| `PUT` | `/{id}` | UpdateMediaException | `UpdateMediaExceptionRequestModel` | `200` + `MediaExceptionResponseModel` |
| `DELETE` | `/{id}` | DeleteMediaException | — | `200` |
| `GET` | `/default-values` | GetDefaultValues | — | `200` + `DefaultValuesResponseModel` |

### Error responses

- `404` when `id` not found (Update, Delete)
- Standard Umbraco problem details format for validation errors

## Request & Response Models

### `CreateMediaExceptionRequestModel`
```
MediaKey  Guid   required — Umbraco media item key
Quality   int    required — override quality (1–100)
ForceWebp bool   required — override WebP conversion
```

### `UpdateMediaExceptionRequestModel`
```
Quality   int    required
ForceWebp bool   required
```

### `MediaExceptionResponseModel`
```
Id        Guid
MediaKey  Guid
Quality   int
ForceWebp bool
```

### `DefaultValuesResponseModel`
Returns values from `ImageOptimizationSettings` in `appsettings.json`:
```
DefaultImageQuality  int
ForceWebP            bool
```

## Controller

`MediaExceptionManagementApiController` inherits `ManagementApiControllerBase`.

- Decorated with `[ApiExplorerSettings(GroupName = "pagespeed-optimizer-management-api")]`
- Constructor-injected: `IMediaExceptionRepository`, `IOptions<PageSpeedOptimizerSettings>`
- All action methods are `async` with `CancellationToken` parameter
- StyleCop: XML doc comments on all public members, copyright header, `using` inside namespace

## Swagger Registration

`BackOfficeComposer` (implements `IComposer`) registers a dedicated Swagger document:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("pagespeed-optimizer-management-api", new OpenApiInfo
    {
        Title = "PageSpeed Optimizer Management API",
        Version = "1.0"
    });
});
```

Umbraco auto-discovers `IComposer` implementations across all loaded assemblies. The `BackOffice` assembly is loaded because the main project references it.

Reference: [Custom Swagger API | Umbraco 17 docs](https://docs.umbraco.com/umbraco-cms/17.latest/extend-your-project/server-side-extensions/custom-swagger-api)
