using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.PagespeedOptimizer.Infrastructure.Extensions;

namespace Umbraco.Community.PagespeedOptimizer.Infrastructure;

/// <summary>
/// Composer for the Page Speed Optimizer infrastructure.
/// </summary>
internal sealed class InfrastructureComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.AddPagespeedOptimizer();
}
