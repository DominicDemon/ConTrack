using ContractManagement.Application.Features.Contracts.Signing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Filters;

public sealed class ElectronicSigningEnabledFilter(
    IOptions<ElectronicSigningFeatureOptions> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!options.Value.Enabled)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Electronic signing is not enabled.",
                Detail = "Configure ElectronicSigning.Enabled and the GSM modem before enabling this workflow."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        await next();
    }
}
