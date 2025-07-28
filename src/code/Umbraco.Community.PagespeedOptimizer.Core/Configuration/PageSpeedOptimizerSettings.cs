using System.Diagnostics.CodeAnalysis;

namespace Umbraco.Community.PagespeedOptimizer.Core.Configuration;

/// <summary>
/// Settings object for page speed optimizer.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PageSpeedOptimizerSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Umbraco:Community:PageSpeedOptimizer";

    /// <summary>
    /// Gets or sets the static assets cache configuration.
    /// </summary>
    public StaticAssetsCacheSettings StaticAssetsCache { get; set; } = new();

    /// <summary>
    /// Gets or sets the response compression configuration.
    /// </summary>
    public ResponseCompressionSettings ResponseCompression { get; set; } = new();
}
