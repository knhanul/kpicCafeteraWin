using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>문서 출력 화면.</summary>
public partial class DocumentOutputView : UserControl
{
    public DocumentOutputView(DocumentOutputViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
