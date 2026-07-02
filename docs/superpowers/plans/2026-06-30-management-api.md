# Management API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a back-office Management API to the `BackOffice` project for CRUD operations on `MediaException` entities and reading global image optimization defaults.

**Architecture:** A single `MediaExceptionManagementApiController` inheriting `ManagementApiControllerBase` lives in `BackOffice/Controllers/`. It overrides the base `[MapToApi("management")]` with its own `[MapToApi("pagespeed-optimizer-management-api")]`, maps to a dedicated Swagger document registered by `BackOfficeComposer`. The main project gains a reference to `BackOffice` so Umbraco's composer discovery loads the assembly.

**Tech Stack:** .NET 10, Umbraco 17, `Umbraco.Cms.Api.Management` (ManagementApiControllerBase), `Swashbuckle.AspNetCore` (SwaggerGen), NUnit 4, Moq

## Global Constraints

- All files: copyright header `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`
- All files: `using` directives go **outside** the namespace (matches `stylecop.json` `"usingDirectivesPlacement": "outsideNamespace"`)
- All public members: XML documentation comments required (StyleCop SA1600)
- Nullable reference types enabled
- Target framework: `net10.0`
- Umbraco package version range: `[17.0.0,18.0.0)`
- Central package management: all new `PackageReference` version entries go in `src/Directory.Packages.props`; `BackOffice.csproj` uses `<PackageReference Include="..." />` with no version attribute

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `src/Directory.Packages.props` | Add `Umbraco.Cms.Api.Management` version entry |
| Modify | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj` | Add `Umbraco.Cms.Api.Management` PackageReference |
| Modify | `src/code/Umbraco.Community.PagespeedOptimizer/Umbraco.Community.PagespeedOptimizer.csproj` | Add ProjectReference to BackOffice |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/CreateMediaExceptionRequestModel.cs` | Request model for POST |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/UpdateMediaExceptionRequestModel.cs` | Request model for PUT |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/MediaExceptionResponseModel.cs` | Shared response model |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/DefaultValuesResponseModel.cs` | Response model for GET default-values |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs` | Registers custom Swagger document |
| Create | `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs` | The management API controller |
| Create | `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs` | Controller unit tests |

---

### Task 1: Update project references and NuGet packages

**Files:**
- Modify: `src/Directory.Packages.props`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer/Umbraco.Community.PagespeedOptimizer.csproj`

**Interfaces:**
- Produces: `ManagementApiControllerBase` available in BackOffice project; BackOffice assembly loaded by main project at startup

- [ ] **Step 1: Add `Umbraco.Cms.Api.Management` to central package versions**

In `src/Directory.Packages.props`, add inside the first `<ItemGroup>` (alongside other Umbraco packages):

```xml
<PackageVersion Include="Umbraco.Cms.Api.Management" Version="[17.0.0,18.0.0)" />
```

Full updated `<ItemGroup>` block:
```xml
<ItemGroup>
  <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[17.0.0,18.0.0)" />
  <PackageVersion Include="Umbraco.Cms.Core" Version="[17.0.0,18.0.0)" />
  <PackageVersion Include="Umbraco.Cms.Api.Management" Version="[17.0.0,18.0.0)" />
  <PackageVersion Include="Umbraco.Cms.Imaging.ImageSharp" Version="[17.0.0,18.0.0)" />
  <PackageVersion Include="Umbraco.Cms.Persistence.EFCore" Version="[17.0.0,18.0.0)" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
</ItemGroup>
```

- [ ] **Step 2: Reference `Umbraco.Cms.Api.Management` in the BackOffice project**

Replace the entire contents of `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Umbraco.Cms.Api.Management" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Umbraco.Community.PagespeedOptimizer.Core\Umbraco.Community.PagespeedOptimizer.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add BackOffice project reference to the main project**

