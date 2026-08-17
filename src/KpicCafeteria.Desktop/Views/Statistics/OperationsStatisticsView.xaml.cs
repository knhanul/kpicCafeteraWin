using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Views.Statistics;

/// <summary>운영 기록 통계 화면.</summary>
public partial class OperationsStatisticsView : UserControl
{
    public OperationsStatisticsView(OperationsStatisticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
