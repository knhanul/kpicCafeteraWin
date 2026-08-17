using System.Windows;
using System.Windows.Controls;
using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Desktop.ViewModels;
using KpicCafeteria.Desktop.Views;
using KpicCafeteria.Desktop.Views.Statistics;
using Microsoft.Extensions.DependencyInjection;

namespace KpicCafeteria.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// 화면 전환(네비게이션)과 집중모드 네비게이션 숨김만 담당하며 업무 로직은 ViewModel에 둔다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private WorkspaceView? _workspaceView;
    private OrdersView? _ordersView;
    private DashboardView? _dashboardView;
    private MealStatisticsView? _mealStatisticsView;
    private MenuStatisticsView? _menuStatisticsView;
    private IngredientStatisticsView? _ingredientStatisticsView;
    private OperationsStatisticsView? _operationsStatisticsView;
    private DataManagementView? _dataManagementView;
    private Button? _selectedNavButton;

    public MainWindow(IAppDataPathProvider paths, IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        DatabaseStatusText.Text = $"DB: {paths.DatabasePath}";

        // 기본 화면: 주간 급식 운영
        ShowWorkspace();
        SelectNav(WorkspaceNavButton);
    }

    private void SelectNav(Button button)
    {
        if (_selectedNavButton is not null)
            _selectedNavButton.Tag = null;
        button.Tag = "Selected";
        _selectedNavButton = button;
    }

    private void ShowWorkspace()
    {
        if (!ConfirmLeaveDirty())
        {
            return;
        }

        _workspaceView ??= _services.GetRequiredService<WorkspaceView>();
        _workspaceView.FocusModeChanged += OnWorkspaceFocusModeChanged;
        ContentHost.Content = _workspaceView;
    }

    private void ShowOrders()
    {
        if (!ConfirmLeaveDirty())
        {
            return;
        }

        _ordersView ??= _services.GetRequiredService<OrdersView>();
        ContentHost.Content = _ordersView;
    }

    /// <summary>발주 화면에 저장하지 않은 변경이 있으면 확인한다.</summary>
    private bool ConfirmLeaveDirty()
    {
        if (_ordersView is not null && _ordersView.ViewModel.IsDirty)
        {
            return MessageBox.Show(
                "발주 변경 내용이 저장되지 않았습니다. 이동하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        return true;
    }

    private void OnWorkspaceFocusModeChanged(object? sender, bool isFocusMode)
    {
        // 집중 작성 모드에서는 좌측 네비게이션을 숨긴다.
        NavColumn.Width = isFocusMode ? new GridLength(0) : new GridLength(180);
    }

    private void OnWorkspaceNav(object sender, RoutedEventArgs e)
    {
        ShowWorkspace();
        if (ContentHost.Content == _workspaceView)
            SelectNav(WorkspaceNavButton);
    }

    private void OnOrdersNav(object sender, RoutedEventArgs e)
    {
        ShowOrders();
        if (ContentHost.Content == _ordersView)
            SelectNav(OrdersNavButton);
    }

    private void OnDocumentOutputNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            ContentHost.Content = _services.GetRequiredService<DocumentOutputView>();
            SelectNav(DocumentOutputNavButton);
        }
    }

    private void OnDocumentTemplatesNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            ContentHost.Content = _services.GetRequiredService<DocumentTemplatesView>();
            SelectNav(DocumentTemplatesNavButton);
        }
    }

    private void OnMenuRecipeNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            ContentHost.Content = _services.GetRequiredService<MenuRecipeView>();
            SelectNav(MenuRecipeNavButton);
        }
    }

    private void OnIngredientNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            ContentHost.Content = _services.GetRequiredService<IngredientView>();
            SelectNav(IngredientNavButton);
        }
    }

    private void OnMealDefaultsNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            ContentHost.Content = _services.GetRequiredService<MealDefaultsView>();
            SelectNav(MealDefaultsNavButton);
        }
    }

    private void OnDashboardNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _dashboardView ??= _services.GetRequiredService<DashboardView>();
            ContentHost.Content = _dashboardView;
            SelectNav(DashboardNavButton);
        }
    }

    private void OnMealStatisticsNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _mealStatisticsView ??= _services.GetRequiredService<MealStatisticsView>();
            ContentHost.Content = _mealStatisticsView;
            SelectNav(MealStatisticsNavButton);
        }
    }

    private void OnMenuStatisticsNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _menuStatisticsView ??= _services.GetRequiredService<MenuStatisticsView>();
            ContentHost.Content = _menuStatisticsView;
            SelectNav(MenuStatisticsNavButton);
        }
    }

    private void OnIngredientStatisticsNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _ingredientStatisticsView ??= _services.GetRequiredService<IngredientStatisticsView>();
            ContentHost.Content = _ingredientStatisticsView;
            SelectNav(IngredientStatisticsNavButton);
        }
    }

    private void OnOperationsStatisticsNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _operationsStatisticsView ??= _services.GetRequiredService<OperationsStatisticsView>();
            ContentHost.Content = _operationsStatisticsView;
            SelectNav(OperationsStatisticsNavButton);
        }
    }

    private void OnDataManagementNav(object sender, RoutedEventArgs e)
    {
        if (ConfirmLeaveDirty())
        {
            _dataManagementView ??= _services.GetRequiredService<DataManagementView>();
            ContentHost.Content = _dataManagementView;
            SelectNav(DataManagementNavButton);
        }
    }
}