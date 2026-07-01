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

        cacheMock.Verify(c => c.RebuildAsync(), Times.Once);
    }
}
