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
