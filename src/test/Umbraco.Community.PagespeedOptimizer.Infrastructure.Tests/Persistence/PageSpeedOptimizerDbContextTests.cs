// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="PageSpeedOptimizerDbContext"/> model configuration.
/// </summary>
[TestFixture]
internal sealed class PageSpeedOptimizerDbContextTests
{
    private PageSpeedOptimizerDbContext dbContext = null!;

    /// <summary>
    /// Sets up an in-memory DbContext before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<PageSpeedOptimizerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        this.dbContext = new PageSpeedOptimizerDbContext(options);
    }

    /// <summary>
    /// Tears down the DbContext after each test.
    /// </summary>
    [TearDown]
    public void TearDown() => this.dbContext.Dispose();

    /// <summary>
    /// Verifies that <see cref="MediaException"/> maps to the correct table name.
    /// </summary>
    [Test]
    public void MediaException_Should_Map_To_Correct_Table_Name()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));

        Assert.That(entityType?.GetTableName(), Is.EqualTo("PageSpeedOptimizer_MediaExceptions"));
    }

    /// <summary>
    /// Verifies that the primary key is configured on the Id property.
    /// </summary>
    [Test]
    public void MediaException_Should_Have_Id_As_Primary_Key()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var primaryKey = entityType?.FindPrimaryKey();
        var keyProperty = primaryKey?.Properties.SingleOrDefault();

        Assert.That(keyProperty?.Name, Is.EqualTo(nameof(MediaException.Id)));
    }

    /// <summary>
    /// Verifies that MediaKey is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_MediaKey_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.MediaKey));

        Assert.That(property?.IsNullable, Is.False);
    }

    /// <summary>
    /// Verifies that Quality is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_Quality_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.Quality));

        Assert.That(property?.IsNullable, Is.False);
    }

    /// <summary>
    /// Verifies that ForceWebp is configured as required (non-nullable).
    /// </summary>
    [Test]
    public void MediaException_ForceWebp_Should_Be_Required()
    {
        var entityType = this.dbContext.Model.FindEntityType(typeof(MediaException));
        var property = entityType?.FindProperty(nameof(MediaException.ForceWebp));

        Assert.That(property?.IsNullable, Is.False);
    }
}
