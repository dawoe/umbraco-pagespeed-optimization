// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice;

/// <summary>
/// Composer for the Page Speed Optimizer back-office, registering the management API OpenAPI document.
/// </summary>
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddBackOfficeOpenApiDocument(
            Constants.ApiName,
            doc => doc
                .WithTitle("PageSpeed Optimizer Management API")
                .WithBackOfficeAuthentication());
}
