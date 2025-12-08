using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.OptionsConfiguration;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="InfrastructureComposer"/> class.
/// </summary>
[TestFixture]
internal sealed class InfrastructureComposerTests
{
    private ServiceCollection serviceCollection = null!;
    private IUmbracoBuilder builder = null!;
    private ServiceProvider serviceProvider = null!;

    /// <summary>
    /// Test that settings are registered as <see cref="IOptions&lt;PageSpeedOptimizerSettings&gt;"/> in the service collection.
    /// </summary>
    [Test]
    public void Settings_Should_Be_Registered_As_Options()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        var options = this.serviceProvider.GetRequiredService<IOptions<PageSpeedOptimizerSettings>>();

        Assert.Multiple(() =>
        {
            Assert.That(options, Is.Not.Null);
            Assert.That(options.Value.StaticAssetsCache.Enabled, Is.EqualTo(settings.StaticAssetsCache.Enabled));
            Assert.That(options.Value.StaticAssetsCache.MaxAgeInDays, Is.EqualTo(settings.StaticAssetsCache.MaxAgeInDays));
            Assert.That(options.Value.StaticAssetsCache.CacheExtensions, Is.EqualTo(settings.StaticAssetsCache.CacheExtensions));
        });
    }

    /// <summary>
    /// Test that the <see cref="StaticFileOptionsConfiguration"/> is not registered as a transient service in the service collection, when static cache is disabled.
    /// </summary>
    [Test]
    public void Given_Static_Cache_Is_Disabled_StaticFileOptionsConfiguration_Should_Not_Be_Registered()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        Assert.That(this.serviceCollection.Any(x => x.ImplementationType == typeof(StaticFileOptionsConfiguration) && x.Lifetime == ServiceLifetime.Transient), Is.False);
    }

    /// <summary>
    /// Test that the <see cref="StaticFileOptionsConfiguration"/> is registered as a transient service in the service collection, when static cache is enabled.
    /// </summary>
    [Test]
    public void Given_Static_Cache_Is_Enabled_StaticFileOptionsConfiguration_Should_Be_Registered()
    {
        var settings = new PageSpeedOptimizerSettings { StaticAssetsCache = { Enabled = true } };

        this.Compose(settings);

        Assert.That(this.serviceCollection.Any(x => x.ImplementationType == typeof(StaticFileOptionsConfiguration) && x.Lifetime == ServiceLifetime.Transient), Is.True);
    }

    /// <summary>
    /// Tests that response compression services are not registered when it is disabled.
    /// </summary>
    [Test]
    public void Given_Response_Compression_Is_Disabled_Services_Should_Not_Be_Registered()
    {
        var settings = new PageSpeedOptimizerSettings();

        this.Compose(settings);

        Assert.Multiple(() =>
        {
            var compressionOptions = this.serviceProvider.GetService<IOptions<ResponseCompressionOptions>>();

            Assert.That(compressionOptions?.Value, Is.Not.Null);
            Assert.That(compressionOptions?.Value.EnableForHttps, Is.False);
            Assert.That(compressionOptions?.Value.Providers.Count, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Tests that response compression services are registered when it is enabled.
    /// </summary>
    [Test]
    public void Given_Response_Compression_Is_Enabled_Services_Should_Be_Registered()
    {
        var settings = new PageSpeedOptimizerSettings();
        settings.ResponseCompression.Enabled = true;

        this.Compose(settings);

        Assert.Multiple(() =>
        {
            var compressionOptions = this.serviceProvider.GetService<IOptions<ResponseCompressionOptions>>();

            Assert.That(compressionOptions?.Value, Is.Not.Null);
            Assert.That(compressionOptions?.Value.EnableForHttps, Is.True);
            Assert.That(compressionOptions?.Value.Providers.Count, Is.EqualTo(2));
        });
    }

    private void Compose(PageSpeedOptimizerSettings settings)
    {
        var settingsDictionary = this.MapToSettingsToDictionary(settings, PageSpeedOptimizerSettings.SectionName);

        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(settingsDictionary);

        var config = configBuilder.Build();

        this.serviceCollection = new ServiceCollection();

        this.builder = new UmbracoBuilder(this.serviceCollection, config, new TypeLoader(Mock.Of<ITypeFinder>(), Mock.Of<ILogger<TypeLoader>>()));

        var composer = new InfrastructureComposer();

        composer.Compose(this.builder);

        this.serviceProvider = this.serviceCollection.BuildServiceProvider();
    }

    private Dictionary<string, string?> MapToSettingsToDictionary(object source, string name)
    {
        var dictionary = new Dictionary<string, string?>();
        this.MapToDictionaryInternal(dictionary, source, name);
        return dictionary;
    }

    private void MapToDictionaryInternal(Dictionary<string, string?> dictionary, object source, string name)
    {
        var properties = source.GetType().GetProperties();
        foreach (var p in properties)
        {
            var key = name + ":" + p.Name;
            var value = p.GetValue(source, null) ?? string.Empty;
            var valueType = value.GetType();

            if (valueType.IsPrimitive || valueType == typeof(string))
            {
                dictionary[key] = value.ToString();
            }
            else if (value is ISet<string> set)
            {
                var i = 0;
                foreach (var o in set)
                {
                    dictionary[key + ":" + i] = o;
                    i++;
                }
            }
            else
            {
                this.MapToDictionaryInternal(dictionary, value, key);
            }
        }
    }
}
