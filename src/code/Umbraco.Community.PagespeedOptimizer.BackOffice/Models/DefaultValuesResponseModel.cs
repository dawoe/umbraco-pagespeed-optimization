// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Models;

/// <summary>
/// Response model for the global image optimization default values from application settings.
/// </summary>
public sealed class DefaultValuesResponseModel
{
    /// <summary>
    /// Gets or sets the default image quality from global settings.
    /// </summary>
    public required int DefaultImageQuality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether WebP is forced globally.
    /// </summary>
    public required bool ForceWebP { get; set; }
}
