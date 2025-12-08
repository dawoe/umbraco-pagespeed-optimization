using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.OptionsConfiguration;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/>.
/// </summary>
internal static class UmbracoBuilderExtensions
{
    /// <summary>
    /// Registers page speed optimizer services with the Umbraco builder.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    public static IUmbracoBuilder AddPagespeedOptimizer(this IUmbracoBuilder builder) =>
        builder
            .LoadConfiguration()
            .AddStaticCache()
            .AddResponseCompression();

    private static IUmbracoBuilder LoadConfiguration(this IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<PageSpeedOptimizerSettings>().Bind(builder.Config.GetSection(PageSpeedOptimizerSettings.SectionName));
        return builder;
    }

    private static IUmbracoBuilder AddStaticCache(this IUmbracoBuilder builder)
    {
        var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<PageSpeedOptimizerSettings>>().Value;

        if (configuration.StaticAssetsCache.Enabled == false)
        {
            return builder;
        }

        builder.Services.AddTransient<IConfigureOptions<StaticFileOptions>, StaticFileOptionsConfiguration>();

        return builder;
    }

    private static IUmbracoBuilder AddResponseCompression(this IUmbracoBuilder builder)
    {
        var configuration = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<PageSpeedOptimizerSettings>>();

        if (configuration.Value.ResponseCompression.Enabled == false)
        {
            return builder;
        }

        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
            options.Providers.Add<BrotliCompressionProvider>();
        });

        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return builder;
    }
}