Replace the contents of `src/code/Umbraco.Community.PagespeedOptimizer/Umbraco.Community.PagespeedOptimizer.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Umbraco.Community.PagespeedOptimizer.Infrastructure\Umbraco.Community.PagespeedOptimizer.Infrastructure.csproj" />
    <ProjectReference Include="..\Umbraco.Community.PagespeedOptimizer.BackOffice\Umbraco.Community.PagespeedOptimizer.BackOffice.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <PackageTags>umbraco, umbraco-marketplace</PackageTags>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Restore and verify build**

```
dotnet restore src/
dotnet build -c Release src/
```

Expected: Build succeeds, no errors.

- [ ] **Step 5: Commit**

```
git add src/Directory.Packages.props
git add "src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Umbraco.Community.PagespeedOptimizer.BackOffice.csproj"
git add "src/code/Umbraco.Community.PagespeedOptimizer/Umbraco.Community.PagespeedOptimizer.csproj"
git commit -m "chore: add Umbraco.Cms.Api.Management reference and wire BackOffice into main project"
```

---

### Task 2: Create request and response models

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/CreateMediaExceptionRequestModel.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/UpdateMediaExceptionRequestModel.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/MediaExceptionResponseModel.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/DefaultValuesResponseModel.cs`

**Interfaces:**
- Produces: `CreateMediaExceptionRequestModel`, `UpdateMediaExceptionRequestModel`, `MediaExceptionResponseModel`, `DefaultValuesResponseModel` in namespace `Umbraco.Community.PagespeedOptimizer.BackOffice.Models`

- [ ] **Step 1: Create `CreateMediaExceptionRequestModel.cs`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Request model for creating a media exception.
/// </summary>
public sealed class CreateMediaExceptionRequestModel
{
    /// <summary>
    /// Gets or sets the Umbraco media item key this exception applies to.
    /// </summary>
    public required Guid MediaKey { get; set; }

    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public required int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force WebP conversion for this media item.
    /// </summary>
    public required bool ForceWebp { get; set; }
}
```

- [ ] **Step 2: Create `UpdateMediaExceptionRequestModel.cs`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Request model for updating an existing media exception.
/// </summary>
public sealed class UpdateMediaExceptionRequestModel
{
    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public required int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force WebP conversion for this media item.
    /// </summary>
    public required bool ForceWebp { get; set; }
}
```

- [ ] **Step 3: Create `MediaExceptionResponseModel.cs`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Response model representing a media exception.
/// </summary>
public sealed class MediaExceptionResponseModel
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco media item key this exception applies to.
    /// </summary>
    public required Guid MediaKey { get; set; }

    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public required int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether WebP conversion is forced for this media item.
    /// </summary>
    public required bool ForceWebp { get; set; }
}
```

- [ ] **Step 4: Create `DefaultValuesResponseModel.cs`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Response model for the global image optimization default values from application settings.
/// </summary>
public sealed class DefaultValuesResponseModel
{
    /// <summary>
    /// Gets or sets the default image quality from global settings.
    /// </summary>
    public required int DefaultImageQuality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether WebP is forced globally.
    /// </summary>
    public required bool ForceWebP { get; set; }
}
```

- [ ] **Step 5: Verify build**

```
dotnet build -c Release src/
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Models/
git commit -m "feat: add management API request and response models"
```

---

### Task 3: Create BackOfficeComposer for Swagger registration

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs`

**Interfaces:**
- Consumes: `IComposer` from `Umbraco.Cms.Core.Composing`; `SwaggerGenOptions` from `Swashbuckle.AspNetCore.SwaggerGen` (transitive via `Umbraco.Cms.Api.Management`)
- Produces: Swagger document `"pagespeed-optimizer-management-api"` registered in DI

- [ ] **Step 1: Create `BackOfficeComposer.cs`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice;

/// <summary>
/// Composer for the Page Speed Optimizer back-office, registering the management API Swagger document.
/// </summary>
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddSwaggerGen(options =>
            options.SwaggerDoc(
                "pagespeed-optimizer-management-api",
                new OpenApiInfo
                {
                    Title = "PageSpeed Optimizer Management API",
                    Version = "1.0",
                }));
}
```

