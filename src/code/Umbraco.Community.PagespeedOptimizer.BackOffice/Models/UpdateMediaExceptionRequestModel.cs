// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Request model for updating an existing media exception.
/// </summary>
public sealed class UpdateMediaExceptionRequestModel
{
    /// <summary>
    /// Gets or sets the image quality override for this media item (1–100).
    /// </summary>
    public required int Quality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to force WebP conversion for this media item.
    /// </summary>
    public required bool ForceWebp { get; set; }
}
