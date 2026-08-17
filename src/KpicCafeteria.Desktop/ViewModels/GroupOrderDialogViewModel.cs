using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Desktop.Services;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>
/// 묶음 발주 입력 대화상자 ViewModel.
/// 필요량 합계/추천량을 표시하고 사용자가 발주량/발주일/배송일을 확정한다.
/// </summary>
public partial class GroupOrderDialogViewModel : ObservableObject
{
    public GroupOrderDialogViewModel(
        string ingredientName,
        double? totalRequired,
        string? requiredUnit,
        double? suggestedQuantity,
        string? suggestedUnit,
        DateOnly defaultOrderDate,
        DateOnly defaultDeliveryDate)
    {
        IngredientName = ingredientName;
        TotalRequiredDisplay = totalRequired is null ? "-" : $"{totalRequired} {requiredUnit}";
        SuggestedDisplay = suggestedQuantity is null
            ? (suggestedUnit is null ? "포장단위 확인 필요" : "-")
            : $"{suggestedQuantity} {suggestedUnit}";
        OrderQuantity = suggestedQuantity ?? totalRequired;
        OrderUnit = suggestedUnit ?? requiredUnit;
        OrderDate = defaultOrderDate.ToDateTime(TimeOnly.MinValue);
        DeliveryDate = defaultDeliveryDate.ToDateTime(TimeOnly.MinValue);
    }

    public string IngredientName { get; }

    public string TotalRequiredDisplay { get; }

    public string SuggestedDisplay { get; }

    [ObservableProperty]
    private double? orderQuantity;

    [ObservableProperty]
    private string? orderUnit;

    [ObservableProperty]
    private DateTime orderDate;

    [ObservableProperty]
    private DateTime deliveryDate;

    public GroupOrderSelection? Result { get; private set; }

    public void Confirm()
    {
        Result = new GroupOrderSelection(
            OrderQuantity,
            string.IsNullOrWhiteSpace(OrderUnit) ? null : OrderUnit.Trim(),
            DateOnly.FromDateTime(OrderDate),
            DateOnly.FromDateTime(DeliveryDate));
    }
}
