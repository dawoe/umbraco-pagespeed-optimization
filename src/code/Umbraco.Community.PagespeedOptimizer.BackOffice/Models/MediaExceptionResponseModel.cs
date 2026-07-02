// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Response model representing a media exception.
/// </summary>
public sealed class MediaExceptionResponseModel
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco media item key this exception applies to.
    /// </summary>
    public required Guid MediaKey { get; set; }

    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public required int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether WebP conversion is forced for this media item.
    /// </summary>
    public required bool ForceWebp { get; set; }
}
