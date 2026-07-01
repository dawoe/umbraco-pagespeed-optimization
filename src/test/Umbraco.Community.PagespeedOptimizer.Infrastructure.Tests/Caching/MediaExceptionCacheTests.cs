// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Moq;
using Umbraco.Cms.Core;
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
