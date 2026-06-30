# MediaExceptions EF Core Table + Repository Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a `PageSpeedOptimizer_MediaExceptions` database table via EF Core migrations with a full CRUD repository pattern, wired into the existing Umbraco package infrastructure.

**Architecture:** A `MediaException` POCO and `IMediaExceptionRepository` interface live in the Core project. An EF `DbContext`, internal repository implementation, migration files, startup migration runner, and DI registration all live in the Infrastructure project. The migration runs automatically on `UmbracoApplicationStartedNotification`.

**Tech Stack:** .NET 10, Umbraco 17, `Umbraco.Cms.Persistence.EFCore`, EF Core 10.0.6, NUnit 4, Moq

## Global Constraints

- Target framework: `net10.0`
- Umbraco version range: `[17.0.0, 18.0.0)`
- EF Core version: `10.0.6` (matches Umbraco 17's pinned version)
- All Infrastructure types are `internal sealed` — never `public`
- All Core types are `public sealed`
- Every file begins with the copyright header comment: `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`
- XML doc comments required on every member (public and internal) — StyleCop enforces this as a warning-as-error
- `using` directives go **outside** the namespace declaration (file-scoped namespace syntax is used throughout)
- Nullable reference types are enabled — use `?` where nullability applies
- `RestorePackagesWithLockFile=true` — run `dotnet restore src/ --force-evaluate` after any `.csproj` or `Directory.Packages.props` change
- Tests use NUnit 4 with `[TestFixture]`, `[Test]`, `Assert.Multiple` patterns
- Scope pattern: `using IEfCoreScope<T> scope = _scopeProvider.CreateScope(); ... scope.Complete();`
- `AddUmbracoDbContext<T>` is an extension on `IServiceCollection` in namespace `Umbraco.Extensions`

---

### Task 1: Add NuGet Packages

**Files:**
- Modify: `src/Directory.Packages.props`
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Umbraco.Community.PagespeedOptimizer.Infrastructure.csproj`
- Modify: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.csproj`

**Interfaces:**
- Produces: `Umbraco.Cms.Persistence.EFCore` package available in Infrastructure; `Microsoft.EntityFrameworkCore.InMemory` available in Infrastructure.Tests

- [ ] **Step 1: Add package versions to central props**

Open `src/Directory.Packages.props` and add the following `<PackageVersion>` entries inside the first `<ItemGroup>` (alongside the other `Umbraco.*` entries):

```xml
<PackageVersion Include="Umbraco.Cms.Persistence.EFCore" Version="[17.0.0,18.0.0)" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
```

The file should look like:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Umbraco.Cms.Web.Website" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Core" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Imaging.ImageSharp" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Umbraco.Cms.Persistence.EFCore" Version="[17.0.0,18.0.0)" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.6" />
  </ItemGroup>
  ...
</Project>
```

- [ ] **Step 2: Reference EFCore packages in the Infrastructure project**

Open `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Umbraco.Community.PagespeedOptimizer.Infrastructure.csproj` and add:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Umbraco.Cms.Imaging.ImageSharp" />
    <PackageReference Include="Umbraco.Cms.Persistence.EFCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Umbraco.Community.PagespeedOptimizer.Core\Umbraco.Community.PagespeedOptimizer.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Reference InMemory package in the Infrastructure test project**

Open `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.csproj` and add:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <ProjectReference Include="..\..\code\Umbraco.Community.PagespeedOptimizer.Infrastructure\Umbraco.Community.PagespeedOptimizer.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Restore and build**

```bash
dotnet restore src/ --force-evaluate
dotnet build -c Release src/
```

Expected: Build succeeds with no errors. Lock files regenerate.

- [ ] **Step 5: Commit**

```bash
git add src/Directory.Packages.props
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Umbraco.Community.PagespeedOptimizer.Infrastructure.csproj"
git add "src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.csproj"
git add src/**/*.lock.json
git commit -m "feat: add EFCore NuGet package references"
```

---

### Task 2: Core Entity Model and Repository Interface

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Core/Models/MediaException.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Core/Repositories/IMediaExceptionRepository.cs`

**Interfaces:**
- Produces: `MediaException` POCO; `IMediaExceptionRepository` with `GetAsync`, `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`

- [ ] **Step 1: Create the entity model**

Create `src/code/Umbraco.Community.PagespeedOptimizer.Core/Models/MediaException.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.Core.Models;

/// <summary>
/// Represents a per-media exception overriding the global image optimization settings.
/// </summary>
public sealed class MediaException
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco media key this exception applies to.
    /// </summary>
    public Guid MediaKey { get; set; }

    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force WebP for this media item.
    /// </summary>
    public bool ForceWebp { get; set; }
}
```

- [ ] **Step 2: Create the repository interface**

Create `src/code/Umbraco.Community.PagespeedOptimizer.Core/Repositories/IMediaExceptionRepository.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Core.Repositories;

/// <summary>
/// Defines CRUD operations for <see cref="MediaException"/> entities.
/// </summary>
public interface IMediaExceptionRepository
{
    /// <summary>
    /// Gets a <see cref="MediaException"/> by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or <c>null</c> if not found.</returns>
    Task<MediaException?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all <see cref="MediaException"/> entities.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All stored exceptions.</returns>
    Task<IEnumerable<MediaException>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new <see cref="MediaException"/>.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created entity (with any DB-assigned values populated).</returns>
    Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing <see cref="MediaException"/>.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated entity.</returns>
    Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Deletes a <see cref="MediaException"/> by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build -c Release src/
```

Expected: Build succeeds, no StyleCop warnings.

- [ ] **Step 4: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Core/Models/MediaException.cs"
git add "src/code/Umbraco.Community.PagespeedOptimizer.Core/Repositories/IMediaExceptionRepository.cs"
git commit -m "feat: add MediaException model and IMediaExceptionRepository interface"
```

---

### Task 3: DbContext

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/PageSpeedOptimizerDbContext.cs`
- Create: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Persistence/PageSpeedOptimizerDbContextTests.cs`

**Interfaces:**
- Consumes: `MediaException` from Task 2
- Produces: `PageSpeedOptimizerDbContext` with `DbSet<MediaException> MediaExceptions`; table mapped to `PageSpeedOptimizer_MediaExceptions`

- [ ] **Step 1: Write the failing test**

Create `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Persistence/PageSpeedOptimizerDbContextTests.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="PageSpeedOptimizerDbContext"/> model configuration.
/// </summary>
[TestFixture]
internal sealed class PageSpeedOptimizerDbContextTests
{
    private PageSpeedOptimizerDbContext dbContext = null!;

    /// <summary>
    /// Sets up an in-memory DbContext before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<PageSpeedOptimizerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        this.dbContext = new PageSpeedOptimizerDbContext(options);
    }

    /// <summary>
    /// Tears down the DbContext after each test.
    /// </summary>
    [TearDown]
    public void TearDown() => this.dbContext.Dispose();

    /// <summary>
    /// Verifies that <see cref="MediaException"/> maps to the correct table name.
    /// </summary>
    [Test]
    public void MediaException_Should_Map_To_Correct_Table_Name()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));

        Assert.That(entityType?.GetTableName(), Is.EqualTo("PageSpeedOptimizer_MediaExceptions"));
    }

    /// <summary>
    /// Verifies that the primary key is configured on the Id property.
    /// </summary>
    [Test]
    public void MediaException_Should_Have_Id_As_Primary_Key()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var primaryKey = entityType?.FindPrimaryKey();
        var keyProperty = primaryKey?.Properties.SingleOrDefault();

        Assert.That(keyProperty?.Name, Is.EqualTo(nameof(MediaException.Id)));
    }

    /// <summary>
    /// Verifies that MediaKey is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_MediaKey_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.MediaKey));

        Assert.That(property?.IsNullable, Is.False);
    }

    /// <summary>
    /// Verifies that Quality is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_Quality_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.Quality));

        Assert.That(property?.IsNullable, Is.False);
    }

    /// <summary>
    /// Verifies that ForceWebp is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_ForceWebp_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.ForceWebp));

        Assert.That(property?.IsNullable, Is.False);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

