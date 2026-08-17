using System.IO;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Desktop.ViewModels;
using KpicCafeteria.Desktop.Views;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.Services;

/// <summary>
/// 대화상자 표시 서비스 구현.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly WorkspaceService _workspaceService;
    private readonly MasterDataService _masterDataService;
    private readonly ILoggerFactory _loggerFactory;

    public DialogService(
        WorkspaceService workspaceService,
        MasterDataService masterDataService,
        ILoggerFactory loggerFactory)
    {
        _workspaceService = workspaceService;
        _masterDataService = masterDataService;
        _loggerFactory = loggerFactory;
    }

    public List<MenuPickerSelection>? ShowMenuPicker(int serviceId)
    {
        var viewModel = new MenuPickerDialogViewModel(
            _workspaceService,
            _loggerFactory.CreateLogger<MenuPickerDialogViewModel>(),
            serviceId);
        var dialog = new MenuPickerDialog(viewModel);
        var shown = dialog.ShowDialog();
        return shown == true ? viewModel.Result : null;
    }

    public int? ShowRecipePicker(int menuId)
    {
        var menu = _masterDataService.GetMenuAsync(menuId).GetAwaiter().GetResult();
        var activeRecipes = menu.Recipes.Where(r => r.Active).ToList();
        if (activeRecipes.Count == 0)
        {
            return null;
        }

        var viewModel = new RecipePickerDialogViewModel(activeRecipes);
        var dialog = new RecipePickerDialog(viewModel);
        var shown = dialog.ShowDialog();
        return shown == true ? viewModel.Result : null;
    }

    public GroupOrderSelection? ShowGroupOrderDialog(
        string ingredientName,
        double? totalRequired,
        string? requiredUnit,
        double? suggestedQuantity,
        string? suggestedUnit,
        DateOnly defaultOrderDate,
        DateOnly defaultDeliveryDate)
    {
        var viewModel = new GroupOrderDialogViewModel(
            ingredientName, totalRequired, requiredUnit, suggestedQuantity, suggestedUnit,
            defaultOrderDate, defaultDeliveryDate);
        var dialog = new GroupOrderDialog(viewModel);
        var shown = dialog.ShowDialog();
        return shown == true ? viewModel.Result : null;
    }

    public BulkUpdateSelection? ShowBulkUpdateDialog()
    {
        var viewModel = new BulkUpdateDialogViewModel();
        var dialog = new BulkUpdateDialog(viewModel);
        var shown = dialog.ShowDialog();
        return shown == true ? viewModel.Result : null;
    }

    public DocumentOutputSelection? ShowDocumentOutputDialog(
        string documentType,
        DateOnly defaultStartDate,
        DateOnly defaultEndDate)
    {
        var viewModel = new DocumentOutputDialogViewModel(documentType, defaultStartDate, defaultEndDate);
        var dialog = new DocumentOutputDialog(viewModel);
        var shown = dialog.ShowDialog();
        return shown == true ? viewModel.Result : null;
    }

    public string? ShowSaveFileDialog(string suggestedFilename, string filter)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = suggestedFilename,
            Filter = filter,
            AddExtension = true,
            DefaultExt = Path.GetExtension(suggestedFilename).TrimStart('.'),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenFileDialog(string filter, string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
