// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Persistence.EFCore.Scoping;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IMediaExceptionRepository"/>.
/// </summary>
internal sealed class MediaExceptionRepository : IMediaExceptionRepository
{
    private readonly IEFCoreScopeProvider<PageSpeedOptimizerDbContext> _scopeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionRepository"/> class.
    /// </summary>
    /// <param name="scopeProvider">The EF Core scope provider.</param>
    public MediaExceptionRepository(IEFCoreScopeProvider<PageSpeedOptimizerDbContext> scopeProvider)
        => _scopeProvider = scopeProvider;

    /// <inheritdoc />
    public async Task<MediaException?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException? result = await scope.ExecuteWithContextAsync(
            async (PageSpeedOptimizerDbContext db) => await db.MediaExceptions.FindAsync([id], ct));

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaException>> GetAllAsync(CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        List<MediaException> result = await scope.ExecuteWithContextAsync(
            async (PageSpeedOptimizerDbContext db) => await db.MediaExceptions.ToListAsync(ct));

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException result = await scope.ExecuteWithContextAsync(async (PageSpeedOptimizerDbContext db) =>
        {
            db.MediaExceptions.Add(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        });

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        MediaException result = await scope.ExecuteWithContextAsync(async (PageSpeedOptimizerDbContext db) =>
        {
            db.MediaExceptions.Update(entity);
            await db.SaveChangesAsync(ct);
            return entity;
        });

        scope.Complete();

        return result;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using IEfCoreScope<PageSpeedOptimizerDbContext> scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<Task>(async (PageSpeedOptimizerDbContext db) =>
        {
            MediaException? entity = await db.MediaExceptions.FindAsync([id], ct);

            if (entity is not null)
            {
                db.MediaExceptions.Remove(entity);
                await db.SaveChangesAsync(ct);
            }
        });

        scope.Complete();
    }
}
