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
