using System.Diagnostics.CodeAnalysis;

namespace Umbraco.Community.PagespeedOptimizer.Core.Configuration;

/// <summary>
/// Settings object for static assets caching.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class StaticAssetsCacheSettings
{
    private static readonly ISet<string> DefaultCacheExtensions = new HashSet<string> { "js", "css", "svg", "woff2", "woff", "otf", "ttf", "ico", "jpg", "png", "gif", "webp" };

    /// <summary>
    /// Gets or sets a value indicating whether static cache should be enabled.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the max-age in days.
    /// </summary>
    /// <remarks>Defaults to 365 days.</remarks>
    public int MaxAgeInDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets a list of file extensions where a cache-control header should be applied.
    /// </summary>
    /// <remarks>By default, js, css, svg, woff2, woff, otf, ttf, ico, jpg, png, gif and webp we are cached.</remarks>
    public ISet<string> CacheExtensions { get; set; } = DefaultCacheExtensions;

    /// <summary>
    /// Gets or sets a value indicating whether static cache settings should be applied to smidge bundles.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool ApplyToSmidgeBundles { get; set; } = false;
}
