using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>데이터 관리 화면.</summary>
public partial class DataManagementView : UserControl
{
    public DataManagementView(DataManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadBackupsCommand.Execute(null);
    }
}
