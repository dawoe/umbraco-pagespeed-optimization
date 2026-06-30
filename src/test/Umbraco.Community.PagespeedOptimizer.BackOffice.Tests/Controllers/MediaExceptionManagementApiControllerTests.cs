// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="MediaExceptionManagementApiController"/>.
/// </summary>
[TestFixture]
internal sealed class MediaExceptionManagementApiControllerTests
{
    private Mock<IMediaExceptionRepository> _repositoryMock = null!;
    private Mock<IOptions<PageSpeedOptimizerSettings>> _settingsMock = null!;
    private MediaExceptionManagementApiController _controller = null!;

    /// <summary>
    /// Sets up a fresh controller with mocked dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _repositoryMock = new Mock<IMediaExceptionRepository>();
        _settingsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        _settingsMock.Setup(s => s.Value).Returns(new PageSpeedOptimizerSettings());
        _controller = new MediaExceptionManagementApiController(_repositoryMock.Object, _settingsMock.Object);
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.CreateMediaException"/> returns 201 Created with the created entity.
    /// </summary>
    [Test]
    public async Task CreateMediaException_Returns_201_With_ResponseModel()
    {
        var mediaKey = Guid.NewGuid();
        var request = new CreateMediaExceptionRequestModel { MediaKey = mediaKey, Quality = 75, ForceWebp = true };

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await _controller.CreateMediaException(request, CancellationToken.None);

        var created = result as CreatedResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.StatusCode, Is.EqualTo(201));

        var response = created.Value as MediaExceptionResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.MediaKey, Is.EqualTo(mediaKey));
            Assert.That(response.Quality, Is.EqualTo(75));
            Assert.That(response.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.UpdateMediaException"/> returns 200 OK with the updated entity when found.
    /// </summary>
    [Test]
    public async Task UpdateMediaException_Returns_200_With_ResponseModel_When_Found()
    {
        var id = Guid.NewGuid();
        var existing = new MediaException { Id = id, MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };
        var request = new UpdateMediaExceptionRequestModel { Quality = 60, ForceWebp = true };

        _repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await _controller.UpdateMediaException(id, request, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as MediaExceptionResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Id, Is.EqualTo(id));
            Assert.That(response.Quality, Is.EqualTo(60));
            Assert.That(response.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.UpdateMediaException"/> returns 404 when the entity does not exist.
    /// </summary>
    [Test]
    public async Task UpdateMediaException_Returns_404_When_Not_Found()
    {
        _repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await _controller.UpdateMediaException(Guid.NewGuid(), new UpdateMediaExceptionRequestModel { Quality = 80, ForceWebp = false }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.DeleteMediaException"/> returns 200 OK when the entity exists.
    /// </summary>
    [Test]
    public async Task DeleteMediaException_Returns_200_When_Found()
    {
        var id = Guid.NewGuid();
        var existing = new MediaException { Id = id, MediaKey = Guid.NewGuid(), Quality = 85, ForceWebp = false };

        _repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repositoryMock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.DeleteMediaException(id, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.DeleteMediaException"/> returns 404 when the entity does not exist.
    /// </summary>
    [Test]
    public async Task DeleteMediaException_Returns_404_When_Not_Found()
    {
        _repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await _controller.DeleteMediaException(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetDefaultValues"/> returns 200 OK with the values from settings.
    /// </summary>
    [Test]
    public void GetDefaultValues_Returns_200_With_Settings_Values()
    {
        var settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { DefaultImageQuality = 70, ForceWebP = true },
        };
        _settingsMock.Setup(s => s.Value).Returns(settings);

        var result = _controller.GetDefaultValues();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as DefaultValuesResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.DefaultImageQuality, Is.EqualTo(70));
            Assert.That(response.ForceWebP, Is.True);
        });
    }
}
