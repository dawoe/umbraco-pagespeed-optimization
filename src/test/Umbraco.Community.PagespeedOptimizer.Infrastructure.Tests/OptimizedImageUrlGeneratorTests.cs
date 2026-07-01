// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Caching;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="OptimizedImageUrlGenerator"/>.
/// </summary>
[TestFixture]
internal sealed class OptimizedImageUrlGeneratorTests
{
    private Mock<IImageUrlGenerator> innerGeneratorMock = null!;
    private Mock<IMediaExceptionCache> mediaExceptionCacheMock = null!;
    private PageSpeedOptimizerSettings settings = null!;
    private OptimizedImageUrlGenerator generator = null!;

    /// <summary>
    /// Sets up a fresh generator with a pass-through inner generator before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.innerGeneratorMock = new Mock<IImageUrlGenerator>();
        this.innerGeneratorMock
            .Setup(g => g.GetImageUrl(It.IsAny<ImageUrlGenerationOptions>()))
            .Returns((ImageUrlGenerationOptions o) => o.ImageUrl);

        this.mediaExceptionCacheMock = new Mock<IMediaExceptionCache>();

        this.settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { Enabled = true, DefaultImageQuality = 85, ForceWebP = false },
        };

        var optionsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        optionsMock.SetupGet(o => o.Value).Returns(this.settings);

        this.generator = new OptimizedImageUrlGenerator(this.innerGeneratorMock.Object, optionsMock.Object, this.mediaExceptionCacheMock.Object);
    }

    /// <summary>
    /// Tests that a matching exception's quality is used instead of the global default.
    /// </summary>
    [Test]
    public void GetImageUrl_Uses_Exception_Quality_Instead_Of_Global_Default()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 40, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(40));
    }

    /// <summary>
    /// Tests that the global default quality is used when no exception matches.
    /// </summary>
    [Test]
    public void GetImageUrl_Uses_Global_Default_Quality_When_No_Exception_Matches()
    {
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl(It.IsAny<string>())).Returns((MediaException?)null);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(85));
    }

    /// <summary>
    /// Tests that an explicitly requested quality (already set by the caller) is never overridden, even when an exception matches.
    /// </summary>
    [Test]
    public void GetImageUrl_Does_Not_Override_Explicitly_Requested_Quality()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 40, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg") { Quality = 95 };

        this.generator.GetImageUrl(options);

        Assert.That(options.Quality, Is.EqualTo(95));
    }

    /// <summary>
    /// Tests that an exception forcing WebP wins even when the global setting does not force it.
    /// </summary>
    [Test]
    public void GetImageUrl_Forces_Webp_When_Exception_ForceWebp_Is_True_Even_If_Global_Setting_Is_False()
    {
        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = true };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.FurtherOptions, Is.EqualTo("format=webp"));
    }

    /// <summary>
    /// Tests that an exception opting out of WebP wins even when the global setting forces it.
    /// </summary>
    [Test]
    public void GetImageUrl_Does_Not_Force_Webp_When_Exception_ForceWebp_Is_False_Even_If_Global_Setting_Is_True()
    {
        this.settings.ImageOptimization.ForceWebP = true;

        var exception = new MediaException { Id = Guid.NewGuid(), MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };
        this.mediaExceptionCacheMock.Setup(c => c.GetByImageUrl("/media/1001/photo.jpg")).Returns(exception);

        var options = new ImageUrlGenerationOptions("/media/1001/photo.jpg");

        this.generator.GetImageUrl(options);

        Assert.That(options.FurtherOptions, Is.Not.EqualTo("format=webp"));
    }
}
