// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Notifications;

/// <summary>
/// Runs any pending EF Core migrations for the PageSpeed Optimizer on application startup.
/// </summary>
internal sealed class RunMediaExceptionsMigration : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly PageSpeedOptimizerDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunMediaExceptionsMigration"/> class.
    /// </summary>
    /// <param name="dbContext">The PageSpeed Optimizer database context.</param>
    public RunMediaExceptionsMigration(PageSpeedOptimizerDbContext dbContext)
        => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        => await _dbContext.Database.MigrateAsync(cancellationToken);
}
