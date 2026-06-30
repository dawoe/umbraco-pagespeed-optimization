// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice;

/// <summary>
/// Composer for the Page Speed Optimizer back-office, registering the management API Swagger document.
/// </summary>
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddSwaggerGen(options =>
            options.SwaggerDoc(
                "pagespeed-optimizer-management-api",
                new OpenApiInfo
                {
                    Title = "PageSpeed Optimizer Management API",
                    Version = "1.0",
                }));
}
