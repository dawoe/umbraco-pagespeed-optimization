using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Imaging.ImageSharp.Media;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;
using Umbraco.Community.PagespeedOptimizer.Core.Services;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Caching.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.OptionsConfiguration;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Notifications;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Persistence.Repositories;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Services;
using Umbraco.Extensions;

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
            .AddResponseCompression()
            .AddOptimizedImageUrlGenerator()
            .AddMediaExceptionPersistence()
            .AddMediaExceptionCaching();

    private static IUmbracoBuilder LoadConfiguration(this IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<PageSpeedOptimizerSettings>().Bind(builder.Config.GetSection(PageSpeedOptimizerSettings.SectionName));
        return builder;
    }

    private static IUmbracoBuilder AddStaticCache(this IUmbracoBuilder builder)
    {
        var configuration = new PageSpeedOptimizerSettings();
        builder.Config.GetSection(PageSpeedOptimizerSettings.SectionName).Bind(configuration);

        if (configuration.StaticAssetsCache.Enabled == false)
        {
            return builder;
        }

        builder.Services.AddTransient<IConfigureOptions<StaticFileOptions>, StaticFileOptionsConfiguration>();

        return builder;
    }

    private static IUmbracoBuilder AddResponseCompression(this IUmbracoBuilder builder)
    {
        var configuration = new PageSpeedOptimizerSettings();
        builder.Config.GetSection(PageSpeedOptimizerSettings.SectionName).Bind(configuration);

        if (configuration.ResponseCompression.Enabled == false)
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

    private static IUmbracoBuilder AddOptimizedImageUrlGenerator(this IUmbracoBuilder builder)
    {
        var configuration = new PageSpeedOptimizerSettings();
        builder.Config.GetSection(PageSpeedOptimizerSettings.SectionName).Bind(configuration);

        if (configuration.ImageOptimization.Enabled == false)
        {
            return builder;
        }

        var imageUrlGenerators = builder.Services.Where(s => s.ServiceType == typeof(IImageUrlGenerator)).ToList();
        var imageSharpGenerator = imageUrlGenerators.FirstOrDefault(s => s.ImplementationType == typeof(ImageSharpImageUrlGenerator));

        if (imageSharpGenerator?.ImplementationType == null)
        {
            return builder;
        }

        foreach (var generator in imageUrlGenerators)
        {
            builder.Services.Remove(generator);
        }

        builder.Services.AddSingleton<IImageUrlGenerator>(provider =>
        {
            var inner = (IImageUrlGenerator)ActivatorUtilities.CreateInstance(provider, imageSharpGenerator.ImplementationType);

            var options = provider.GetRequiredService<IOptions<PageSpeedOptimizerSettings>>();
            var mediaExceptionCache = provider.GetRequiredService<IMediaExceptionCache>();

            return new OptimizedImageUrlGenerator(inner, options, mediaExceptionCache);
        });

        return builder;
    }

    /// <summary>
    /// Registers persistence services for media exceptions, including the DbContext, repository, and migration notification handler.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    private static IUmbracoBuilder AddMediaExceptionPersistence(this IUmbracoBuilder builder)
    {
        builder.Services.AddUmbracoDbContext<PageSpeedOptimizerDbContext>(
            (_, optionsBuilder, connectionString, providerName) =>
            {
                if (connectionString is not null && providerName is not null)
                {
                    optionsBuilder.UseDatabaseProvider(providerName, connectionString);
                }
            });

        builder.Services.AddSingleton<IMediaExceptionRepository, MediaExceptionRepository>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunMediaExceptionsMigration>();

        return builder;
    }

    /// <summary>
    /// Registers the media exception cache, the service that keeps it in sync with writes, the cache refresher, and the notification handlers that rebuild it asynchronously.
    /// </summary>
    /// <param name="builder">A <see cref="IUmbracoBuilder"/>.</param>
    /// <returns>Updated <see cref="IUmbracoBuilder"/>.</returns>
    private static IUmbracoBuilder AddMediaExceptionCaching(this IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IMediaExceptionCache, MediaExceptionCache>();
        builder.Services.AddSingleton<IMediaExceptionService, MediaExceptionService>();

        builder.CacheRefreshers().Add<MediaExceptionCacheRefresher>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RebuildMediaExceptionCacheOnStartup>();
        builder.AddNotificationAsyncHandler<MediaExceptionCacheRefresherNotification, RebuildMediaExceptionCacheOnRefresh>();

        return builder;
    }
}
