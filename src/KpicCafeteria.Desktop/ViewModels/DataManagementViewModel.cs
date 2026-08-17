using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Desktop.Services;
using Microsoft.Win32;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>데이터 관리 ViewModel.</summary>
public sealed partial class DataManagementViewModel : ObservableObject
{
    private readonly IImportService _import;
    private readonly IBackupService _backup;
    private readonly IRestoreService _restore;
    private readonly ExcelExportService _archive;
    private readonly IMessageService _messages;

    public DataManagementViewModel(
        IImportService import,
        IBackupService backup,
        IRestoreService restore,
        ExcelExportService archive,
        IMessageService messages)
    {
        _import = import;
        _backup = backup;
        _restore = restore;
        _archive = archive;
        _messages = messages;
    }

    [ObservableProperty]
    private string selectedImportPath = string.Empty;

    [ObservableProperty]
    private string importPreviewText = string.Empty;

    [ObservableProperty]
    private string importStatus = string.Empty;

    [ObservableProperty]
    private bool isImportReady;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private DateTime? archiveStartDate = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    private DateTime? archiveEndDate = DateTime.Today;

    [ObservableProperty]
    private string archiveStatus = string.Empty;

    public ObservableCollection<BackupInfo> Backups { get; } = new();

    [ObservableProperty]
    private BackupInfo? selectedBackup;

    [RelayCommand]
    private async Task BrowseImportAsync(CancellationToken cancellationToken)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            Title = "XLSX 이관 파일 선택",
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedImportPath = dialog.FileName;
            await PreviewImportAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task PreviewImportAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SelectedImportPath) || !File.Exists(SelectedImportPath))
        {
            _messages.ShowError("파일을 먼저 선택하세요.");
            return;
        }

        IsBusy = true;
        try
        {
            var preview = await _import.PreviewAsync(SelectedImportPath, cancellationToken);
            IsImportReady = preview.Ready;
            ImportPreviewText = $"배식:{preview.MealTypeCount}, 메뉴:{preview.MenuCount}, 재료:{preview.IngredientCount}, " +
                                $"별칭:{preview.AliasCount}, 레시피행:{preview.RecipeRowCount}, " +
                                $"식단:{preview.MealHistoryRowCount}, 식단재료:{preview.MealIngredientRowCount}\n" +
                                $"오류:{preview.Errors.Count} / 경고:{preview.Warnings.Count}";
            ImportStatus = string.Join("\n", preview.Errors.Select(e => $"[오류] {e.Message}").Concat(preview.Warnings.Select(w => $"[경고] {w.Message}")));
        }
        catch (Exception ex)
        {
            _messages.ShowError($"Preview 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyImportAsync(ImportMode mode, CancellationToken cancellationToken)
    {
        if (!IsImportReady)
        {
            _messages.ShowError("유효하지 않은 파일입니다. Preview를 먼저 실행하세요.");
            return;
        }

        if (mode == ImportMode.Replace &&
            !_messages.Confirm("Replace 모드는 현재 데이터를 모두 초기화합니다. 계속하시겠습니까?\n(자동 사전 백업이 수행됩니다.)"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _import.ApplyAsync(SelectedImportPath, mode, cancellationToken);
            ImportStatus = $"이관 완료: 배식 {result.MealTypes}, 메뉴 {result.Menus}, 재료 {result.Ingredients}, " +
                           $"별칭 {result.Aliases}, 레시피 {result.Recipes}, 식단 {result.Services}, " +
                           $"식단재료 {result.MealIngredientRows}";
            _messages.ShowInfo("이관이 완료되었습니다.");
        }
        catch (Exception ex)
        {
            _messages.ShowError($"Apply 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadBackupsAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            Backups.Clear();
            var list = await _backup.ListBackupsAsync(cancellationToken);
            foreach (var b in list)
            {
                Backups.Add(b);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var info = await _backup.CreateManualBackupAsync(cancellationToken);
            _messages.ShowInfo($"백업 완료: {info.Filename}");
            await LoadBackupsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _messages.ShowError($"백업 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(CancellationToken cancellationToken)
    {
        if (SelectedBackup is null)
        {
            _messages.ShowError("복원할 백업을 선택하세요.");
            return;
        }

        if (!_messages.Confirm($"'{SelectedBackup.Filename}'로 복구합니다.\n현재 데이터는 사전 백업됩니다.\n복구 후 프로그램을 다시 시작해야 합니다."))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var manifest = await _restore.ValidateAsync(SelectedBackup.StoredPath, cancellationToken);
            _messages.ShowInfo($"백업 검증 완료 (버전 {manifest.BackupVersion}).");

            if (await _restore.RestoreAsync(SelectedBackup.StoredPath, cancellationToken))
            {
                _messages.ShowInfo("복구가 완료되었습니다. 프로그램을 다시 시작합니다.");
                RestartApplication();
            }
        }
        catch (Exception ex)
        {
            _messages.ShowError($"복구 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseArchiveAsync(CancellationToken cancellationToken)
    {
        if (ArchiveStartDate is null || ArchiveEndDate is null)
        {
            _messages.ShowError("시작일과 종료일을 모두 선택하세요.");
            return;
        }

        var start = DateOnly.FromDateTime(ArchiveStartDate.Value);
        var end = DateOnly.FromDateTime(ArchiveEndDate.Value);
        if (start > end)
        {
            _messages.ShowError("시작일이 종료일보다 늦을 수 없습니다.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            Title = "데이터 아카이브 저장",
            FileName = $"데이터아카이브_{start:yyyyMMdd}-{end:yyyyMMdd}.xlsx",
        };
        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var (content, _) = await _archive.ExportAsync(start, end, cancellationToken);
            await File.WriteAllBytesAsync(dialog.FileName, content, cancellationToken);
            ArchiveStatus = $"아카이브 저장 완료: {dialog.FileName}";
            _messages.ShowInfo("아카이브 Excel이 저장되었습니다.");
        }
        catch (Exception ex)
        {
            ArchiveStatus = $"아카이브 실패: {ex.Message}";
            _messages.ShowError($"아카이브 실패: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void RestartApplication()
    {
        var exe = Environment.ProcessPath!;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        System.Windows.Application.Current.Shutdown();
    }
}
