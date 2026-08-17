using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>
/// 문서 출력 화면 ViewModel.
/// 식단표/조리지시서/보존식 기록지 HWPX·PDF 생성과 Excel 데이터 아카이브를 담당한다.
/// </summary>
public partial class DocumentOutputViewModel : ObservableObject
{
    private readonly DocumentService _documentService;
    private readonly ExcelExportService _excelService;
    private readonly IDialogService _dialogs;
    private readonly IMessageService _messages;
    private readonly ILogger<DocumentOutputViewModel> _logger;

    public DocumentOutputViewModel(
        DocumentService documentService,
        ExcelExportService excelService,
        IDialogService dialogs,
        IMessageService messages,
        ILogger<DocumentOutputViewModel> logger)
    {
        _documentService = documentService;
        _excelService = excelService;
        _dialogs = dialogs;
        _messages = messages;
        _logger = logger;

        GenerateHwpxCommand = new AsyncRelayCommand(() => GenerateAsync(generatePdf: false));
        GeneratePdfCommand = new AsyncRelayCommand(() => GenerateAsync(generatePdf: true));
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);

        StartDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        EndDate = StartDate.AddDays(6);
    }

    public string[] DocumentTypes { get; } = DocumentTemplateService.ValidDocumentTypes;

    [ObservableProperty]
    private string selectedDocumentType = DocumentTemplateService.ValidDocumentTypes[0];

    [ObservableProperty]
    private DateTime startDate;

    [ObservableProperty]
    private DateTime endDate;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    public IAsyncRelayCommand GenerateHwpxCommand { get; }

    public IAsyncRelayCommand GeneratePdfCommand { get; }

    public IAsyncRelayCommand ExportExcelCommand { get; }

    private async Task GenerateAsync(bool generatePdf)
    {
        await ExecuteAsync(async () =>
        {
            var start = DateOnly.FromDateTime(StartDate);
            var end = DateOnly.FromDateTime(EndDate);
            if (start > end)
            {
                throw new DocumentException("시작일이 종료일보다 늦습니다.");
            }

            var services = await _documentService.ResolveServicesAsync(null, start, end);
            if (services.Count == 0)
            {
                throw new NoServicesException();
            }

            var (content, filename) = generatePdf
                ? await _documentService.GeneratePdfAsync(SelectedDocumentType, services, start, end)
                : await _documentService.GenerateHwpxAsync(SelectedDocumentType, services, start, end);

            var filter = generatePdf ? "PDF 문서 (*.pdf)|*.pdf" : "한글 문서 (*.hwpx)|*.hwpx";
            var savePath = _dialogs.ShowSaveFileDialog(filename, filter);
            if (savePath is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(savePath, content);
            await _documentService.MarkOutputAsync(SelectedDocumentType, services);
            StatusText = $"저장됨: {savePath}";
            _messages.ShowInfo($"문서가 저장되었습니다.\n{savePath}");
        });
    }

    private async Task ExportExcelAsync()
    {
        await ExecuteAsync(async () =>
        {
            var start = DateOnly.FromDateTime(StartDate);
            var end = DateOnly.FromDateTime(EndDate);
            var (content, filename) = await _excelService.ExportAsync(start, end);
            var savePath = _dialogs.ShowSaveFileDialog(filename, "Excel 문서 (*.xlsx)|*.xlsx");
            if (savePath is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(savePath, content);
            StatusText = $"저장됨: {savePath}";
            _messages.ShowInfo($"데이터 아카이브가 저장되었습니다.\n{savePath}");
        });
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (DocumentException ex)
        {
            _messages.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "문서 출력 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
