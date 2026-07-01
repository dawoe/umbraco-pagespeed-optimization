// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers;

/// <summary>
/// Management API controller for creating, updating, and deleting media exceptions, and for reading global image optimization defaults.
/// </summary>
[ApiController]
[BackOfficeRoute("pagespeed-optimizer/v{version:apiVersion}/media-exception")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[MapToApi(Constants.ApiName)]
[ApiExplorerSettings(GroupName = "Page Speed Optimizer")]
public sealed class MediaExceptionManagementApiController : ManagementApiControllerBase
{
    private readonly IMediaExceptionRepository repository;
    private readonly IOptions<PageSpeedOptimizerSettings> settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionManagementApiController"/> class.
    /// </summary>
    /// <param name="repository">The media exception repository.</param>
    /// <param name="settings">The page speed optimizer settings.</param>
    public MediaExceptionManagementApiController(
        IMediaExceptionRepository repository,
        IOptions<PageSpeedOptimizerSettings> settings)
    {
        this.repository = repository;
        this.settings = settings;
    }

    /// <summary>
    /// Creates a new media exception.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created media exception with a 201 Created response, or 409 Conflict if an exception already exists for the given media key.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMediaException([FromBody] CreateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var existing = await this.repository.GetByMediaKeyAsync(request.MediaKey, ct);
        if (existing is not null)
        {
            return this.Conflict();
        }

        var entity = new MediaException
        {
            Id = Guid.NewGuid(),
            MediaKey = request.MediaKey,
            Quality = request.Quality,
            ForceWebp = request.ForceWebp,
        };

        var created = await this.repository.CreateAsync(entity, ct);
        return this.Created($"/media-exception/{created.Id}", MapToResponseModel(created));
    }

    /// <summary>
    /// Updates an existing media exception.
    /// </summary>
    /// <param name="id">The unique identifier of the media exception.</param>
    /// <param name="request">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated media exception, or 404 Not Found if no exception exists for the given identifier.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMediaException(Guid id, [FromBody] UpdateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var existing = await this.repository.GetAsync(id, ct);
        if (existing is null)
        {
            return this.NotFound();
        }

        existing.Quality = request.Quality;
        existing.ForceWebp = request.ForceWebp;

        var updated = await this.repository.UpdateAsync(existing, ct);
        return this.Ok(MapToResponseModel(updated));
    }

    /// <summary>
    /// Deletes a media exception.
    /// </summary>
    /// <param name="id">The unique identifier of the media exception to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK if deleted, or 404 Not Found if no exception exists for the given identifier.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMediaException(Guid id, CancellationToken ct)
    {
        var existing = await this.repository.GetAsync(id, ct);
        if (existing is null)
        {
            return this.NotFound();
        }

        await this.repository.DeleteAsync(id, ct);
        return this.Ok();
    }

    /// <summary>
    /// Gets a media exception by its media key.
    /// </summary>
    /// <param name="mediaKey">The Umbraco media item key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The media exception, or 404 Not Found if no exception exists for the given media key.</returns>
    [HttpGet("by-media-key/{mediaKey:guid}")]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByMediaKey(Guid mediaKey, CancellationToken ct)
    {
        var existing = await this.repository.GetByMediaKeyAsync(mediaKey, ct);
        if (existing is null)
        {
            return this.NotFound();
        }

        return this.Ok(MapToResponseModel(existing));
    }

    /// <summary>
    /// Gets the default image quality and ForceWebP values from global application settings.
    /// </summary>
    /// <returns>The default values from <see cref="ImageOptimizationSettings"/>.</returns>
    [HttpGet("default-values")]
    [ProducesResponseType(typeof(DefaultValuesResponseModel), StatusCodes.Status200OK)]
    public IActionResult GetDefaultValues()
    {
        var imageSettings = this.settings.Value.ImageOptimization;
        return this.Ok(new DefaultValuesResponseModel
        {
            DefaultImageQuality = imageSettings.DefaultImageQuality,
            ForceWebP = imageSettings.ForceWebP,
        });
    }

    /// <summary>
    /// Maps a <see cref="MediaException"/> entity to a <see cref="MediaExceptionResponseModel"/>.
    /// </summary>
    private static MediaExceptionResponseModel MapToResponseModel(MediaException entity) =>
        new()
        {
            Id = entity.Id,
            MediaKey = entity.MediaKey,
            Quality = entity.Quality,
            ForceWebp = entity.ForceWebp,
        };
}
