using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Views.Statistics;

/// <summary>식재료 통계 화면.</summary>
public partial class IngredientStatisticsView : UserControl
{
    public IngredientStatisticsView(IngredientStatisticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
