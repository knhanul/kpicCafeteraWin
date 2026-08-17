using System.Windows;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>일괄 변경 입력 대화상자.</summary>
public partial class BulkUpdateDialog : Window
{
    private readonly BulkUpdateDialogViewModel _viewModel;

    public BulkUpdateDialog(BulkUpdateDialogViewModel viewModel)
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