```bash
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --no-restore
```

Expected: Tests fail with `PageSpeedOptimizerDbContext` not found.

- [ ] **Step 3: Create the DbContext**

Create `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/PageSpeedOptimizerDbContext.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the PageSpeed Optimizer custom tables.
/// </summary>
internal sealed class PageSpeedOptimizerDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageSpeedOptimizerDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public PageSpeedOptimizerDbContext(DbContextOptions<PageSpeedOptimizerDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the media exceptions.
    /// </summary>
    public DbSet<MediaException> MediaExceptions { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaException>(entity =>
        {
            entity.ToTable("PageSpeedOptimizer_MediaExceptions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.MediaKey).IsRequired();
            entity.Property(e => e.Quality).IsRequired();
            entity.Property(e => e.ForceWebp).IsRequired();
        });
    }
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

```bash
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --no-restore
```

Expected: All `PageSpeedOptimizerDbContextTests` pass.

- [ ] **Step 5: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/PageSpeedOptimizerDbContext.cs"
git add "src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Persistence/PageSpeedOptimizerDbContextTests.cs"
git commit -m "feat: add PageSpeedOptimizerDbContext with MediaException mapping"
```

---

### Task 4: Repository Implementation

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Repositories/MediaExceptionRepository.cs`

**Interfaces:**
- Consumes: `IMediaExceptionRepository` from Task 2; `PageSpeedOptimizerDbContext` from Task 3; `IEFCoreScopeProvider<PageSpeedOptimizerDbContext>` from `Umbraco.Cms.Persistence.EFCore.Scoping`
- Produces: `MediaExceptionRepository` implementing `IMediaExceptionRepository`

> **Note:** Repository integration tests require a live Umbraco database and are out of scope for this spec. The implementation is verified to compile correctly; DI wiring is tested in Task 6.

- [ ] **Step 1: Create the repository**

Create `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Repositories/MediaExceptionRepository.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Persistence.EFCore.Scoping;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IMediaExceptionRepository"/>.
/// </summary>
internal sealed class MediaExceptionRepository : IMediaExceptionRepository
{
    private readonly IEFCoreScopeProvider<PageSpeedOptimizerDbContext> _scopeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionRepository"/> class.
    /// </summary>
    /// <param name="scopeProvider">The EF Core scope provider.</param>
    public MediaExceptionRepository(IEFCoreScopeProvider<PageSpeedOptimizerDbContext> scopeProvider)
        => _scopeProvider = scopeProvider;

