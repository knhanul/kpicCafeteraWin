using System.Diagnostics;
using System.Text;

namespace KpicCafeteria.Documents.Pdf;

/// <summary>
/// 한컴오피스 자동화 워커 프로세스(KpicCafeteria.HancomWorker)를 통해
/// HWPX → PDF 변환을 수행하는 렌더러.
/// 변환은 격리 프로세스에서 실행되므로 한글 오류/중단이 UI를 막지 않는다.
/// </summary>
public sealed class HancomPdfRenderer : IPdfRenderer
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    private readonly TimeSpan _timeout;
    private readonly string? _workerPath;

    public HancomPdfRenderer(TimeSpan? timeout = null, string? workerPath = null)
    {
        _timeout = timeout ?? DefaultTimeout;
        _workerPath = workerPath;
    }

    public byte[] Render(byte[] hwpxBytes, string sourceName = "document.hwpx")
    {
        if (hwpxBytes.Length == 0)
        {
            throw new ArgumentException("HWPX 내용이 비어 있습니다.", nameof(hwpxBytes));
        }

        var worker = ResolveWorkerPath();
        var tempDir = Path.Combine(Path.GetTempPath(), "KpicCafeteria", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(sourceName) + ".hwpx");
        var outputPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(sourceName) + ".pdf");

        try
        {
            File.WriteAllBytes(inputPath, hwpxBytes);

            var startInfo = new ProcessStartInfo
            {
                FileName = worker,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--timeout");
            startInfo.ArgumentList.Add(((int)_timeout.TotalSeconds).ToString());

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("한글 변환 프로세스를 시작할 수 없습니다.");

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 이미 종료된 경우 무시
                }

                throw new PdfConversionException("한글 PDF 변환이 시간 초과되었습니다. 한컴오피스가 실행 중인지 확인해 주세요.");
            }

            if (process.ExitCode != 0)
            {
                var message = stderr.Result.Trim();
                throw new PdfConversionException(
                    string.IsNullOrWhiteSpace(message)
                        ? "한글 PDF 변환에 실패했습니다."
                        : $"한글 PDF 변환에 실패했습니다: {message}");
            }

            if (!File.Exists(outputPath))
            {
                throw new PdfConversionException("PDF 파일이 생성되지 않았습니다.");
            }

            return File.ReadAllBytes(outputPath);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // 임시 파일 정리 실패는 치명적이지 않다.
            }
        }
    }

    private string ResolveWorkerPath()
    {
        if (_workerPath is not null)
        {
            if (!File.Exists(_workerPath))
            {
                throw new PdfConversionException($"한글 변환 워커를 찾을 수 없습니다: {_workerPath}");
            }

            return _workerPath;
        }

        var candidate = Path.Combine(AppContext.BaseDirectory, "KpicCafeteria.HancomWorker.exe");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new PdfConversionException(
            "한글 변환 워커(KpicCafeteria.HancomWorker.exe)를 찾을 수 없습니다. 설치가 올바른지 확인해 주세요.");
    }
}

/// <summary>PDF 변환 실패 예외.</summary>
public sealed class PdfConversionException : Exception
{
    public PdfConversionException(string message)
        : base(message)
    {
    }

    public PdfConversionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
