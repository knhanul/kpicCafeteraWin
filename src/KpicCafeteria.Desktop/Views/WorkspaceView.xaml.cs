using System.ComponentModel;
using System.Windows.Controls;
using KpicCafeteria.Desktop.ViewModels;

namespace KpicCafeteria.Desktop.Views;

/// <summary>
/// 주간 급식 운영 화면. 화면 표시/집중모드 이벤트 전달만 담당하며 업무 로직은 ViewModel에 둔다.
/// </summary>
public partial class WorkspaceView : UserControl
{
    private readonly WorkspaceViewModel _viewModel;

    public WorkspaceView(WorkspaceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>집중 작성 모드 전환 시 MainWindow의 네비게이션 표시를 제어하기 위한 이벤트.</summary>
    public event EventHandler<bool>? FocusModeChanged;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.IsFocusMode))
        {
            FocusModeChanged?.Invoke(this, _viewModel.IsFocusMode);
        }
    }
}
