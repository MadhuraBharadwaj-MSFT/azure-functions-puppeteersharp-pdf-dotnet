using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AzureFunctions.PuppeteerSharpPdf.Services;

public sealed record ChromiumInfo(
    bool Available,
    string? Path,
    string? Version,
    string? Error);

public sealed class ChromiumService
{
    private static readonly string[] WindowsCandidates =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Google",
            "Chrome",
            "Application",
            "chrome.exe"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft",
            "Edge",
            "Application",
            "msedge.exe")
    ];

    public string GetExecutablePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("CHROMIUM_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            throw new FileNotFoundException(
                $"CHROMIUM_PATH points to a file that does not exist: {configuredPath}");
        }

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsCandidates
            : ["/usr/bin/chromium", "/usr/bin/chromium-browser"];

        var executablePath = candidates.FirstOrDefault(File.Exists);
        return executablePath
            ?? throw new FileNotFoundException(
                "Chromium was not found. Set CHROMIUM_PATH to the browser executable.");
    }

    public async Task<ChromiumInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = GetExecutablePath();
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Chromium version process did not start.");
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Chromium exited with code {process.ExitCode}: {error.Trim()}");
            }

            return new ChromiumInfo(true, path, output.Trim(), null);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or InvalidOperationException
                or UnauthorizedAccessException
                or Win32Exception)
        {
            return new ChromiumInfo(false, null, null, exception.Message);
        }
    }
}
