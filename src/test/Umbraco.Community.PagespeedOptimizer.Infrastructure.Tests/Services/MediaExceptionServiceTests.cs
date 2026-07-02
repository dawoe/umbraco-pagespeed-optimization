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
