// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.Core.Models;

/// <summary>
/// Represents a per-media exception overriding the global image optimization settings.
/// </summary>
public sealed class MediaException
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco media key this exception applies to.
    /// </summary>
    public Guid MediaKey { get; set; }

    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force WebP for this media item.
    /// </summary>
    public bool ForceWebp { get; set; }
}
