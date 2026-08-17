using System.Windows;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 메뉴 선택기 대화상자.
/// </summary>
public partial class MenuPickerDialog : Window
{
    private readonly MenuPickerDialogViewModel _viewModel;

    public MenuPickerDialog(MenuPickerDialogViewModel viewModel)
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
