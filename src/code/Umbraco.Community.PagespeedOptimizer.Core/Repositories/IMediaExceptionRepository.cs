// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Core.Repositories;

/// <summary>
/// Defines CRUD operations for <see cref="MediaException"/> entities.
/// </summary>
public interface IMediaExceptionRepository
{
    /// <summary>
    /// Gets a <see cref="MediaException"/> by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or <c>null</c> if not found.</returns>
    Task<MediaException?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all <see cref="MediaException"/> entities.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All stored exceptions.</returns>
    Task<IEnumerable<MediaException>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new <see cref="MediaException"/>.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created entity (with any DB-assigned values populated).</returns>
    Task<MediaException> CreateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing <see cref="MediaException"/>.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated entity.</returns>
    Task<MediaException> UpdateAsync(MediaException entity, CancellationToken ct = default);

    /// <summary>
    /// Gets a <see cref="MediaException"/> by its media key.
    /// </summary>
    /// <param name="mediaKey">The media key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or <c>null</c> if not found.</returns>
    Task<MediaException?> GetByMediaKeyAsync(Guid mediaKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes a <see cref="MediaException"/> by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
