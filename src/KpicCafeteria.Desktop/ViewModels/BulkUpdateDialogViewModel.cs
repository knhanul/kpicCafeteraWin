using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Desktop.Services;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>
/// 일괄 변경 입력 대화상자 ViewModel.
/// 발주일/배송일/상태 중 입력된 항목만 적용한다 (변경 항목이 하나도 없으면 실행하지 않는다).
/// </summary>
public partial class BulkUpdateDialogViewModel : ObservableObject
{
    public BulkUpdateDialogViewModel()
    {
        StatusOptions = OrderStatusOption.All;
    }

    public IReadOnlyList<OrderStatusOption> StatusOptions { get; }

    /// <summary>상태 미변경 옵션.</summary>
    public OrderStatusOption NoStatusChange { get; } = OrderStatusOption.None;

    [ObservableProperty]
    private bool changeOrderDate;

    [ObservableProperty]
    private DateTime? orderDate;

    [ObservableProperty]
    private bool changeDeliveryDate;

    [ObservableProperty]
    private DateTime? deliveryDate;

    [ObservableProperty]
    private OrderStatusOption selectedStatus = OrderStatusOption.None;

    public BulkUpdateSelection? Result { get; private set; }

    public void Confirm()
    {
        Result = new BulkUpdateSelection(
            ChangeOrderDate ? (OrderDate is null ? null : DateOnly.FromDateTime(OrderDate.Value)) : null,
            ChangeDeliveryDate ? (DeliveryDate is null ? null : DateOnly.FromDateTime(DeliveryDate.Value)) : null,
            SelectedStatus.Code);
    }
}

/// <summary>발주 상태 선택 옵션 (미변경 포함).</summary>
public sealed record OrderStatusOption(string Code, string Label)
{
    public static readonly OrderStatusOption None = new("", "변경 안 함");
    public static readonly OrderStatusOption Pending = new("pending", "미처리");
    public static readonly OrderStatusOption Ordered = new("ordered", "발주완료");
    public static readonly OrderStatusOption Skipped = new("skipped", "발주안함");

    public static IReadOnlyList<OrderStatusOption> All => [None, Pending, Ordered, Skipped];

    /// <summary>XAML ObjectDataProvider용.</summary>
    public static IReadOnlyList<OrderStatusOption> GetAll() => All;
}
