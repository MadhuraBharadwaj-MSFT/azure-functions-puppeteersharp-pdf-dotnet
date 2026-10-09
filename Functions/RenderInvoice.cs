using System.Text.Json;
using AzureFunctions.PuppeteerSharpPdf.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.PuppeteerSharpPdf.Functions;

public sealed class RenderInvoice(PdfRenderer pdfRenderer, ILogger<RenderInvoice> logger)
{
    private const int MaxRequestBytes = 256 * 1024;

    [Function(nameof(RenderInvoice))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "render-invoice")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxRequestBytes)
        {
            return PayloadTooLarge();
        }

        JsonDocument document;
        try
        {
            var body = await ReadBodyAsync(request, cancellationToken);
            if (body.Length > MaxRequestBytes)
            {
                return PayloadTooLarge();
            }

            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(
                new { error = "Request body must be valid JSON." });
        }

        using (document)
        {
            var validation = InvoiceValidator.Validate(document.RootElement);
            if (!validation.IsValid)
            {
                return new BadRequestObjectResult(
                    new
                    {
                        error = "Invoice validation failed.",
                        details = validation.Errors
                    });
            }

            try
            {
                var invoice = validation.Invoice!;
                var pdf = await pdfRenderer.RenderAsync(
                    InvoiceHtmlBuilder.Build(invoice),
                    cancellationToken);

                request.HttpContext.Response.Headers.CacheControl = "no-store";
                request.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
                return new FileContentResult(pdf, "application/pdf")
                {
                    FileDownloadName = InvoiceFilename.Create(invoice.InvoiceNumber)
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "PDF rendering failed.");
                return new ObjectResult(
                    new
                    {
                        error = "The invoice could not be rendered. Check the function logs for details."
                    })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }

    private static async Task<byte[]> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            if (output.Length > MaxRequestBytes)
            {
                break;
            }
        }

        return output.ToArray();
    }

    private static ObjectResult PayloadTooLarge() =>
        new(new { error = $"Request body must not exceed {MaxRequestBytes} bytes." })
        {
            StatusCode = StatusCodes.Status413PayloadTooLarge
        };
}