- [ ] **Step 2: Verify build**

```
dotnet build -c Release src/
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/BackOfficeComposer.cs
git commit -m "feat: register PageSpeed Optimizer management API Swagger document"
```

---

### Task 4: Create MediaExceptionManagementApiController with tests

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`
- Create: `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionRepository` (Core), `IOptions<PageSpeedOptimizerSettings>` (Core), `ManagementApiControllerBase` (Umbraco.Cms.Api.Management), models from Task 2
- Produces: REST endpoints at `/umbraco/management/api/v1/pagespeed-optimizer/media-exception`

- [ ] **Step 1: Write failing tests**

Create `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="MediaExceptionManagementApiController"/>.
/// </summary>
[TestFixture]
internal sealed class MediaExceptionManagementApiControllerTests
{
    private Mock<IMediaExceptionRepository> _repositoryMock = null!;
    private Mock<IOptions<PageSpeedOptimizerSettings>> _settingsMock = null!;
    private MediaExceptionManagementApiController _controller = null!;

    /// <summary>
    /// Sets up a fresh controller with mocked dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IMediaExceptionRepository>();
        _settingsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        _settingsMock.Setup(s => s.Value).Returns(new PageSpeedOptimizerSettings());
        _controller = new MediaExceptionManagementApiController(_repositoryMock.Object, _settingsMock.Object);
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.CreateMediaException"/> returns 201 Created with the created entity.
    /// </summary>
    [Test]
    public async Task CreateMediaException_Returns_201_With_ResponseModel()
    {
        var mediaKey = Guid.NewGuid();
        var request = new CreateMediaExceptionRequestModel { MediaKey = mediaKey, Quality = 75, ForceWebp = true };

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await _controller.CreateMediaException(request, CancellationToken.None);

        var created = result as CreatedResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.StatusCode, Is.EqualTo(201));

