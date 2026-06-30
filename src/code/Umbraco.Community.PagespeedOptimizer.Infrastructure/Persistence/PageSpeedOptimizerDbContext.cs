// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the PageSpeed Optimizer custom tables.
/// </summary>
internal sealed class PageSpeedOptimizerDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageSpeedOptimizerDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public PageSpeedOptimizerDbContext(DbContextOptions<PageSpeedOptimizerDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the media exceptions.
    /// </summary>
    public DbSet<MediaException> MediaExceptions { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaException>(entity =>
        {
            entity.ToTable("PageSpeedOptimizer_MediaExceptions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.MediaKey).IsRequired();
            entity.Property(e => e.Quality).IsRequired();
            entity.Property(e => e.ForceWebp).IsRequired();
        });
    }
}
