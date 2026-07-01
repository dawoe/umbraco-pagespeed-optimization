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
