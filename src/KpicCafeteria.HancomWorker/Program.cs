using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KpicCafeteria.HancomWorker;

/// <summary>
/// 한컴오피스 COM 자동화로 HWPX → PDF 변환을 수행하는 격리 프로세스.
/// UI 프로세스와 분리되어 있어 한글 오류/중단이 앱을 막지 않는다.
/// 사용법: KpicCafeteria.HancomWorker --input <hwpx> --output <pdf> [--timeout <초>]
/// 종료 코드: 0 성공, 1 실패, 2 시간 초과
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitTimeout = 2;

    private static readonly string[] ProgIds = ["HWPFrame.HwpObject", "HwpObject"];

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options is null)
            {
                PrintUsage();
                return ExitFailure;
            }

            if (!File.Exists(options.InputPath))
            {
                Console.Error.WriteLine($"입력 파일이 없습니다: {options.InputPath}");
                return ExitFailure;
            }

            var sw = Stopwatch.StartNew();
            var lastError = "알 수 없는 오류";
            for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
            {
                if (sw.Elapsed.TotalSeconds >= options.TimeoutSeconds)
                {
                    Console.Error.WriteLine("시간 초과로 변환을 중단합니다.");
                    return ExitTimeout;
                }

                try
                {
                    ConvertOnce(options.InputPath, options.OutputPath);
                    Console.WriteLine($"OK {sw.Elapsed.TotalSeconds:F1}s");
                    return ExitSuccess;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Console.Error.WriteLine($"시도 {attempt}/{options.MaxAttempts} 실패: {ex.Message}");
                    if (attempt < options.MaxAttempts)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(2 * attempt));
                    }
                }
            }

            Console.Error.WriteLine(lastError);
            return ExitFailure;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitFailure;
        }
    }

    private static void ConvertOnce(string inputPath, string outputPath)
    {
        dynamic? hwp = null;
        try
        {
            hwp = CreateInstance();
            if (hwp is null)
            {
                throw new InvalidOperationException("한컴오피스가 설치되어 있지 않거나 자동화를 지원하지 않습니다.");
            }

            // 한글 창을 숨긴다 (표시하면 사용자 작업을 방해함)
            try
            {
                hwp.SetMessageBoxMode(0x00000010); // MB_SAVEAS_ALERT_OFF | 확인창 자동 닫기
                hwp.Visible = false;
            }
            catch
            {
                // 일부 버전은 Visible 속성을 지원하지 않는다.
            }

            hwp.Open(inputPath, "HWPX");

            // PDF 저장. 일부 버전은 FileSaveAs의 포맷 문자열이 다르므로 순차 시도.
            var saved = false;
            foreach (var format in new[] { "PDF", "PDF2" })
            {
                try
                {
                    hwp.FileSaveAs(outputPath, format);
                    saved = true;
                    break;
                }
                catch
                {
                    // 다음 포맷 시도
                }
            }

            if (!saved)
            {
                throw new InvalidOperationException("PDF 저장에 실패했습니다.");
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                throw new InvalidOperationException("PDF 파일이 생성되지 않았습니다.");
            }
        }
        finally
        {
            if (hwp is not null)
            {
                try
                {
                    hwp.Quit();
                }
                catch
                {
                    // 이미 종료된 경우 무시
                }
            }
        }
    }

    private static dynamic? CreateInstance()
    {
        foreach (var progId in ProgIds)
        {
            try
            {
                var type = Type.GetTypeFromProgID(progId);
                if (type is null)
                {
                    continue;
                }

                return Activator.CreateInstance(type);
            }
            catch (COMException)
            {
                // 다음 ProgID 시도
            }
        }

        return null;
    }

    private static WorkerOptions? ParseArgs(string[] args)
    {
        string? input = null;
        string? output = null;
        var timeout = 120;
        var maxAttempts = 3;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--input" when index + 1 < args.Length:
                    input = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--timeout" when index + 1 < args.Length && int.TryParse(args[++index], out var parsed):
                    timeout = Math.Max(10, parsed);
                    break;
                case "--attempts" when index + 1 < args.Length && int.TryParse(args[++index], out var parsed):
                    maxAttempts = Math.Max(1, parsed);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        return new WorkerOptions(input, output, timeout, maxAttempts);
    }

    private static void PrintUsage()
        => Console.Error.WriteLine("사용법: KpicCafeteria.HancomWorker --input <hwpx> --output <pdf> [--timeout <초>] [--attempts <횟수>]");

    private sealed record WorkerOptions(string InputPath, string OutputPath, int TimeoutSeconds, int MaxAttempts);
}
