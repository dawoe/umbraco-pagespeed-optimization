using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
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

        options.OnPrepareResponse += this.PrepareResponseHandler;
    }

    /// <summary>
    /// Event handler for the <see cref="StaticFileOptions.OnPrepareResponse"/> event.
    /// </summary>
    /// <param name="context">A <see cref="StaticFileResponseContext"/>.</param>
    internal void PrepareResponseHandler(StaticFileResponseContext context)
    {
    }
}
