using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace VepayDO.Api.Kernel.Routing;

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
