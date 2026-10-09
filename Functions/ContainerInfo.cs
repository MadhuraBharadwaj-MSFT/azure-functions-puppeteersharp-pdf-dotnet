using System.Runtime.InteropServices;
using AzureFunctions.PuppeteerSharpPdf.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.PuppeteerSharpPdf.Functions;

public sealed class ContainerInfo(ChromiumService chromiumService)
{
    [Function(nameof(ContainerInfo))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "container-info")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var chromium = await chromiumService.GetInfoAsync(cancellationToken);
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        request.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";

        return new OkObjectResult(
            new
            {
                dotnetVersion = Environment.Version.ToString(),
                platform = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                chromium
            });
    }
}
