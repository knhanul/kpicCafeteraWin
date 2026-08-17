using System.Windows;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>문서 출력 기간/형식 입력 대화상자.</summary>
public partial class DocumentOutputDialog : Window
{
    private readonly DocumentOutputDialogViewModel _viewModel;

    public DocumentOutputDialog(DocumentOutputDialogViewModel viewModel)
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
