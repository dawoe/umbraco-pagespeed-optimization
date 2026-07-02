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
