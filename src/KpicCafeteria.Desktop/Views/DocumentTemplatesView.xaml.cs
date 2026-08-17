using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>문서 양식 관리 화면.</summary>
public partial class DocumentTemplatesView : UserControl
{
    public DocumentTemplatesView(DocumentTemplatesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
