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
