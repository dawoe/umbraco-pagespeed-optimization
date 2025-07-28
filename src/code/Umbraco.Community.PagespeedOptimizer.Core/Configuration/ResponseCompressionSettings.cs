using System.Diagnostics.CodeAnalysis;

namespace Umbraco.Community.PagespeedOptimizer.Core.Configuration;

/// <summary>
/// Response compression configuration
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ResponseCompressionSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether response compression should be enabled.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool Enabled { get; set; } = false;
}
