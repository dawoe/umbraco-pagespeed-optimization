using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;

namespace Umbraco.Community.PagespeedOptimizer.Extensions;

/// <summary>
/// Extension methods for <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Enables response compression when enabled in configuration.
    /// </summary>
    /// <param name="app">A <see cref="WebApplication"/> instance.</param>
    public static void EnableResponseCompression(this WebApplication app)
    {
        var config = app.Services.GetRequiredService<IOptions<PageSpeedOptimizerSettings>>();

        if (config.Value.ResponseCompression.Enabled == false)
        {
            return;
        }

        app.UseResponseCompression();
    }
}
