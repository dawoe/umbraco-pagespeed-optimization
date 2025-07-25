using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Extensions;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.OptionsConfiguration;

/// <summary>
/// Static file options configuration.
/// </summary>
/// <param name="globalSettings">A <see cref="IOptions{GlobalSettings}"/>.</param>
/// <param name="pageSpeedOptimizerSettings">A <see cref="IOptions{PageSpeedOptimizerSettings}"/>.</param>
/// <param name="hostingEnvironment">A <see cref="IHostingEnvironment"/>.</param>
internal sealed class StaticFileOptionsConfiguration(
    IOptions<GlobalSettings> globalSettings,
    IOptions<PageSpeedOptimizerSettings> pageSpeedOptimizerSettings,
    IHostingEnvironment hostingEnvironment)
    : IConfigureOptions<StaticFileOptions>
{
    private readonly StaticAssetsCache staticAssetsCacheSettings = pageSpeedOptimizerSettings.Value.StaticAssetsCache;
    private readonly string backOfficePath = globalSettings.Value.GetBackOfficePath(hostingEnvironment);

    /// <inheritdoc />
    public void Configure(StaticFileOptions options)
    {
        if (this.staticAssetsCacheSettings.Enabled == false)
        {
            return;
        }

        if (this.staticAssetsCacheSettings.CacheExtensions.Count == 0)
        {
            return;
        }

        options.OnPrepareResponse += this.PrepareResponseHandler;
    }

    /// <summary>
    /// Event handler for the <see cref="StaticFileOptions.OnPrepareResponse"/> event.
    /// </summary>
    /// <param name="context">A <see cref="StaticFileResponseContext"/>.</param>
    internal void PrepareResponseHandler(StaticFileResponseContext context)
    {
        if (this.CanRequestBeCached(context) == false)
        {
            return;
        }

        this.ApplyCacheHeaders(context);
    }

    private bool CanRequestBeCached(StaticFileResponseContext context)
    {
        if (context.Context.Request.Path.StartsWithSegments(this.backOfficePath))
        {
            return false;
        }

        var fileExtension = Path.GetExtension(context.File.Name);

        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            return false;
        }

        return this.staticAssetsCacheSettings.CacheExtensions.Contains(fileExtension.TrimStart("."));
    }

    private void ApplyCacheHeaders(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.GetTypedHeaders();

        var cacheControl = headers.CacheControl ?? new CacheControlHeaderValue();
        cacheControl.Public = true;
        cacheControl.MaxAge = TimeSpan.FromDays(this.staticAssetsCacheSettings.MaxAgeInDays);
        headers.CacheControl = cacheControl;
    }
}
