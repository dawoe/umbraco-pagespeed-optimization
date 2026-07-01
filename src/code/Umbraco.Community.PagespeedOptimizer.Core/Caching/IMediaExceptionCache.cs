// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Core.Caching;

/// <summary>
/// Provides fast, non-blocking lookup of <see cref="MediaException"/> entities by the image URL they apply to.
/// </summary>
public interface IMediaExceptionCache
{
    /// <summary>
    /// Gets the <see cref="MediaException"/> that applies to the given image URL, if any.
    /// </summary>
    /// <param name="imageUrl">The image URL, as produced by Umbraco's image URL generation pipeline.</param>
    /// <returns>The matching <see cref="MediaException"/>, or <c>null</c> if none matches or the cache has not been built yet.</returns>
    /// <remarks>This is a synchronous, non-blocking read. It never triggers a rebuild.</remarks>
    MediaException? GetByImageUrl(string imageUrl);

    /// <summary>
    /// Rebuilds the cached URL-to-exception map from the database.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>Must only be called from asynchronous entry points (startup, cache refresh notifications) — never from a synchronous hot path.</remarks>
    Task RebuildAsync();
}
