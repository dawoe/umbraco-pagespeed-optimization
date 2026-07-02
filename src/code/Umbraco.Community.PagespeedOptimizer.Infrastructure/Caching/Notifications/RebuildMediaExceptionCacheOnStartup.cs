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