        var response = created.Value as MediaExceptionResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.MediaKey, Is.EqualTo(mediaKey));
            Assert.That(response.Quality, Is.EqualTo(75));
            Assert.That(response.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.UpdateMediaException"/> returns 200 OK with the updated entity when found.
    /// </summary>
    [Test]
    public async Task UpdateMediaException_Returns_200_With_ResponseModel_When_Found()
    {
        var id = Guid.NewGuid();
        var existing = new MediaException { Id = id, MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };
        var request = new UpdateMediaExceptionRequestModel { Quality = 60, ForceWebp = true };

        _repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await _controller.UpdateMediaException(id, request, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as MediaExceptionResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Id, Is.EqualTo(id));
            Assert.That(response.Quality, Is.EqualTo(60));
            Assert.That(response.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.UpdateMediaException"/> returns 404 when the entity does not exist.
    /// </summary>
    [Test]
    public async Task UpdateMediaException_Returns_404_When_Not_Found()
    {
        _repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await _controller.UpdateMediaException(Guid.NewGuid(), new UpdateMediaExceptionRequestModel { Quality = 80, ForceWebp = false }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.DeleteMediaException"/> returns 200 OK when the entity exists.
    /// </summary>
    [Test]
    public async Task DeleteMediaException_Returns_200_When_Found()
    {
        var id = Guid.NewGuid();
        var existing = new MediaException { Id = id, MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };

        _repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repositoryMock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.DeleteMediaException(id, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.DeleteMediaException"/> returns 404 when the entity does not exist.
    /// </summary>
    [Test]
    public async Task DeleteMediaException_Returns_404_When_Not_Found()
    {
        _repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await _controller.DeleteMediaException(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetDefaultValues"/> returns 200 OK with the values from settings.
    /// </summary>
    [Test]
    public void GetDefaultValues_Returns_200_With_Settings_Values()
    {
        var settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { DefaultImageQuality = 70, ForceWebP = true },
        };
        _settingsMock.Setup(s => s.Value).Returns(settings);

        var result = _controller.GetDefaultValues();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as DefaultValuesResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.DefaultImageQuality, Is.EqualTo(70));
            Assert.That(response.ForceWebP, Is.True);
        });
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (controller not yet created)**

```
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ -c Release
```

Expected: Compilation error — `MediaExceptionManagementApiController` does not exist.

- [ ] **Step 3: Create `MediaExceptionManagementApiController.cs`**

Create `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers;

/// <summary>
/// Management API controller for creating, updating, and deleting media exceptions, and for reading global image optimization defaults.
/// </summary>
[Route("umbraco/management/api/v1/pagespeed-optimizer/media-exception")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[MapToApi("pagespeed-optimizer-management-api")]
[ApiExplorerSettings(GroupName = "pagespeed-optimizer-management-api")]
public class MediaExceptionManagementApiController : ManagementApiControllerBase
{
    private readonly IMediaExceptionRepository _repository;
    private readonly IOptions<PageSpeedOptimizerSettings> _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionManagementApiController"/> class.
    /// </summary>
    /// <param name="repository">The media exception repository.</param>
    /// <param name="settings">The page speed optimizer settings.</param>
    public MediaExceptionManagementApiController(
        IMediaExceptionRepository repository,
        IOptions<PageSpeedOptimizerSettings> settings)
    {
        _repository = repository;
        _settings = settings;
    }

    /// <summary>
    /// Creates a new media exception.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created media exception with a 201 Created response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMediaException(CreateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var entity = new MediaException
        {
            Id = Guid.NewGuid(),
            MediaKey = request.MediaKey,
            Quality = request.Quality,
            ForceWebp = request.ForceWebp,
        };

        var created = await _repository.CreateAsync(entity, ct);
        return Created(string.Empty, MapToResponseModel(created));
    }

    /// <summary>
    /// Updates an existing media exception.
    /// </summary>
    /// <param name="id">The unique identifier of the media exception.</param>
    /// <param name="request">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated media exception, or 404 Not Found if no exception exists for the given identifier.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMediaException(Guid id, UpdateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var existing = await _repository.GetAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Quality = request.Quality;
        existing.ForceWebp = request.ForceWebp;

        var updated = await _repository.UpdateAsync(existing, ct);
        return Ok(MapToResponseModel(updated));
    }

    /// <summary>
    /// Deletes a media exception.
    /// </summary>
    /// <param name="id">The unique identifier of the media exception to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK if deleted, or 404 Not Found if no exception exists for the given identifier.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMediaException(Guid id, CancellationToken ct)
    {
        var existing = await _repository.GetAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, ct);
        return Ok();
    }

    /// <summary>
    /// Gets the default image quality and ForceWebP values from global application settings.
    /// </summary>
    /// <returns>The default values from <see cref="ImageOptimizationSettings"/>.</returns>
    [HttpGet("default-values")]
    [ProducesResponseType(typeof(DefaultValuesResponseModel), StatusCodes.Status200OK)]
    public IActionResult GetDefaultValues()
    {
        var imageSettings = _settings.Value.ImageOptimization;
        return Ok(new DefaultValuesResponseModel
        {
            DefaultImageQuality = imageSettings.DefaultImageQuality,
            ForceWebP = imageSettings.ForceWebP,
        });
    }

    private static MediaExceptionResponseModel MapToResponseModel(MediaException entity) =>
        new()
        {
            Id = entity.Id,
            MediaKey = entity.MediaKey,
            Quality = entity.Quality,
            ForceWebp = entity.ForceWebp,
        };
}
```

- [ ] **Step 4: Run tests and verify they pass**

```
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ -c Release
```

Expected: All 6 tests pass.

- [ ] **Step 5: Run full test suite**

```
dotnet test -c Release src/
```

Expected: All tests pass; no regressions.

- [ ] **Step 6: Commit**

```
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs
git add src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs
git commit -m "feat: add MediaException management API controller"
```
