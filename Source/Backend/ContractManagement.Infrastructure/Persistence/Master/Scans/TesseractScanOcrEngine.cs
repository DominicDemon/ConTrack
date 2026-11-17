using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Process-isolated Tesseract run for one page image, like the LibreOffice PDF renderer.
/// Tesseract and its vie/eng language data are a deployment dependency; they are not bundled.
/// </summary>
public sealed class TesseractScanOcrEngine : IScanOcrEngine
{
    private const int MaxOutputCharacters = 1_000_000;

    // Each run is CPU heavy: at most MaxParallelPages at a time across all requests (the engine is
    // a singleton), sharing the cores between them so the runs do not fight over the same ones.
    private readonly SemaphoreSlim _runGate;

    private readonly ScanOcrOptions _options;

    public TesseractScanOcrEngine(IOptions<ScanOcrOptions> options)
    {
        _options = options.Value;
        _runGate = new SemaphoreSlim(_options.MaxParallelPages, _options.MaxParallelPages);
    }

    public async Task<string> RecognizeAsync(
        byte[] image,
        int? dpi,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var extension = ScanFileKinds.Detect(image) switch
        {
            ScanFileKind.Png => ".png",
            ScanFileKind.Jpeg => ".jpg",
            _ => throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.UnsupportedFile,
                "OCR chỉ đọc được ảnh PNG hoặc JPEG.",
                isClientError: true)
        };
        var executable = ResolveExecutablePath(_options.ExecutablePath)
            ?? throw new ContractScanOcrException(
                ContractScanOcrErrorCodes.EngineUnavailable,
                "Không tìm thấy Tesseract OCR. Hãy cài Tesseract kèm dữ liệu ngôn ngữ vie, eng " +
                "hoặc cấu hình ScanOcr:ExecutablePath tới tesseract executable.");

        await _runGate.WaitAsync(cancellationToken);
        var root = Path.Combine(Path.GetTempPath(), $"contract-scan-ocr-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var inputPath = Path.Combine(root, "page" + extension);
            await System.IO.File.WriteAllBytesAsync(inputPath, image, cancellationToken);

            using var process = new Process
            {
                StartInfo = CreateStartInfo(executable, inputPath, dpi)
            };
            try
            {
                if (!process.Start())
                {
                    throw Unavailable();
                }
            }
            catch (ContractScanOcrException)
            {
                throw;
            }
            catch (Exception exception) when (exception is Win32Exception
                or UnauthorizedAccessException or IOException)
            {
                throw Unavailable();
            }

            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            // stderr only carries progress notes such as "Estimating resolution as 199".
            var notes = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.PageTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeout.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await DrainAsync(output, notes);
                if (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new ContractScanOcrException(
                        ContractScanOcrErrorCodes.Timeout,
                        "Tesseract vượt quá thời gian đọc một trang cho phép.");
                }
                throw;
            }

            var text = await output;
            await notes;
            if (process.ExitCode != 0)
            {
                throw new ContractScanOcrException(
                    ContractScanOcrErrorCodes.EngineFailed,
                    $"Tesseract không đọc được ảnh. Hãy kiểm tra dữ liệu ngôn ngữ '{_options.Languages}'.");
            }

            return text.Length > MaxOutputCharacters ? text[..MaxOutputCharacters] : text;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch
            {
                // Temporary cleanup is operational only and cannot change an OCR result.
            }
            _runGate.Release();
        }
    }

    private ProcessStartInfo CreateStartInfo(string executable, string inputPath, int? dpi)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        info.Environment["OMP_THREAD_LIMIT"] =
            Math.Max(1, Environment.ProcessorCount / _options.MaxParallelPages).ToString(CultureInfo.InvariantCulture);
        info.ArgumentList.Add(inputPath);
        info.ArgumentList.Add("stdout");
        info.ArgumentList.Add("-l");
        info.ArgumentList.Add(_options.Languages);
        info.ArgumentList.Add("--psm");
        info.ArgumentList.Add("3");
        if (!string.IsNullOrWhiteSpace(_options.TessdataPath))
        {
            info.ArgumentList.Add("--tessdata-dir");
            info.ArgumentList.Add(_options.TessdataPath.Trim().Trim('"'));
        }
        if (dpi is > 0)
        {
            info.ArgumentList.Add("--dpi");
            info.ArgumentList.Add(dpi.Value.ToString(CultureInfo.InvariantCulture));
        }
        return info;
    }

    private static ContractScanOcrException Unavailable() => new(
        ContractScanOcrErrorCodes.EngineUnavailable,
        "Không thể khởi động Tesseract OCR.");

    internal static string? ResolveExecutablePath(string? configuredPath)
    {
        var configured = configuredPath?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (System.IO.File.Exists(configured))
            {
                return Path.GetFullPath(configured);
            }

            var configuredFromPath = FindOnPath(configured);
            if (configuredFromPath is not null)
            {
                return configuredFromPath;
            }
        }

        foreach (var candidate in GetDefaultExecutableCandidates())
        {
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }
        }

        return FindOnPath(OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract");
    }

    private static IEnumerable<string> GetDefaultExecutableCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            // Machine-wide install, then the per-user install winget makes without admin rights.
            foreach (var folder in new[]
                     {
                         Environment.SpecialFolder.ProgramFiles,
                         Environment.SpecialFolder.ProgramFilesX86
                     })
            {
                var root = Environment.GetFolderPath(folder);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    yield return Path.Combine(root, "Tesseract-OCR", "tesseract.exe");
                }
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", "Tesseract-OCR", "tesseract.exe");
            }
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/opt/homebrew/bin/tesseract";
        }
        yield return "/usr/bin/tesseract";
        yield return "/usr/local/bin/tesseract";
    }

    private static string? FindOnPath(string executableName)
    {
        if (Path.IsPathRooted(executableName) ||
            executableName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), executableName);
                if (System.IO.File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException or PathTooLongException)
            {
                // Ignore malformed PATH entries and continue with the remaining entries.
            }
        }
        return null;
    }

    private static async Task DrainAsync(params Task[] reads)
    {
        try
        {
            await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // The process was killed; its partial output is not used.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The temporary directory cleanup remains best effort.
        }
    }
}
