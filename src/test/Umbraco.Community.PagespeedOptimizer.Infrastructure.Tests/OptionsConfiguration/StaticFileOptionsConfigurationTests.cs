using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.OptionsConfiguration;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests.OptionsConfiguration;

/// <summary>
/// Unit for <see cref="StaticFileOptionsConfiguration"/>.
/// </summary>
[TestFixture]
internal sealed class StaticFileOptionsConfigurationTests
{
    private PageSpeedOptimizerSettings pageSpeedOptimizerSettings = null!;
    private StaticFileOptionsConfiguration configuration = null!;

    /// <summary>
    /// Setup for all tests.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.pageSpeedOptimizerSettings = new PageSpeedOptimizerSettings { StaticAssetsCache = { Enabled = true } };

        var pageSpeedOptimizerOptionsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        pageSpeedOptimizerOptionsMock.SetupGet(x => x.Value).Returns(this.pageSpeedOptimizerSettings);

        var hostingEnvironment = new Mock<IHostingEnvironment>();
        hostingEnvironment.Setup(x => x.ToAbsolute(It.IsAny<string>())).Returns("/umbraco");

        this.configuration = new StaticFileOptionsConfiguration(pageSpeedOptimizerOptionsMock.Object, hostingEnvironment.Object);
    }

    /// <summary>
    /// Tests that the event handler to prepare the response is not registered when the static assets cache is disabled.
    /// </summary>
    [Test]
    public void Given_Static_Assets_Cache_Is_Disabled_Event_Handler_Should_Not_Be_Registered()
    {
        this.pageSpeedOptimizerSettings.StaticAssetsCache.Enabled = false;

        var options = new StaticFileOptions();

        this.configuration.Configure(options);

        Assert.That(options.OnPrepareResponse.Method.Name, Is.Not.EqualTo(nameof(StaticFileOptionsConfiguration.PrepareResponseHandler)));
    }

    /// <summary>
    /// Tests that the event handler to prepare the response is not registered when the static assets cache is enabled, but not file extensions are registerd.
    /// </summary>
    [Test]
    public void Given_No_File_Extensions_Are_Configured_Event_Handler_Should_Not_Be_Registered()
    {
        this.pageSpeedOptimizerSettings.StaticAssetsCache.CacheExtensions = new HashSet<string>();

        var options = new StaticFileOptions();

        this.configuration.Configure(options);

        Assert.That(options.OnPrepareResponse.Method.Name, Is.Not.EqualTo(nameof(StaticFileOptionsConfiguration.PrepareResponseHandler)));
    }

    /// <summary>
    /// Tests that the event handler to prepare the response is registered when the static assets cache is enabled and file extensions are registered.
    /// </summary>
    [Test]
    public void Given_Static_Assets_Cache_Is_Enabled_Event_Handler_Should_Not_Be_Registered()
    {
        var options = new StaticFileOptions();

        this.configuration.Configure(options);

        Assert.That(options.OnPrepareResponse.Method.Name, Is.EqualTo(nameof(StaticFileOptionsConfiguration.PrepareResponseHandler)));
    }

    /// <summary>
    /// Tests that no headers are applied for back office requests.
    /// </summary>
    [Test]
    public void Given_Request_Is_BackOffice_No_Headers_Should_Be_Set()
    {
        var httpContext = this.CreateHttpContext("/umbraco/image.png", out _);

        this.configuration.PrepareResponseHandler(new StaticFileResponseContext(httpContext, Mock.Of<IFileInfo>()));

        Assert.That(httpContext.Response.Headers.CacheControl.ToString(), Is.Empty);
    }

    /// <summary>
    /// Tests that headers are not applied when request has no extension.
    /// </summary>
    [Test]
    public void Given_Request_Has_No_Extension_No_Headers_Should_Be_Set()
    {
        var httpContext = this.CreateHttpContext("/file", out var fileInfo);

        this.configuration.PrepareResponseHandler(new StaticFileResponseContext(httpContext, fileInfo.Object));

        Assert.That(httpContext.Response.Headers.CacheControl.ToString(), Is.Empty);
    }

    /// <summary>
    /// Tests that headers are not applied given file extensions are not configured.
    /// </summary>
    [Test]
    public void Given_File_Extension_Is_Not_Configured_No_Headers_Should_Be_Set()
    {
        var httpContext = this.CreateHttpContext("/file.txt", out var fileInfo);

        this.configuration.PrepareResponseHandler(new StaticFileResponseContext(httpContext, fileInfo.Object));

        Assert.That(httpContext.Response.Headers.CacheControl.ToString(), Is.Empty);
    }

    /// <summary>
    /// Tests that headers are applied correctly.
    /// </summary>
    [Test]
    public void Given_Request_Is_For_Configured_File_Extension_Correct_Cache_Headers_Should_Be_Applied()
    {
        var httpContext = this.CreateHttpContext("/image.png", out var fileInfo);

        this.configuration.PrepareResponseHandler(new StaticFileResponseContext(httpContext, fileInfo.Object));

        var timeSpan = TimeSpan.FromDays(this.pageSpeedOptimizerSettings.StaticAssetsCache.MaxAgeInDays).TotalSeconds;

        Assert.That(httpContext.Response.Headers.CacheControl.ToString(), Is.EqualTo($"public, max-age={timeSpan}"));
    }

    private HttpContext CreateHttpContext(string path, out Mock<IFileInfo> fileInfoMock)
    {
        fileInfoMock = new Mock<IFileInfo>();
        fileInfoMock.SetupGet(x => x.Name).Returns(path);

        return new DefaultHttpContext { Request = { Path = path } };
    }
}
