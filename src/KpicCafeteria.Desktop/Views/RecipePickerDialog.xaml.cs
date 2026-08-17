using System.Windows;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 레시피 선택기 대화상자.
/// </summary>
public partial class RecipePickerDialog : Window
{
    private readonly RecipePickerDialogViewModel _viewModel;

    public RecipePickerDialog(RecipePickerDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        _viewModel.Confirm();
        DialogResult = true;
    }
}
