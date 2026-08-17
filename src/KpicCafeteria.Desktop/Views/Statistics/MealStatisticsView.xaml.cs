using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Views.Statistics;

/// <summary>식수 통계 화면.</summary>
public partial class MealStatisticsView : UserControl
{
    public MealStatisticsView(MealStatisticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
