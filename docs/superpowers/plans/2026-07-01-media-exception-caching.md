# Media Exception Caching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `OptimizedImageUrlGenerator` applies per-media quality/WebP overrides from `MediaException` rows, sourced from an isolated-cache-backed lookup that is rebuilt asynchronously (never inline on the image-rendering hot path) whenever the underlying data changes.

**Architecture:** A new `IMediaExceptionCache` holds a `Dictionary<string ImageUrl, MediaException>` in Umbraco's isolated cache (`AppCaches.IsolatedCaches`). It is only ever rebuilt from genuinely asynchronous entry points — an app-startup notification handler and a new `MediaExceptionCacheRefresher` notification handler — never from `GetImageUrl()` itself, which only performs a synchronous, non-blocking read. Writes go through a new `IMediaExceptionService` that wraps `IMediaExceptionRepository` and triggers `DistributedCache.RefreshAll` after each successful write.

**Tech Stack:** .NET 10, Umbraco CMS 17.x, EF Core (via `IEFCoreScopeProvider<T>`), NUnit + Moq for tests.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-01-media-exception-caching-design.md` — follow it exactly; this plan implements it task-by-task.
- Copyright header on every new file, verbatim: `// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.`
- **`using` directives go before the `namespace` declaration**, and the namespace is file-scoped (`namespace X.Y;`) — this is the actual convention in every existing file in this repo (`OptimizedImageUrlGenerator.cs`, `MediaExceptionRepository.cs`, `IMediaExceptionRepository.cs`), overriding the (stale) "usings inside namespace" note in `CLAUDE.md`.
- XML doc comments (`<summary>`, `<param>`, `<returns>` as applicable) are required on every type, constructor, and member — including `internal` ones — matching the existing style in `MediaExceptionRepository.cs` and `OptimizedImageUrlGenerator.cs`. StyleCop.Analyzers runs with warnings-as-errors.
- Field naming: `private readonly` fields use `this.fieldName` (camelCase, no underscore prefix), matching `MediaExceptionRepository.cs` and `OptimizedImageUrlGenerator.cs` (the majority convention in this repo).
- Test style: NUnit `[TestFixture]`/`[Test]`/`[SetUp]`, Moq for mocks, `Assert.That(...)` (constraint model), `Assert.Multiple` for multi-assertion tests — matching `StaticFileOptionsConfigurationTests.cs` and `MediaExceptionManagementApiControllerTests.cs`.
- Build/test commands (from `CLAUDE.md`):
  - `dotnet build -c Release --no-restore src/`
  - `dotnet test -c Release --no-restore --no-build src/`
- New/changed production types: `MediaExceptionCache`, `MediaExceptionCacheRefresher`, `MediaExceptionCacheRefresherNotification`, `RebuildMediaExceptionCacheOnStartup`, `RebuildMediaExceptionCacheOnRefresh`, `MediaExceptionService` are all `internal sealed`, matching `MediaExceptionRepository`/`OptimizedImageUrlGenerator`. The two new Core interfaces (`IMediaExceptionCache`, `IMediaExceptionService`) are `public`, matching `IMediaExceptionRepository`.

---

### Task 1: `IMediaExceptionCache` interface and `MediaExceptionCache` implementation

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Core/Caching/IMediaExceptionCache.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCache.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/MediaExceptionCacheTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionRepository.GetAllAsync(CancellationToken ct = default) : Task<IEnumerable<MediaException>>` (existing, `src/code/Umbraco.Community.PagespeedOptimizer.Core/Repositories/IMediaExceptionRepository.cs`); `MediaException` (existing, `Id`, `MediaKey`, `Quality`, `ForceWebp`); Umbraco's `AppCaches`, `IPublishedUrlProvider.GetMediaUrl(Guid id, UrlMode mode = ..., ...)`, `IUmbracoContextFactory.EnsureUmbracoContext() : UmbracoContextReference`.
- Produces: `IMediaExceptionCache.GetByImageUrl(string imageUrl) : MediaException?` and `IMediaExceptionCache.RebuildAsync() : Task` — consumed by Task 2 (`OptimizedImageUrlGenerator`) and Task 3 (notification handlers).

- [ ] **Step 1: Write the `IMediaExceptionCache` interface**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Core.Caching;

/// <summary>
/// Provides fast, non-blocking lookup of <see cref="MediaException"/> entities by the image URL they apply to.
/// </summary>
public interface IMediaExceptionCache
{
    /// <summary>
    /// Gets the <see cref="MediaException"/> that applies to the given image URL, if any.
    /// </summary>
    /// <param name="imageUrl">The image URL, as produced by Umbraco's image URL generation pipeline.</param>
    /// <returns>The matching <see cref="MediaException"/>, or <c>null</c> if none matches or the cache has not been built yet.</returns>
    /// <remarks>This is a synchronous, non-blocking read. It never triggers a rebuild.</remarks>
    MediaException? GetByImageUrl(string imageUrl);

    /// <summary>
    /// Rebuilds the cached URL-to-exception map from the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Must only be called from asynchronous entry points (startup, cache refresh notifications) — never from a synchronous hot path.</remarks>
    Task RebuildAsync();
}
```

- [ ] **Step 2: Write the failing tests for `MediaExceptionCache`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="MediaExceptionCache"/>.
/// </summary>
[TestFixture]
internal sealed class MediaExceptionCacheTests
{
    private Mock<IMediaExceptionRepository> repositoryMock = null!;
    private Mock<IPublishedUrlProvider> publishedUrlProviderMock = null!;
    private Mock<IUmbracoContextFactory> umbracoContextFactoryMock = null!;
    private AppCaches appCaches = null!;
    private MediaExceptionCache cache = null!;

    /// <summary>
    /// Sets up a fresh cache with a real, working isolated cache before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.repositoryMock = new Mock<IMediaExceptionRepository>();
        this.publishedUrlProviderMock = new Mock<IPublishedUrlProvider>();

        this.umbracoContextFactoryMock = new Mock<IUmbracoContextFactory>();
        this.umbracoContextFactoryMock
            .Setup(f => f.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(Mock.Of<IUmbracoContext>(), true, Mock.Of<IUmbracoContextAccessor>()));

