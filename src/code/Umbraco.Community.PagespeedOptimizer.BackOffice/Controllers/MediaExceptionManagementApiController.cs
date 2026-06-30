// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.PagespeedOptimizer.BackOffice.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Configuration;
using Umbraco.Community.PagespeedOptimizer.Core.Models;
using Umbraco.Community.PagespeedOptimizer.Core.Repositories;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers;

/// <summary>
/// Management API controller for creating, updating, and deleting media exceptions, and for reading global image optimization defaults.
/// </summary>
[Route("umbraco/management/api/v1/pagespeed-optimizer/media-exception")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
[MapToApi("pagespeed-optimizer-management-api")]
[ApiExplorerSettings(GroupName = "pagespeed-optimizer-management-api")]
public class MediaExceptionManagementApiController : ManagementApiControllerBase
{
    private readonly IMediaExceptionRepository _repository;
    private readonly IOptions<PageSpeedOptimizerSettings> _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaExceptionManagementApiController"/> class.
    /// </summary>
    /// <param name="repository">The media exception repository.</param>
    /// <param name="settings">The page speed optimizer settings.</param>
    public MediaExceptionManagementApiController(
        IMediaExceptionRepository repository,
        IOptions<PageSpeedOptimizerSettings> settings)
    {
        _repository = repository;
        _settings = settings;
    }

    /// <summary>
    /// Creates a new media exception.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created media exception with a 201 Created response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MediaExceptionResponseModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMediaException(CreateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var entity = new MediaException
        {
            Id = Guid.NewGuid(),
            MediaKey = request.MediaKey,
            Quality = request.Quality,
            ForceWebp = request.ForceWebp,
        };

        var created = await _repository.CreateAsync(entity, ct);
        return Created(string.Empty, MapToResponseModel(created));
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
    public async Task<IActionResult> UpdateMediaException(Guid id, UpdateMediaExceptionRequestModel request, CancellationToken ct)
    {
        var existing = await _repository.GetAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Quality = request.Quality;
        existing.ForceWebp = request.ForceWebp;

        var updated = await _repository.UpdateAsync(existing, ct);
        return Ok(MapToResponseModel(updated));
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
        var existing = await _repository.GetAsync(id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, ct);
        return Ok();
    }

    /// <summary>
    /// Gets the default image quality and ForceWebP values from global application settings.
    /// </summary>
    /// <returns>The default values from <see cref="ImageOptimizationSettings"/>.</returns>
    [HttpGet("default-values")]
    [ProducesResponseType(typeof(DefaultValuesResponseModel), StatusCodes.Status200OK)]
    public IActionResult GetDefaultValues()
    {
        var imageSettings = _settings.Value.ImageOptimization;
        return Ok(new DefaultValuesResponseModel
        {
            DefaultImageQuality = imageSettings.DefaultImageQuality,
            ForceWebP = imageSettings.ForceWebP,
        });
    }

    private static MediaExceptionResponseModel MapToResponseModel(MediaException entity) =>
        new()
        {
            Id = entity.Id,
            MediaKey = entity.MediaKey,
            Quality = entity.Quality,
            ForceWebp = entity.ForceWebp,
        };
}
