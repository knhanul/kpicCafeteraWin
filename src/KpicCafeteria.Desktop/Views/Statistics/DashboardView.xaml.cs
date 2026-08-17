using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Views.Statistics;

/// <summary>운영 대시보드 화면.</summary>
public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