        this.appCaches = new AppCaches(new ObjectCacheAppCache(), Mock.Of<IRequestCache>(), new IsolatedCaches(_ => new ObjectCacheAppCache()));

        this.cache = new MediaExceptionCache(
            this.appCaches,
            this.repositoryMock.Object,
            this.publishedUrlProviderMock.Object,
            this.umbracoContextFactoryMock.Object);
    }

    /// <summary>
    /// Tests that a lookup before any rebuild has happened returns null instead of blocking or throwing.
    /// </summary>
    [Test]
    public void GetByImageUrl_Returns_Null_When_Cache_Has_Not_Been_Built()
    {
        var result = this.cache.GetByImageUrl("/media/1001/photo.jpg");

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests that rebuilding populates the cache keyed by the resolved media URL.
    /// </summary>
    [Test]
    public async Task RebuildAsync_Populates_Cache_With_Resolved_Urls()
    {
        var mediaKey = Guid.NewGuid();
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = mediaKey, Quality = 60, ForceWebp = true };

        this.repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaException> { exception });

        this.publishedUrlProviderMock
            .Setup(p => p.GetMediaUrl(mediaKey, It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns("/media/1001/photo.jpg");

        await this.cache.RebuildAsync();

        var result = this.cache.GetByImageUrl("/media/1001/photo.jpg");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Quality, Is.EqualTo(60));
            Assert.That(result.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that an exception whose media no longer resolves to a URL (e.g. deleted media) is skipped rather than breaking the rebuild.
    /// </summary>
    [Test]
    public async Task RebuildAsync_Skips_Exceptions_Whose_Media_No_Longer_Resolves()
    {
        var mediaKey = Guid.NewGuid();
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = mediaKey, Quality = 60, ForceWebp = true };

        this.repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaException> { exception });

        this.publishedUrlProviderMock
            .Setup(p => p.GetMediaUrl(mediaKey, It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns(string.Empty);

        await this.cache.RebuildAsync();

        var result = this.cache.GetByImageUrl("/media/1001/photo.jpg");

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Tests that a second rebuild fully replaces the contents of the first, rather than merging with it.
    /// </summary>
    [Test]
    public async Task RebuildAsync_Overwrites_Previous_Cache_Contents()
    {
        var firstKey = Guid.NewGuid();
        var firstException = new MediaException { Id = Guid.NewGuid(), MediaKey = firstKey, Quality = 50, ForceWebp = false };

        this.repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaException> { firstException });

        this.publishedUrlProviderMock
            .Setup(p => p.GetMediaUrl(firstKey, It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns("/media/1001/photo.jpg");

        await this.cache.RebuildAsync();

        this.repositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaException>());

        await this.cache.RebuildAsync();

        var result = this.cache.GetByImageUrl("/media/1001/photo.jpg");

        Assert.That(result, Is.Null);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail (compile error — `MediaExceptionCache` does not exist yet)**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionCacheTests"`
Expected: build FAILS — `MediaExceptionCache` (Infrastructure implementation) does not exist yet.

- [ ] **Step 4: Implement `MediaExceptionCache`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

/// <summary>
/// Isolated-cache-backed implementation of <see cref="IMediaExceptionCache"/>.
/// </summary>
internal sealed class MediaExceptionCache : IMediaExceptionCache
{
    private const string CacheKey = "PageSpeedOptimizer_MediaExceptionsByUrl";

    private readonly AppCaches appCaches;
    private readonly IMediaExceptionRepository repository;
    private readonly IPublishedUrlProvider publishedUrlProvider;
    private readonly IUmbracoContextFactory umbracoContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionCache"/> class.
    /// </summary>
    /// <param name="appCaches">Umbraco's application caches.</param>
    /// <param name="repository">The media exception repository.</param>
    /// <param name="publishedUrlProvider">Used to resolve a media key to its current URL.</param>
    /// <param name="umbracoContextFactory">Used to provide an ambient Umbraco context during background rebuilds.</param>
    public MediaExceptionCache(
        AppCaches appCaches,
        IMediaExceptionRepository repository,
        IPublishedUrlProvider publishedUrlProvider,
        IUmbracoContextFactory umbracoContextFactory)
    {
        this.appCaches = appCaches;
        this.repository = repository;
        this.publishedUrlProvider = publishedUrlProvider;
        this.umbracoContextFactory = umbracoContextFactory;
    }

    /// <inheritdoc />
    public MediaException? GetByImageUrl(string imageUrl)
    {
        var isolatedCache = this.appCaches.IsolatedCaches.GetOrCreate<MediaException>();

        if (isolatedCache.Get(CacheKey) is not Dictionary<string, MediaException> map)
        {
            return null;
        }

        return map.TryGetValue(imageUrl, out var exception) ? exception : null;
    }

    /// <inheritdoc />
    public async Task RebuildAsync()
    {
        var exceptions = await this.repository.GetAllAsync();

        var map = new Dictionary<string, MediaException>(StringComparer.OrdinalIgnoreCase);

        using (this.umbracoContextFactory.EnsureUmbracoContext())
        {
            foreach (var exception in exceptions)
            {
                var url = this.publishedUrlProvider.GetMediaUrl(exception.MediaKey, UrlMode.Relative);

                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                map[url] = exception;
            }
        }

        var isolatedCache = this.appCaches.IsolatedCaches.GetOrCreate<MediaException>();
        isolatedCache.Insert(CacheKey, () => map);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionCacheTests"`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Core/Caching/IMediaExceptionCache.cs src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCache.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/MediaExceptionCacheTests.cs
git commit -m "feat: add IMediaExceptionCache with isolated-cache-backed URL lookup"
```

---

### Task 2: `MediaExceptionCacheRefresherNotification` and `MediaExceptionCacheRefresher`

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCacheRefresherNotification.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCacheRefresher.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/MediaExceptionCacheRefresherTests.cs`

**Interfaces:**
- Consumes: Umbraco's `CacheRefresherBase<TNotification>` (constructor `(AppCaches, IEventAggregator, ICacheRefresherNotificationFactory)`), `CacheRefresherNotification` base class.
- Produces: `MediaExceptionCacheRefresher.UniqueId : Guid` (a fixed `317F1A0A-1131-4F2B-A9E8-6287C49C6B38`) — consumed by Task 5 (`MediaExceptionService`) and Task 7 (DI registration). `MediaExceptionCacheRefresherNotification` — consumed by Task 3 (notification handler).

- [ ] **Step 1: Write the failing test**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="MediaExceptionCacheRefresher"/>.
/// </summary>
[TestFixture]
internal sealed class MediaExceptionCacheRefresherTests
{
    /// <summary>
    /// Tests that calling RefreshAll publishes a <see cref="MediaExceptionCacheRefresherNotification"/>.
    /// </summary>
    [Test]
    public void RefreshAll_Publishes_MediaExceptionCacheRefresherNotification()
    {
        var appCaches = new AppCaches(new ObjectCacheAppCache(), Mock.Of<IRequestCache>(), new IsolatedCaches(_ => new ObjectCacheAppCache()));

        MediaExceptionCacheRefresherNotification? published = null;

        // CacheRefresherBase<TNotification>.OnCacheUpdated takes a CacheRefresherNotification, so the
        // generic type argument resolved at the EventAggregator.Publish call site is CacheRefresherNotification,
        // not MediaExceptionCacheRefresherNotification. The setup must match that actual runtime dispatch.
        var eventAggregatorMock = new Mock<IEventAggregator>();
        eventAggregatorMock
            .Setup(a => a.Publish(It.IsAny<CacheRefresherNotification>()))
            .Callback<CacheRefresherNotification>(n => published = n as MediaExceptionCacheRefresherNotification);

        var notificationFactoryMock = new Mock<ICacheRefresherNotificationFactory>();
        notificationFactoryMock
            .Setup(f => f.Create<MediaExceptionCacheRefresherNotification>(It.IsAny<object>(), MessageType.RefreshAll))
            .Returns((object msg, MessageType type) => new MediaExceptionCacheRefresherNotification(msg, type));

        var refresher = new MediaExceptionCacheRefresher(appCaches, eventAggregatorMock.Object, notificationFactoryMock.Object);

        refresher.RefreshAll();

        Assert.That(published, Is.Not.Null);
    }

    /// <summary>
    /// Tests that the refresher's unique id is stable.
    /// </summary>
    [Test]
    public void RefresherUniqueId_Is_The_Expected_Fixed_Guid()
    {
        Assert.That(MediaExceptionCacheRefresher.UniqueId, Is.EqualTo(Guid.Parse("317F1A0A-1131-4F2B-A9E8-6287C49C6B38")));
    }

    /// <summary>
    /// Tests that per-id refresh is not supported, since this refresher only ever does a full rebuild.
    /// </summary>
    [Test]
    public void Refresh_By_Int_Id_Throws_NotSupportedException()
    {
        var appCaches = new AppCaches(new ObjectCacheAppCache(), Mock.Of<IRequestCache>(), new IsolatedCaches(_ => new ObjectCacheAppCache()));
        var refresher = new MediaExceptionCacheRefresher(appCaches, Mock.Of<IEventAggregator>(), Mock.Of<ICacheRefresherNotificationFactory>());

        Assert.Throws<NotSupportedException>(() => refresher.Refresh(1));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionCacheRefresherTests"`
Expected: build FAILS — `MediaExceptionCacheRefresher` and `MediaExceptionCacheRefresherNotification` do not exist yet.

- [ ] **Step 3: Implement `MediaExceptionCacheRefresherNotification`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

/// <summary>
/// Notification published whenever the media exception cache should be refreshed.
/// </summary>
internal sealed class MediaExceptionCacheRefresherNotification : CacheRefresherNotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionCacheRefresherNotification"/> class.
    /// </summary>
    /// <param name="messageObject">The payload containing information about what to refresh.</param>
    /// <param name="messageType">The type of cache refresh operation.</param>
    public MediaExceptionCacheRefresherNotification(object messageObject, MessageType messageType)
        : base(messageObject, messageType)
    {
    }
}
```

- [ ] **Step 4: Implement `MediaExceptionCacheRefresher`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

/// <summary>
/// Cache refresher for media exception overrides. Only supports a full rebuild — the exception list is expected to be small.
/// </summary>
internal sealed class MediaExceptionCacheRefresher : CacheRefresherBase<MediaExceptionCacheRefresherNotification>
{
    /// <summary>
    /// The unique identifier for this cache refresher.
    /// </summary>
    public static readonly Guid UniqueId = Guid.Parse("317F1A0A-1131-4F2B-A9E8-6287C49C6B38");

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionCacheRefresher"/> class.
    /// </summary>
    /// <param name="appCaches">Umbraco's application caches.</param>
    /// <param name="eventAggregator">The event aggregator used to publish the refresh notification.</param>
    /// <param name="factory">The cache refresher notification factory.</param>
    public MediaExceptionCacheRefresher(AppCaches appCaches, IEventAggregator eventAggregator, ICacheRefresherNotificationFactory factory)
        : base(appCaches, eventAggregator, factory)
    {
    }

    /// <inheritdoc />
    public override Guid RefresherUniqueId => UniqueId;

    /// <inheritdoc />
    public override string Name => "Media Exception Cache Refresher";

    /// <inheritdoc />
    public override void Refresh(int id) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Refresh(Guid id) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Remove(int id) => throw new NotSupportedException();
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionCacheRefresherTests"`
Expected: PASS (3 tests)

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCacheRefresherNotification.cs src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/MediaExceptionCacheRefresher.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/MediaExceptionCacheRefresherTests.cs
git commit -m "feat: add MediaExceptionCacheRefresher for full-rebuild cache invalidation"
```

---

### Task 3: Async rebuild notification handlers (startup + cache refresh)

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/Notifications/RebuildMediaExceptionCacheOnStartup.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/Notifications/RebuildMediaExceptionCacheOnRefresh.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/Notifications/RebuildMediaExceptionCacheOnStartupTests.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/Notifications/RebuildMediaExceptionCacheOnRefreshTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionCache.RebuildAsync() : Task` (Task 1), `MediaExceptionCacheRefresherNotification` (Task 2), Umbraco's `INotificationAsyncHandler<TNotification>`, `UmbracoApplicationStartedNotification(bool isRestarting)`.
- Produces: `RebuildMediaExceptionCacheOnStartup`, `RebuildMediaExceptionCacheOnRefresh` — registered as notification handlers in Task 7.

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Caching.Notifications;

/// <summary>
/// Unit tests for <see cref="RebuildMediaExceptionCacheOnStartup"/>.
/// </summary>
[TestFixture]
internal sealed class RebuildMediaExceptionCacheOnStartupTests
{
    /// <summary>
    /// Tests that handling the startup notification triggers a cache rebuild.
    /// </summary>
    [Test]
    public async Task HandleAsync_Calls_RebuildAsync()
    {
        var cacheMock = new Mock<IMediaExceptionCache>();
        var handler = new RebuildMediaExceptionCacheOnStartup(cacheMock.Object, NullLogger<RebuildMediaExceptionCacheOnStartup>.Instance);

        await handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);

        cacheMock.Verify(c => c.RebuildAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that a failed rebuild is swallowed rather than propagating and crashing startup.
    /// </summary>
    [Test]
    public void HandleAsync_Does_Not_Throw_When_RebuildAsync_Fails()
    {
        var cacheMock = new Mock<IMediaExceptionCache>();
        cacheMock.Setup(c => c.RebuildAsync()).ThrowsAsync(new InvalidOperationException("db down"));

        var handler = new RebuildMediaExceptionCacheOnStartup(cacheMock.Object, NullLogger<RebuildMediaExceptionCacheOnStartup>.Instance);

        Assert.DoesNotThrowAsync(async () => await handler.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None));
    }
}
```

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Caching.Notifications;

/// <summary>
/// Unit tests for <see cref="RebuildMediaExceptionCacheOnRefresh"/>.
/// </summary>
[TestFixture]
internal sealed class RebuildMediaExceptionCacheOnRefreshTests
{
    /// <summary>
    /// Tests that handling the refresh notification triggers a cache rebuild.
    /// </summary>
    [Test]
    public async Task HandleAsync_Calls_RebuildAsync()
    {
        var cacheMock = new Mock<IMediaExceptionCache>();
        var handler = new RebuildMediaExceptionCacheOnRefresh(cacheMock.Object, NullLogger<RebuildMediaExceptionCacheOnRefresh>.Instance);

        var notification = new MediaExceptionCacheRefresherNotification(string.Empty, MessageType.RefreshAll);

        await handler.HandleAsync(notification, CancellationToken.None);

        cacheMock.Verify(c => c.RebuildAsync(), Times.Once);
    }

    /// <summary>
    /// Tests that a failed rebuild is swallowed rather than propagating out of the notification pipeline.
    /// </summary>
    [Test]
    public void HandleAsync_Does_Not_Throw_When_RebuildAsync_Fails()
    {
        var cacheMock = new Mock<IMediaExceptionCache>();
        cacheMock.Setup(c => c.RebuildAsync()).ThrowsAsync(new InvalidOperationException("db down"));

        var handler = new RebuildMediaExceptionCacheOnRefresh(cacheMock.Object, NullLogger<RebuildMediaExceptionCacheOnRefresh>.Instance);

        var notification = new MediaExceptionCacheRefresherNotification(string.Empty, MessageType.RefreshAll);

        Assert.DoesNotThrowAsync(async () => await handler.HandleAsync(notification, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~RebuildMediaExceptionCache"`
Expected: build FAILS — handler classes do not exist yet.

- [ ] **Step 3: Implement `RebuildMediaExceptionCacheOnStartup`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;

/// <summary>
/// Builds the media exception cache once on application startup.
/// </summary>
internal sealed class RebuildMediaExceptionCacheOnStartup : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IMediaExceptionCache mediaExceptionCache;
    private readonly ILogger<RebuildMediaExceptionCacheOnStartup> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RebuildMediaExceptionCacheOnStartup"/> class.
    /// </summary>
    /// <param name="mediaExceptionCache">The media exception cache to build.</param>
    /// <param name="logger">The logger.</param>
    public RebuildMediaExceptionCacheOnStartup(IMediaExceptionCache mediaExceptionCache, ILogger<RebuildMediaExceptionCacheOnStartup> logger)
    {
        this.mediaExceptionCache = mediaExceptionCache;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await this.mediaExceptionCache.RebuildAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to build the media exception cache on startup.");
        }
    }
}
```

- [ ] **Step 4: Implement `RebuildMediaExceptionCacheOnRefresh`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;

/// <summary>
/// Rebuilds the media exception cache whenever it is invalidated.
/// </summary>
internal sealed class RebuildMediaExceptionCacheOnRefresh : INotificationAsyncHandler<MediaExceptionCacheRefresherNotification>
{
    private readonly IMediaExceptionCache mediaExceptionCache;
    private readonly ILogger<RebuildMediaExceptionCacheOnRefresh> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RebuildMediaExceptionCacheOnRefresh"/> class.
    /// </summary>
    /// <param name="mediaExceptionCache">The media exception cache to rebuild.</param>
    /// <param name="logger">The logger.</param>
    public RebuildMediaExceptionCacheOnRefresh(IMediaExceptionCache mediaExceptionCache, ILogger<RebuildMediaExceptionCacheOnRefresh> logger)
    {
        this.mediaExceptionCache = mediaExceptionCache;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(MediaExceptionCacheRefresherNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await this.mediaExceptionCache.RebuildAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to rebuild the media exception cache after invalidation.");
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~RebuildMediaExceptionCache"`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/Notifications/RebuildMediaExceptionCacheOnStartup.cs src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Caching/Notifications/RebuildMediaExceptionCacheOnRefresh.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/Notifications/RebuildMediaExceptionCacheOnStartupTests.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Caching/Notifications/RebuildMediaExceptionCacheOnRefreshTests.cs
git commit -m "feat: rebuild media exception cache asynchronously on startup and refresh"
```

---

### Task 4: Wire `OptimizedImageUrlGenerator` to apply media exceptions

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/OptimizedImageUrlGenerator.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/OptimizedImageUrlGeneratorTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionCache.GetByImageUrl(string) : MediaException?` (Task 1).
- Produces: no new public surface; `OptimizedImageUrlGenerator`'s constructor gains a third parameter, consumed by Task 7's DI registration update.

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="OptimizedImageUrlGenerator"/>.
/// </summary>
[TestFixture]
internal sealed class OptimizedImageUrlGeneratorTests
{
    private Mock<IImageUrlGenerator> innerGeneratorMock = null!;
    private Mock<IMediaExceptionCache> mediaExceptionCacheMock = null!;
    private PageSpeedOptimizerSettings settings = null!;
    private OptimizedImageUrlGenerator generator = null!;

    /// <summary>
    /// Sets up a fresh generator with a pass-through inner generator before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.innerGeneratorMock = new Mock<IImageUrlGenerator>();
        this.innerGeneratorMock
            .Setup(g => g.GetImageUrl(It.IsAny<ImageUrlGenerationOptions>()))
            .Returns((ImageUrlGenerationOptions o) => o.ImageUrl);

        this.mediaExceptionCacheMock = new Mock<IMediaExceptionCache>();

        this.settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { Enabled = true, DefaultImageQuality = 85, ForceWebP = false },
        };

        var optionsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        optionsMock.SetupGet(o => o.Value).Returns(this.settings);

        this.generator = new OptimizedImageUrlGenerator(this.innerGeneratorMock.Object, optionsMock.Object, this.mediaExceptionCacheMock.Object);
    }

    /// <summary>
    /// Tests that a matching exception's quality is used instead of the global default.
    /// </summary>
    [Test]
    public void GetImageUrl_Uses_Exception_Quality_Instead_Of_Global_Default()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 40, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(40));
    }

    /// <summary>
    /// Tests that the global default quality is used when no exception matches.
    /// </summary>
    [Test]
    public void GetImageUrl_Uses_Global_Default_Quality_When_No_Exception_Matches()
    {
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl(It.IsAny<string>())).Returns((MediaException?)null);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(85));
    }

    /// <summary>
    /// Tests that an explicitly requested quality (already set by the caller) is never overridden, even when an exception matches.
    /// </summary>
    [Test]
    public void GetImageUrl_Does_Not_Override_Explicitly_Requested_Quality()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 40, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg") { Quality = 95 };

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(95));
    }

    /// <summary>
    /// Tests that an exception forcing WebP wins even when the global setting does not force it.
    /// </summary>
    [Test]
    public void GetImageUrl_Forces_Webp_When_Exception_ForceWebp_Is_True_Even_If_Global_Setting_Is_False()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = true };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.FurtherOptions, Is.EqualTo("format=webp"));
    }

    /// <summary>
    /// Tests that an exception opting out of WebP wins even when the global setting forces it.
    /// </summary>
    [Test]
    public void GetImageUrl_Does_Not_Force_Webp_When_Exception_ForceWebp_Is_False_Even_If_Global_Setting_Is_True()
    {
        this.settings.ImageOptimization.ForceWebP = true;

        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.FurtherOptions, Is.Not.EqualTo("format=webp"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~OptimizedImageUrlGeneratorTests"`
Expected: build FAILS — `OptimizedImageUrlGenerator` constructor does not accept a third `IMediaExceptionCache` argument yet.

- [ ] **Step 3: Update `OptimizedImageUrlGenerator`**

Replace the full contents of `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/OptimizedImageUrlGenerator.cs` with:

```csharp
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure;

/// <summary>
/// Image url generator that sets quality and forces webp format, applying per-media exceptions where they exist.
/// </summary>
/// <param name="innerGenerator">The existing umbraco generator that we decorate in this one.</param>
/// <param name="options">The page speed optimizer settings.</param>
/// <param name="mediaExceptionCache">The cache of per-media quality/WebP overrides.</param>
internal sealed class OptimizedImageUrlGenerator(IImageUrlGenerator innerGenerator, IOptions<PageSpeedOptimizerSettings> options, IMediaExceptionCache mediaExceptionCache)
    : IImageUrlGenerator
{
    private ImageOptimizationSettings settings = options.Value.ImageOptimization;

    private const string FormatParam = "format";

    /// <inheritdoc />
    public IEnumerable<string> SupportedImageFileTypes => innerGenerator.SupportedImageFileTypes;

    /// <inheritdoc />
    public string? GetImageUrl(ImageUrlGenerationOptions options)
    {
        if (this.settings.Enabled == false)
        {
            return innerGenerator.GetImageUrl(options);
        }

        if (string.IsNullOrWhiteSpace(options.ImageUrl))
        {
            return innerGenerator.GetImageUrl(options);
        }

        var exception = mediaExceptionCache.GetByImageUrl(options.ImageUrl);

        this.TrySetWebpFormat(options, exception);

        if (options.Quality.HasValue)
        {
            return innerGenerator.GetImageUrl(options);
        }

        options.Quality = exception?.Quality ?? this.settings.DefaultImageQuality;

        return innerGenerator.GetImageUrl(options);
    }

    private void TrySetWebpFormat(ImageUrlGenerationOptions options, MediaException? exception)
    {
        if (string.IsNullOrWhiteSpace(options.FurtherOptions) == false &&
            options.FurtherOptions.Contains(FormatParam))
        {
            return;
        }

        var forceWebP = exception?.ForceWebp ?? this.settings.ForceWebP;

        if (forceWebP == false)
        {
            return;
        }

        options.FurtherOptions = $"{FormatParam}=webp";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~OptimizedImageUrlGeneratorTests"`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/OptimizedImageUrlGenerator.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/OptimizedImageUrlGeneratorTests.cs
git commit -m "feat: apply media exceptions in OptimizedImageUrlGenerator"
```

---

### Task 5: `IMediaExceptionService` and `MediaExceptionService`

**Files:**
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Core/Services/IMediaExceptionService.cs`
- Create: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Services/MediaExceptionService.cs`
- Test: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Services/MediaExceptionServiceTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionRepository` (existing, all 6 methods), `MediaExceptionCacheRefresher.UniqueId : Guid` (Task 2), Umbraco's `DistributedCache.RefreshAll(Guid refresherGuid)`.
- Produces: `IMediaExceptionService` with the same 6-method surface as `IMediaExceptionRepository` (`GetAsync`, `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `GetByMediaKeyAsync`, `DeleteAsync`) — consumed by Task 6 (`MediaExceptionManagementApiController`).

- [ ] **Step 1: Write the `IMediaExceptionService` interface**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Core.Services;

/// <summary>
/// Manages <see cref="MediaException"/> entities and keeps the media exception cache in sync with writes.
/// </summary>
public interface IMediaExceptionService
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
    /// Creates a new <see cref="MediaException"/> and refreshes the media exception cache.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created entity (with any DB-assigned values populated).</returns>
    Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing <see cref="MediaException"/> and refreshes the media exception cache.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated entity.</returns>
    Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Gets a <see cref="MediaException"/> by its media key.
    /// </summary>
    /// <param name="mediaKey">The media key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or <c>null</c> if not found.</returns>
    Task<MediaException?> GetByMediaKeyAsync(Guid mediaKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes a <see cref="MediaException"/> by its identifier and refreshes the media exception cache.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Services;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MediaExceptionService"/>.
/// </summary>
[TestFixture]
internal sealed class MediaExceptionServiceTests
{
    private Mock<IMediaExceptionRepository> repositoryMock = null!;
    private Mock<IServerMessenger> serverMessengerMock = null!;
    private MediaExceptionCacheRefresher refresher = null!;
    private DistributedCache distributedCache = null!;
    private MediaExceptionService service = null!;

    /// <summary>
    /// Sets up a service backed by a real DistributedCache (sealed, cannot be mocked) wired to a mocked server messenger.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.repositoryMock = new Mock<IMediaExceptionRepository>();
        this.serverMessengerMock = new Mock<IServerMessenger>();

        var appCaches = new AppCaches(new ObjectCacheAppCache(), Mock.Of<IRequestCache>(), new IsolatedCaches(_ => new ObjectCacheAppCache()));
        this.refresher = new MediaExceptionCacheRefresher(appCaches, Mock.Of<IEventAggregator>(), Mock.Of<ICacheRefresherNotificationFactory>());

        var cacheRefresherCollection = new CacheRefresherCollection(() => new ICacheRefresher[] { this.refresher });
        this.distributedCache = new DistributedCache(this.serverMessengerMock.Object, cacheRefresherCollection);

        this.service = new MediaExceptionService(this.repositoryMock.Object, this.distributedCache);
    }

    /// <summary>
    /// Tests that creating an exception triggers a distributed cache refresh.
    /// </summary>
    [Test]
    public async Task CreateAsync_Triggers_DistributedCache_RefreshAll()
    {
        var entity = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 70, ForceWebp = false };
        this.repositoryMock.Setup(r => r.CreateAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await this.service.CreateAsync(entity, CancellationToken.None);

        this.serverMessengerMock.Verify(m => m.QueueRefreshAll(this.refresher), Times.Once);
    }

    /// <summary>
    /// Tests that updating an exception triggers a distributed cache refresh.
    /// </summary>
    [Test]
    public async Task UpdateAsync_Triggers_DistributedCache_RefreshAll()
    {
        var entity = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 70, ForceWebp = false };
        this.repositoryMock.Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await this.service.UpdateAsync(entity, CancellationToken.None);

        this.serverMessengerMock.Verify(m => m.QueueRefreshAll(this.refresher), Times.Once);
    }

    /// <summary>
    /// Tests that deleting an exception triggers a distributed cache refresh.
    /// </summary>
    [Test]
    public async Task DeleteAsync_Triggers_DistributedCache_RefreshAll()
    {
        var id = Guid.NewGuid();
        this.repositoryMock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await this.service.DeleteAsync(id, CancellationToken.None);

        this.serverMessengerMock.Verify(m => m.QueueRefreshAll(this.refresher), Times.Once);
    }

    /// <summary>
    /// Tests that read operations pass straight through to the repository without touching the distributed cache.
    /// </summary>
    [Test]
    public async Task GetByMediaKeyAsync_Passes_Through_To_Repository_Without_Refreshing_Cache()
    {
        var mediaKey = Guid.NewGuid();
        var entity = new MediaException { Id = Guid.NewGuid(), MediaKey = mediaKey, Quality = 70, ForceWebp = false };
        this.repositoryMock.Setup(r => r.GetByMediaKeyAsync(mediaKey, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await this.service.GetByMediaKeyAsync(mediaKey, CancellationToken.None);

        Assert.That(result, Is.EqualTo(entity));
        this.serverMessengerMock.Verify(m => m.QueueRefreshAll(It.IsAny<ICacheRefresher>()), Times.Never);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionServiceTests"`
Expected: build FAILS — `MediaExceptionService` does not exist yet.

- [ ] **Step 4: Implement `MediaExceptionService`**

```csharp
// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Core.Cache;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Core.Services;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Services;

/// <summary>
/// EF Core repository-backed implementation of <see cref="IMediaExceptionService"/> that refreshes the media exception cache after every write.
/// </summary>
internal sealed class MediaExceptionService : IMediaExceptionService
{
    private readonly IMediaExceptionRepository repository;
    private readonly DistributedCache distributedCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionService"/> class.
    /// </summary>
    /// <param name="repository">The media exception repository.</param>
    /// <param name="distributedCache">Umbraco's distributed cache entry point.</param>
    public MediaExceptionService(IMediaExceptionRepository repository, DistributedCache distributedCache)
    {
        this.repository = repository;
        this.distributedCache = distributedCache;
    }

    /// <inheritdoc />
    public Task<MediaException?> GetAsync(Guid id, CancellationToken ct = default) => this.repository.GetAsync(id, ct);

    /// <inheritdoc />
    public Task<IEnumerable<MediaException>> GetAllAsync(CancellationToken ct = default) => this.repository.GetAllAsync(ct);

    /// <inheritdoc />
    public Task<MediaException?> GetByMediaKeyAsync(Guid mediaKey, CancellationToken ct = default) => this.repository.GetByMediaKeyAsync(mediaKey, ct);

    /// <inheritdoc />
    public async Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default)
    {
        var created = await this.repository.CreateAsync(entity, ct);
        this.distributedCache.RefreshAll(MediaExceptionCacheRefresher.UniqueId);
        return created;
    }

    /// <inheritdoc />
    public async Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default)
    {
        var updated = await this.repository.UpdateAsync(entity, ct);
        this.distributedCache.RefreshAll(MediaExceptionCacheRefresher.UniqueId);
        return updated;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await this.repository.DeleteAsync(id, ct);
        this.distributedCache.RefreshAll(MediaExceptionCacheRefresher.UniqueId);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~MediaExceptionServiceTests"`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Core/Services/IMediaExceptionService.cs src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Services/MediaExceptionService.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/Services/MediaExceptionServiceTests.cs
git commit -m "feat: add MediaExceptionService to trigger cache refresh on writes"
```

---

### Task 6: Update `MediaExceptionManagementApiController` to use `IMediaExceptionService`

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`
- Modify: `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs`

**Interfaces:**
- Consumes: `IMediaExceptionService` (Task 5) — identical method surface to the `IMediaExceptionRepository` the controller used before, so call sites are unchanged, only the type and field name change.

- [ ] **Step 1: Update the controller test to mock `IMediaExceptionService` instead of `IMediaExceptionRepository`**

In `src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs`:

1. Change the using statement `using Umbraco.Community.PagespeedOptimizer.Core.Repositories;` to `using Umbraco.Community.PagespeedOptimizer.Core.Services;`.
2. Rename the field `private Mock<IMediaExceptionRepository> repositoryMock = null!;` to `private Mock<IMediaExceptionService> serviceMock = null!;`.
3. In `SetUp`, replace:

```csharp
this.repositoryMock = new Mock<IMediaExceptionRepository>();
```

with:

```csharp
this.serviceMock = new Mock<IMediaExceptionService>();
```

and replace the controller construction line:

```csharp
this.controller = new MediaExceptionManagementApiController(this.repositoryMock.Object, this.settingsMock.Object);
```

with:

```csharp
this.controller = new MediaExceptionManagementApiController(this.serviceMock.Object, this.settingsMock.Object);
```

4. Throughout the rest of the file, replace every remaining occurrence of `this.repositoryMock` with `this.serviceMock` (the method names being mocked — `GetByMediaKeyAsync`, `CreateAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync` — are unchanged, since `IMediaExceptionService` mirrors `IMediaExceptionRepository`'s signatures exactly).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ --filter "FullyQualifiedName~MediaExceptionManagementApiControllerTests"`
Expected: build FAILS — the controller's constructor still takes `IMediaExceptionRepository`.

- [ ] **Step 3: Update the controller**

In `src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs`:

1. Change the using statement `using Umbraco.Community.PagespeedOptimizer.Core.Repositories;` to `using Umbraco.Community.PagespeedOptimizer.Core.Services;`.
2. Rename the field `private readonly IMediaExceptionRepository repository;` to `private readonly IMediaExceptionService service;`.
3. Update the constructor:

```csharp
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionManagementApiController"/> class.
    /// </summary>
    /// <param name="service">The media exception service.</param>
    /// <param name="settings">The page speed optimizer settings.</param>
    public MediaExceptionManagementApiController(
        IMediaExceptionService service,
        IOptions<PageSpeedOptimizerSettings> settings)
    {
        this.service = service;
        this.settings = settings;
    }
```

4. Replace every remaining `this.repository.` with `this.service.` in `CreateMediaException`, `UpdateMediaException`, `DeleteMediaException`, and `GetByMediaKey`. No other logic changes — method names and signatures on `IMediaExceptionService` match `IMediaExceptionRepository` exactly.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/ --filter "FullyQualifiedName~MediaExceptionManagementApiControllerTests"`
Expected: PASS (all existing tests, unchanged assertions)

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.BackOffice/Controllers/MediaExceptionManagementApiController.cs src/test/Umbraco.Community.PagespeedOptimizer.BackOffice.Tests/Controllers/MediaExceptionManagementApiControllerTests.cs
git commit -m "refactor: route MediaExceptionManagementApiController writes through IMediaExceptionService"
```

---

### Task 7: DI wiring — Singleton repository, cache/service registration, refresher, notification handlers

**Files:**
- Modify: `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs`
- Modify: `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs`

**Interfaces:**
- Consumes: everything produced by Tasks 1–5 (`MediaExceptionCache`, `MediaExceptionCacheRefresher`, `RebuildMediaExceptionCacheOnStartup`, `RebuildMediaExceptionCacheOnRefresh`, `MediaExceptionService`).
- Produces: final registration graph — no further tasks depend on this one.

- [ ] **Step 1: Update the existing repository-lifetime test and add new registration tests**

In `src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs`:

1. Rename the test method `IMediaExceptionRepository_Should_Be_Registered_As_Scoped` to `IMediaExceptionRepository_Should_Be_Registered_As_Singleton` and change its assertion:

```csharp
    /// <summary>
    /// Tests that <see cref="IMediaExceptionRepository"/> is registered as a singleton service.
    /// </summary>
    [Test]
    public void IMediaExceptionRepository_Should_Be_Registered_As_Singleton()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        Assert.That(
            this.serviceCollection.Any(x =>
                x.ServiceType == typeof(IMediaExceptionRepository) &&
                x.Lifetime == ServiceLifetime.Singleton),
            Is.True);
    }
```

2. Add two new tests immediately after it (add `using Umbraco.Community.PagespeedOptimizer.Core.Caching;` and `using Umbraco.Community.PagespeedOptimizer.Core.Services;` to the top of the file):

```csharp
    /// <summary>
    /// Tests that <see cref="IMediaExceptionCache"/> is registered as a singleton service.
    /// </summary>
    [Test]
    public void IMediaExceptionCache_Should_Be_Registered_As_Singleton()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        Assert.That(
            this.serviceCollection.Any(x =>
                x.ServiceType == typeof(IMediaExceptionCache) &&
                x.Lifetime == ServiceLifetime.Singleton),
            Is.True);
    }

    /// <summary>
    /// Tests that <see cref="IMediaExceptionService"/> is registered as a singleton service.
    /// </summary>
    [Test]
    public void IMediaExceptionService_Should_Be_Registered_As_Singleton()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        Assert.That(
            this.serviceCollection.Any(x =>
                x.ServiceType == typeof(IMediaExceptionService) &&
                x.Lifetime == ServiceLifetime.Singleton),
            Is.True);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~InfrastructureComposerTests"`
Expected: the renamed test FAILS (repository is still Scoped) and the two new tests FAIL (nothing registered yet).

- [ ] **Step 3: Update `UmbracoBuilderExtensions`**

In `src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs`:

1. Add these using statements alongside the existing ones:

```csharp
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Services;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Services;
```

2. Update the `AddPagespeedOptimizer` chain to call the new registration method:

```csharp
    public static IUmbracoBuilder AddPagespeedOptimizer(this IUmbracoBuilder builder) =>
        builder
            .LoadConfiguration()
            .AddStaticCache()
            .AddResponseCompression()
            .AddOptimizedImageUrlGenerator()
            .AddMediaExceptionPersistence()
            .AddMediaExceptionCaching();
```

3. In `AddMediaExceptionPersistence`, change the repository registration from `AddScoped` to `AddSingleton`:

```csharp
        builder.Services.AddSingleton<IMediaExceptionRepository, MediaExceptionRepository>();
```

4. Add a new private method, after `AddMediaExceptionPersistence`:

```csharp
    /// <summary>
    /// Registers the media exception cache, the service that keeps it in sync with writes, the cache refresher, and the notification handlers that rebuild it asynchronously.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    private static IUmbracoBuilder AddMediaExceptionCaching(this IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IMediaExceptionCache, MediaExceptionCache>();
        builder.Services.AddSingleton<IMediaExceptionService, MediaExceptionService>();

        builder.CacheRefreshers().Add<MediaExceptionCacheRefresher>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RebuildMediaExceptionCacheOnStartup>();
        builder.AddNotificationAsyncHandler<MediaExceptionCacheRefresherNotification, RebuildMediaExceptionCacheOnRefresh>();

        return builder;
    }
```

5. Update the `AddOptimizedImageUrlGenerator` factory to resolve and pass `IMediaExceptionCache`:

```csharp
        builder.Services.AddSingleton<IImageUrlGenerator>(provider =>
        {
            var inner = (IImageUrlGenerator)ActivatorUtilities.CreateInstance(provider, imageSharpGenerator.ImplementationType);

            var options = provider.GetRequiredService<IOptions<PageSpeedOptimizerSettings>>();
            var mediaExceptionCache = provider.GetRequiredService<IMediaExceptionCache>();

            return new OptimizedImageUrlGenerator(inner, options, mediaExceptionCache);
        });
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/ --filter "FullyQualifiedName~InfrastructureComposerTests"`
Expected: PASS (all tests, including the 2 new ones and the renamed one)

- [ ] **Step 5: Commit**

```bash
git add src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/Extensions/UmbracoBuilderExtensions.cs src/test/Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests/InfrastructureComposerTests.cs
git commit -m "feat: wire media exception cache, service, refresher, and rebuild handlers into DI"
```

---

### Task 8: Full solution verification

**Files:** none (verification only).

**Interfaces:** none — this task only runs the existing build and test suite end-to-end.

- [ ] **Step 1: Full restore and build**

Run: `dotnet restore src/ && dotnet build -c Release --no-restore src/`
Expected: Build succeeds with 0 warnings and 0 errors (StyleCop.Analyzers runs warnings-as-errors — any missing XML doc, misplaced `using`, or missing copyright header fails the build here).

- [ ] **Step 2: Full test suite**

Run: `dotnet test -c Release --no-restore --no-build src/`
Expected: All tests pass, including every test added in Tasks 1–7 and every pre-existing test (`InfrastructureComposerTests`, `StaticFileOptionsConfigurationTests`, `PageSpeedOptimizerDbContextTests`, `MediaExceptionManagementApiControllerTests`).

- [ ] **Step 3: Note manual verification gap**

The DI wiring for `builder.CacheRefreshers().Add<MediaExceptionCacheRefresher>()` is exercised by Task 7's tests only insofar as it doesn't throw during composition — Umbraco's `CacheRefresherCollectionBuilder` doesn't surface appended types as inspectable `IServiceCollection` entries, so there is no reliable unit-test assertion for "the refresher is registered with Umbraco's cache refresher collection." Record this as a manual check: after implementation, run the package against `test-sites/Website-V17/`, create/update/delete a media exception via the back-office workspace view, and confirm (via a breakpoint or log line temporarily added to `RebuildMediaExceptionCacheOnRefresh`) that the cache rebuild fires. Remove any temporary debugging aids before considering the branch done.

- [ ] **Step 4: Commit (only if Step 3's manual check required code changes; otherwise skip)**

If no changes were needed, there is nothing to commit — the plan is complete after Task 7's commit.

---

## Self-Review Notes

- **Spec coverage:** every component in the spec (`IMediaExceptionCache`/`MediaExceptionCache`, `OptimizedImageUrlGenerator` update, startup + refresh rebuild triggers, `MediaExceptionCacheRefresher`, Singleton repository registration, `IMediaExceptionService`/`MediaExceptionService`, controller update) maps to Tasks 1–7. Error handling (skip unresolved media keys, swallow rebuild failures) is covered in Task 1 Step 2's third test and Task 3's tests. Testing section of the spec is covered by the corresponding task's test file.
- **Type consistency:** `IMediaExceptionCache.GetByImageUrl`/`RebuildAsync` (Task 1) are used with identical signatures in Task 3 (handlers) and Task 4 (`OptimizedImageUrlGenerator`). `MediaExceptionCacheRefresher.UniqueId` (Task 2) is used identically in Task 5 (`MediaExceptionService`) and Task 7 (registration, implicitly via the refresher type). `IMediaExceptionService`'s 6 methods (Task 5) match `IMediaExceptionRepository`'s signatures exactly, and Task 6 relies on that match holding.
- **No placeholders:** all code blocks are complete and compilable given the constructor/interface shapes verified directly against the Umbraco 17.4.2 source during planning (`IPublishedUrlProvider.GetMediaUrl`, `IAppPolicyCache.Get`/`Insert`, `CacheRefresherBase<T>`, `DistributedCache.RefreshAll`, `IServerMessenger.QueueRefreshAll`, `UmbracoContextReference`'s public constructor).
