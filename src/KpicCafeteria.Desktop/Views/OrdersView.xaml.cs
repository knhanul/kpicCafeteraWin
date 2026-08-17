using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 발주 관리 화면. 화면 표시만 담당하며 업무 로직은 ViewModel에 둔다.
/// </summary>
public partial class OrdersView : UserControl
{
    public OrdersView(OrdersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
    }

    public OrdersViewModel ViewModel { get; }
}