    /// <inheritdoc />
    public async Task<MediaException?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException? result = await scope.ExecuteWithContextAsync(
            async (PageSpeedOptimizerDbContext db) => await db.MediaExceptions.FindAsync([id], ct));

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaException>> GetAllAsync(CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        List<MediaException> result = await scope.ExecuteWithContextAsync(
            async (PageSpeedOptimizerDbContext db) => await db.MediaExceptions.ToListAsync(ct));

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException result = await scope.ExecuteWithContextAsync(async (PageSpeedOptimizerDbContext db) =>
        {
            db.MediaExceptions.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        });

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException result = await scope.ExecuteWithContextAsync(async (PageSpeedOptimizerDbContext db) =>
        {
            db.MediaExceptions.Update(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        });

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<Task>(async (PageSpeedOptimizerDbContext db) =>
        {
            MediaException? entity = await db.MediaExceptions.FindAsync([id], ct);

            if (entity is not null)
            {
                db.MediaExceptions.Remove(entity);
                await db.SaveChangesAsync(ct);
            }
        });

        scope.Complete();
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build -c Release src/
```

Expected: Build succeeds with no errors or StyleCop warnings.

- [ ] **Step 3: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Repositories/MediaExceptionRepository.cs"
git commit -m "feat: add MediaExceptionRepository EF Core implementation"
```

---

### Task 5: Migration Runner

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Notifications/RunMediaExceptionsMigration.cs`

**Interfaces:**
- Consumes: `PageSpeedOptimizerDbContext` from Task 3; `UmbracoApplicationStartedNotification` from Umbraco.Cms.Core
- Produces: `RunMediaExceptionsMigration` implementing `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`

- [ ] **Step 1: Create the migration runner**

Create `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Notifications/RunMediaExceptionsMigration.cs`:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Notifications;

/// <summary>
/// Runs any pending EF Core migrations for the PageSpeed Optimizer on application startup.
/// </summary>
internal sealed class RunMediaExceptionsMigration : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly PageSpeedOptimizerDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunMediaExceptionsMigration"/> class.
    /// </summary>
    /// <param name="dbContext">The PageSpeed Optimizer database context.</param>
    public RunMediaExceptionsMigration(PageSpeedOptimizerDbContext dbContext)
        => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        => await _dbContext.Database.MigrateAsync(cancellationToken);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build -c Release src/
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Notifications/RunMediaExceptionsMigration.cs"
git commit -m "feat: add RunMediaExceptionsMigration startup notification handler"
```

---

### Task 6: DI Registration

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Modify: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs`

**Interfaces:**
- Consumes: `PageSpeedOptimizerDbContext` (Task 3); `MediaExceptionRepository` (Task 4); `RunMediaExceptionsMigration` (Task 5); `IMediaExceptionRepository` (Task 2)
- Produces: `IMediaExceptionRepository` registered as scoped; `PageSpeedOptimizerDbContext` registered via `AddUmbracoDbContext`; `RunMediaExceptionsMigration` registered as notification handler

- [ ] **Step 1: Write the failing DI registration test**

Add the following test to `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs` (inside the existing `InfrastructureComposerTests` class, after the last existing test method):

```csharp
/// <summary>
/// Tests that <see cref="IMediaExceptionRepository"/> is registered as a scoped service.
/// </summary>
[Test]
public void IMediaExceptionRepository_Should_Be_Registered_As_Scoped()
{
    var settings = new PageSpeedOptimizerSettings();

    this.Compose(settings);

    Assert.That(
        this.serviceCollection.Any(x =>
            x.ServiceType == typeof(IMediaExceptionRepository) &&
            x.Lifetime == ServiceLifetime.Scoped),
        Is.True);
}
```

Also add the required `using` statements at the top of `InfrastructureComposerTests.cs`:

```csharp
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --no-restore --filter "IMediaExceptionRepository_Should_Be_Registered_As_Scoped"
```

Expected: FAIL — `IMediaExceptionRepository` is not yet registered.

- [ ] **Step 3: Add DI registration to UmbracoBuilderExtensions**

Open `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs`.

Add the following `using` statements (keep all usings outside the namespace, in alphabetical order among the existing ones):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Repositories;
using Umbraco.Extensions;
```

Chain `AddMediaExceptionPersistence()` into the existing `AddPagespeedOptimizer` call:

```csharp
public static IUmbracoBuilder AddPagespeedOptimizer(this IUmbracoBuilder builder) =>
    builder
        .LoadConfiguration()
        .AddStaticCache()
        .AddResponseCompression()
        .AddOptimizedImageUrlGenerator()
        .AddMediaExceptionPersistence();
```

Add the new private method at the bottom of the class, before the closing `}`:

```csharp
private static IUmbracoBuilder AddMediaExceptionPersistence(this IUmbracoBuilder builder)
{
    builder.Services.AddUmbracoDbContext<PageSpeedOptimizerDbContext>(
        (serviceProvider, optionsBuilder, connectionString, providerName) =>
        {
            if (connectionString is not null && providerName is not null)
            {
                optionsBuilder.UseDatabaseProvider(providerName, connectionString);
            }
        },
        shareUmbracoConnection: true);

    builder.Services.AddScoped<IMediaExceptionRepository, MediaExceptionRepository>();

    builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunMediaExceptionsMigration>();

    return builder;
}
```

- [ ] **Step 4: Run all tests to confirm they pass**

```bash
dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --no-restore
```

Expected: All tests pass, including `IMediaExceptionRepository_Should_Be_Registered_As_Scoped`.

- [ ] **Step 5: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs"
git add "src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs"
git commit -m "feat: register MediaException persistence services and migration runner"
```

---

### Task 7: Generate EF Migration

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Migrations/<timestamp>_InitialCreate.cs` (generated)
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Migrations/<timestamp>_InitialCreate.Designer.cs` (generated)
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Migrations/PageSpeedOptimizerDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: `PageSpeedOptimizerDbContext` (Task 3); `test-sites/Website-V17` as startup project
- Produces: EF migration files that create `PageSpeedOptimizer_MediaExceptions` on `Database.MigrateAsync()`

> **Prerequisites:**
> 1. The `dotnet ef` CLI tool must be installed globally: `dotnet tool install --global dotnet-ef`
> 2. `test-sites/Website-V17` must have a project reference to `Umbraco.Community.PagespeedOptimizer.Infrastructure` so the EF tooling can discover the `DbContext`. Check whether the reference already exists; if not, add it temporarily for migration generation.

- [ ] **Step 1: Verify the tool is installed**

```bash
dotnet ef --version
```

Expected: Prints `Entity Framework Core .NET Command-line Tools 10.x.x` (or similar). If not, run: `dotnet tool install --global dotnet-ef`

- [ ] **Step 2: Generate the migration**

Run from the repo root:

```bash
dotnet ef migrations add InitialCreate \
  --context PageSpeedOptimizerDbContext \
  --project src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure \
  --startup-project test-sites/Website-V17 \
  --output-dir Persistence/Migrations
```

On Windows (PowerShell):

```powershell
dotnet ef migrations add InitialCreate `
  --context PageSpeedOptimizerDbContext `
  --project src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure `
  --startup-project test-sites/Website-V17 `
  --output-dir Persistence/Migrations
```

Expected: Three files created under `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 3: Verify the generated Up() method**

Open the generated `<timestamp>_InitialCreate.cs` file and confirm the `Up()` method creates the correct table:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "PageSpeedOptimizer_MediaExceptions",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "...", nullable: false),
            MediaKey = table.Column<Guid>(type: "...", nullable: false),
            Quality = table.Column<int>(type: "...", nullable: false),
            ForceWebp = table.Column<bool>(type: "...", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_PageSpeedOptimizer_MediaExceptions", x => x.Id);
        });
}
```

The exact column types (`uniqueidentifier`, `int`, `bit`) will differ by provider. Verify the table name and all four columns are present.

- [ ] **Step 4: Add copyright headers to generated files**

The `dotnet ef` tool does not add copyright headers. Add the following as the first line of each generated file:

```
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
```

Files to update:
- `Persistence/Migrations/<timestamp>_InitialCreate.cs`
- `Persistence/Migrations/<timestamp>_InitialCreate.Designer.cs`
- `Persistence/Migrations/PageSpeedOptimizerDbContextModelSnapshot.cs`

- [ ] **Step 5: Build to confirm migration files compile**

```bash
dotnet build -c Release src/
```

Expected: Build succeeds. StyleCop may warn about missing XML docs in generated files — add `// <auto-generated />` as the second line of each generated file (after the copyright header) to suppress StyleCop on generated code:

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.
// <auto-generated />
```

- [ ] **Step 6: Run all tests**

```bash
dotnet test -c Release --no-restore --no-build src/
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add "src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Persistence/Migrations/"
git commit -m "feat: add EF Core InitialCreate migration for PageSpeedOptimizer_MediaExceptions"
```
