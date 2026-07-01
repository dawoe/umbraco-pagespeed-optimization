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
