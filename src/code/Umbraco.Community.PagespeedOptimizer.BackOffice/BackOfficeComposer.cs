// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.PagespeedOptimizer.BackOffice;

/// <summary>
/// Composer for the Page Speed Optimizer back-office, registering the management API Swagger document.
/// </summary>
internal sealed class BackOfficeComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                Constants.ApiName,
                new OpenApiInfo { Title = "PageSpeed Optimizer Management API", Version = "1.0", });

            options.OperationFilter<OperationSecurityFilter>();
        });
    }

    private class OperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }


    private class CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
        : OperationIdHandler(apiVersioningOptions)
    {
        protected override bool CanHandle(
            ApiDescription apiDescription,
            ControllerActionDescriptor controllerActionDescriptor) =>
            controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(
                "Umbraco.Community.PagespeedOptimizer.BackOffice.Controllers",
                StringComparison.InvariantCultureIgnoreCase) is true;

        public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
    }

}
