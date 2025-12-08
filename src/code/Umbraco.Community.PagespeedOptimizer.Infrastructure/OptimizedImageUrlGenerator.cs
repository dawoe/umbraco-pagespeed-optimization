using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure;

/// <summary>
/// Image url generator that sets quality and forces webp format.
/// </summary>
/// <param name="innerGenerator">The existing umbraco generator that we decorate in this one.</param>
internal sealed class OptimizedImageUrlGenerator(IImageUrlGenerator innerGenerator, IOptions<PageSpeedOptimizerSettings> options)
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

        this.TrySetWebpFormat(options);

        if (options.Quality.HasValue)
        {
            return innerGenerator.GetImageUrl(options);
        }

        options.Quality = this.settings.DefaultImageQuality;

        return innerGenerator.GetImageUrl(options);
    }

    private void TrySetWebpFormat(ImageUrlGenerationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FurtherOptions) == false &&
            options.FurtherOptions.Contains(FormatParam))
        {
            return;
        }

        if (this.settings.ForceWebP == false)
        {
            return;
        }

        options.FurtherOptions = $"{FormatParam}=webp";
    }
}
