using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure;

/// <summary>
/// Image url generator that sets quality and forces webp format, applying per-media exceptions where they exist.
/// </summary>
/// <param name="innerGenerator">The existing umbraco generator that we decorate in this one.</param>
/// <param name="options">The page speed optimizer settings.</param>
/// <param name="mediaExceptionCache">The cache of per-media quality/WebP overrides.</param>
internal sealed class OptimizedImageUrlGenerator(IImageUrlGenerator innerGenerator, IOptions<PageSpeedOptimizerSettings> options, IMediaExceptionCache mediaExceptionCache)
    : IImageUrlGenerator
{
    private ImageOptimizationSettings settings = options.Value.ImageOptimization;

    private const string FormatParam = "format";

    /// <inheritdoc />
    public IEnumerable<string> SupportedImageFileTypes => innerGenerator.SupportedImageFileTypes;

    /// <inheritdoc />
    public string? GetImageUrl(ImageUrlGenerationOptions options)
    {
        if (this.settings.Enabled == false)
        {
            return innerGenerator.GetImageUrl(options);
        }

        if (string.IsNullOrWhiteSpace(options.ImageUrl))
        {
            return innerGenerator.GetImageUrl(options);
        }

        var exception = mediaExceptionCache.GetByImageUrl(options.ImageUrl);

        this.TrySetWebpFormat(options, exception);

        if (options.Quality.HasValue)
        {
            return innerGenerator.GetImageUrl(options);
        }

        options.Quality = exception?.Quality ?? this.settings.DefaultImageQuality;

        return innerGenerator.GetImageUrl(options);
    }

    private void TrySetWebpFormat(ImageUrlGenerationOptions options, MediaException? exception)
    {
        if (string.IsNullOrWhiteSpace(options.FurtherOptions) == false &&
            options.FurtherOptions.Contains(FormatParam))
        {
            return;
        }

        var forceWebP = exception?.ForceWebp ?? this.settings.ForceWebP;

        if (forceWebP == false)
        {
            return;
        }

        options.FurtherOptions = $"{FormatParam}=webp";
    }
}
