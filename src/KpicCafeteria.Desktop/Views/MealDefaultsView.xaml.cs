using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 배식 기본값 화면. 화면 표시 이외의 업무 로직은 ViewModel에 둔다.
/// </summary>
public partial class MealDefaultsView : UserControl
{
    public MealDefaultsView(MealDefaultsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
