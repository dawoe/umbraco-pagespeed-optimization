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
    private Mock<IMediaExceptionRepository> repositoryMock = null!;
    private Mock<IOptions<PageSpeedOptimizerSettings>> settingsMock = null!;
    private MediaExceptionManagementApiController controller = null!;

    /// <summary>
    /// Sets up a fresh controller with mocked dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.repositoryMock = new Mock<IMediaExceptionRepository>();
        this.settingsMock = new Mock<IOptions<PageSpeedOptimizerSettings>>();
        this.settingsMock.Setup(s => s.Value).Returns(new PageSpeedOptimizerSettings());
        this.controller = new MediaExceptionManagementApiController(this.repositoryMock.Object, this.settingsMock.Object);
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.CreateMediaException"/> returns 201 Created with the created entity.
    /// </summary>
    [Test]
    public async Task CreateMediaException_Returns_201_With_ResponseModel()
    {
        var mediaKey = Guid.NewGuid();
        var request = new CreateMediaExceptionRequestModel { MediaKey = mediaKey, Quality = 75, ForceWebp = true };

        this.repositoryMock
            .Setup(r => r.GetByMediaKeyAsync(mediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException?)null);

        this.repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await this.controller.CreateMediaException(request, CancellationToken.None);

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
    /// Tests that <see cref="MediaExceptionManagementApiController.CreateMediaException"/> returns 409 Conflict when a media exception already exists for the given media key.
    /// </summary>
    [Test]
    public async Task CreateMediaException_Returns_409_When_MediaKey_Already_Exists()
    {
        var mediaKey = Guid.NewGuid();
        var request = new CreateMediaExceptionRequestModel { MediaKey = mediaKey, Quality = 75, ForceWebp = true };
        var existing = new MediaException { Id = Guid.NewGuid(), MediaKey = mediaKey, Quality = 75, ForceWebp = true };

        this.repositoryMock
            .Setup(r => r.GetByMediaKeyAsync(mediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await this.controller.CreateMediaException(request, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<ConflictResult>());
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

        this.repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        this.repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<MediaException>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaException e, CancellationToken _) => e);

        var result = await this.controller.UpdateMediaException(id, request, CancellationToken.None);

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
        this.repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await this.controller.UpdateMediaException(Guid.NewGuid(), new UpdateMediaExceptionRequestModel { Quality = 80, ForceWebp = false }, CancellationToken.None);

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

        this.repositoryMock.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        this.repositoryMock.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await this.controller.DeleteMediaException(id, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.DeleteMediaException"/> returns 404 when the entity does not exist.
    /// </summary>
    [Test]
    public async Task DeleteMediaException_Returns_404_When_Not_Found()
    {
        this.repositoryMock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await this.controller.DeleteMediaException(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetByMediaKey"/> returns 200 OK with the response model when a media exception exists for the given media key.
    /// </summary>
    [Test]
    public async Task GetByMediaKey_Returns_200_With_ResponseModel_When_Found()
    {
        var mediaKey = Guid.NewGuid();
        var existing = new MediaException { Id = Guid.NewGuid(), MediaKey = mediaKey, Quality = 80, ForceWebp = true };

        this.repositoryMock.Setup(r => r.GetByMediaKeyAsync(mediaKey, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await this.controller.GetByMediaKey(mediaKey, CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as MediaExceptionResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Id, Is.EqualTo(existing.Id));
            Assert.That(response.MediaKey, Is.EqualTo(mediaKey));
            Assert.That(response.Quality, Is.EqualTo(80));
            Assert.That(response.ForceWebp, Is.True);
        });
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetByMediaKey"/> returns 200 OK with a null body when no media exception exists for the given media key.
    /// </summary>
    [Test]
    public async Task GetByMediaKey_Returns_200_With_Null_When_Not_Found()
    {
        this.repositoryMock.Setup(r => r.GetByMediaKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MediaException?)null);

        var result = await this.controller.GetByMediaKey(Guid.NewGuid(), CancellationToken.None);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.Null);
    }

    /// <summary>
    /// Tests that <see cref="MediaExceptionManagementApiController.GetDefaultValues"/> returns 200 OK with the values from settings.
    /// </summary>
    [Test]
    public void GetDefaultValues_Returns_200_With_Settings_Values()
    {
        var settings = new PageSpeedOptimizerSettings
        {
            ImageOptimization = new ImageOptimizationSettings { DefaultImageQuality = 70, ForceWebP = true, Enabled = true },
        };
        this.settingsMock.Setup(s => s.Value).Returns(settings);

        var result = this.controller.GetDefaultValues();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as DefaultValuesResponseModel;
        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.DefaultImageQuality, Is.EqualTo(70));
            Assert.That(response.ForceWebP, Is.True);
            Assert.That(response.Enabled, Is.True);
        });
    }
}
