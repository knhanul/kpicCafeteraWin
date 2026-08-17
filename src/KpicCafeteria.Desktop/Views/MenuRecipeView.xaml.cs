using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 메뉴·레시피 화면. 화면 표시 이외의 업무 로직은 ViewModel에 둔다.
/// </summary>
public partial class MenuRecipeView : UserControl
{
    public MenuRecipeView(MenuRecipeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
