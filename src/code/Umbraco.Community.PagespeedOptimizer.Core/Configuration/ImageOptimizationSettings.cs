using System.Diagnostics.CodeAnalysis;

namespace Umbraco.Community.PagespeedOptimizer.Core.Configuration;

/// <summary>
/// Configuration for image optimization.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ImageOptimizationSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether static cache should be enabled.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the default image quality.
    /// </summary>
    /// <remarks>Defaults to 85. This will only be applied when no image quality is specified when generating a crop url.</remarks>
    public int DefaultImageQuality { get; set; } = 85;

    /// <summary>
    /// Gets or sets a value indicating whether to force serving all images in webp format.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool ForceWebP { get; set; }
}
