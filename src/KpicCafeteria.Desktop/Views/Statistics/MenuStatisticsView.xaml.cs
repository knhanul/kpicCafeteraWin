using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Views.Statistics;

/// <summary>메뉴 통계 화면.</summary>
public partial class MenuStatisticsView : UserControl
{
    public MenuStatisticsView(MenuStatisticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
