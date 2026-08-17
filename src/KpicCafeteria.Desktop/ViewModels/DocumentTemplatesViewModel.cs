using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>문서 양식 행.</summary>
public sealed class DocumentTemplateRowViewModel
{
    public int Id { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string DocumentTypeName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Version { get; init; }

    public bool Active { get; init; }

    public bool IsValid { get; init; }

    public string? ValidationMessage { get; init; }

    public long FileSize { get; init; }

    public string? ChecksumSha256 { get; init; }

    public string OriginalFilename { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string FileSizeDisplay => FileSize > 0 ? $"{FileSize:N0} bytes" : "-";

    public string StatusDisplay => Active ? "활성" : (IsValid ? "검증됨" : "오류");

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

/// <summary>
/// 문서 양식 관리 화면 ViewModel.
/// </summary>
public partial class DocumentTemplatesViewModel : ObservableObject
{
    private readonly DocumentTemplateService _service;
    private readonly IDialogService _dialogs;
    private readonly IMessageService _messages;
    private readonly ILogger<DocumentTemplatesViewModel> _logger;

    public DocumentTemplatesViewModel(
        DocumentTemplateService service,
        IDialogService dialogs,
        IMessageService messages,
        ILogger<DocumentTemplatesViewModel> logger)
    {
        _service = service;
        _dialogs = dialogs;
        _messages = messages;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        ActivateCommand = new AsyncRelayCommand(ActivateAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        RestoreDefaultCommand = new AsyncRelayCommand(RestoreDefaultAsync);

        _ = LoadAsync();
    }

    public ObservableCollection<DocumentTemplateRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private DocumentTemplateRowViewModel? selectedRow;

    [ObservableProperty]
    private bool isBusy;

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand RegisterCommand { get; }

    public IAsyncRelayCommand ActivateCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand RestoreDefaultCommand { get; }

    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var templates = await _service.ListAsync();
            Rows.Clear();
            foreach (var template in templates.OrderBy(x => x.DocumentType).ThenByDescending(x => x.Version))
            {
                Rows.Add(new DocumentTemplateRowViewModel
                {
                    Id = template.Id,
                    DocumentType = template.DocumentType,
                    DocumentTypeName = DocumentTypeNames.Get(template.DocumentType),
                    Name = template.Name,
                    Version = template.Version,
                    Active = template.Active,
                    IsValid = template.IsValid,
                    ValidationMessage = template.ValidationMessage,
                    FileSize = template.FileSize ?? 0,
                    ChecksumSha256 = template.ChecksumSha256,
                    OriginalFilename = template.OriginalFilename,
                    CreatedAt = template.CreatedAt,
                });
            }
        });
    }

    private async Task RegisterAsync()
    {
        var path = _dialogs.ShowOpenFileDialog("한글 문서 양식 (*.hwpx)|*.hwpx", "문서 양식 등록");
        if (path is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var documentType = SelectedRow?.DocumentType ?? DocumentTemplateService.ValidDocumentTypes[0];
            var name = Path.GetFileNameWithoutExtension(path);
            await _service.RegisterAsync(documentType, name, bytes, Path.GetFileName(path), activate: false);
            await LoadAsync();
            _messages.ShowInfo("양식이 등록되었습니다.");
        });
    }

    private async Task ActivateAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.ActivateAsync(SelectedRow.Id);
            await LoadAsync();
            _messages.ShowInfo("양식이 활성화되었습니다.");
        });
    }

    private async Task DeleteAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        if (!_messages.Confirm($"'{SelectedRow.Name}' 양식을 삭제하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.DeleteAsync(SelectedRow.Id);
            await LoadAsync();
            _messages.ShowInfo("삭제되었습니다.");
        });
    }

    private async Task RestoreDefaultAsync()
    {
        if (SelectedRow is null)
        {
            return;
        }

        if (!_messages.Confirm($"'{DocumentTypeNames.Get(SelectedRow.DocumentType)}' 기본 양식을 새 버전으로 등록하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.RestoreDefaultAsync(SelectedRow.DocumentType);
            await LoadAsync();
            _messages.ShowInfo("기본 양식이 복원되었습니다.");
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
            _logger.LogError(ex, "문서 양식 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
