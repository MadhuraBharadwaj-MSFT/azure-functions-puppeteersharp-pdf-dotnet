using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace AzureFunctions.PuppeteerSharpPdf.Services;

public sealed class PdfRenderer(ChromiumService chromiumService)
{
    public async Task<byte[]> RenderAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        var launchOptions = new LaunchOptions
        {
            Browser = SupportedBrowser.Chrome,
            ExecutablePath = chromiumService.GetExecutablePath(),
            Headless = true,
            Args =
            [
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--no-sandbox",
                "--no-zygote"
            ]
        };

        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new SetContentOptions());

        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfDataAsync(
            new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true
            });
    }
}
