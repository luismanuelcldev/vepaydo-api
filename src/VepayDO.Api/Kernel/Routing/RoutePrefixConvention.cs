using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace VepayDO.Api.Kernel.Routing;

/// <summary>
/// Prefixes every controller route with the API version, matching
/// <c>app.setGlobalPrefix('api/v1')</c> in the reference API. Minimal-API
/// endpoints such as the health probes declare their own absolute path and are
/// deliberately not affected (AGENTS.md section 10).
/// </summary>
public sealed class RoutePrefixConvention(string prefix) : IControllerModelConvention
{
    private readonly string _prefix = prefix.Trim('/');

    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        foreach (var selector in controller.Selectors)
        {
            var routeModel = selector.AttributeRouteModel;
            if (routeModel?.Template is null)
            {
                continue;
            }

            if (routeModel.Template.StartsWith(_prefix, StringComparison.Ordinal))
            {
                continue;
            }

            routeModel.Template = $"{_prefix}/{routeModel.Template}";
        }
    }
}
