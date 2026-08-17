using System.Windows;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>묶음 발주 입력 대화상자.</summary>
public partial class GroupOrderDialog : Window
{
    private readonly GroupOrderDialogViewModel _viewModel;

    public GroupOrderDialog(GroupOrderDialogViewModel viewModel)
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
