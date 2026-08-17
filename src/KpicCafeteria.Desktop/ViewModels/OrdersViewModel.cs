using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Orders;
using KpicCafeteria.Desktop.Services;
using KpicCafeteria.Domain.Domain;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>발주 목록 행. 필요량/추천량은 읽기 전용, 사용자 입력 필드는 편집 가능.</summary>
public partial class OrderItemRowViewModel : ObservableObject
{
    public int? Id { get; init; }

    public DateOnly ServiceDate { get; init; }

    public int? IngredientId { get; init; }

    public string IngredientName { get; init; } = string.Empty;

    public double? RequiredQuantity { get; init; }

    public string? RequiredUnit { get; init; }

    public double? SuggestedOrderQuantity { get; init; }

    public string? SuggestedUnit { get; init; }

    public bool PackageCompatible { get; init; }

    public double? PackageQuantity { get; init; }

    public string? PackageUnit { get; init; }

    public bool InPlan { get; init; }

    public int? OrderGroupId { get; init; }

    public double? OrderGroupQuantity { get; init; }

    public string? OrderGroupUnit { get; init; }

    public IReadOnlyList<OrderSourceMenuDto> SourceMenus { get; init; } = [];

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private double? orderQuantity;

    [ObservableProperty]
    private string? orderUnit;

    [ObservableProperty]
    private DateTime? orderDate;

    [ObservableProperty]
    private DateTime? deliveryDate;

    [ObservableProperty]
    private string status = "pending";

    [ObservableProperty]
    private string? orderNote;

    public string ServiceDateLabel => ServiceDate.ToString("M/d");

    public string RequiredDisplay => RequiredQuantity is null ? "-" : $"{RequiredQuantity:0.###} {RequiredUnit}";

    public string PackageDisplay => PackageQuantity is null ? "-" : $"{PackageQuantity:0.###} {PackageUnit}";

    public string SuggestedDisplay => SuggestedOrderQuantity is null
        ? (PackageQuantity is null ? "-" : "포장단위 확인 필요")
        : $"{SuggestedOrderQuantity:0.###} {SuggestedUnit}";

    public string StatusDisplay => Status switch
    {
        "ordered" => "발주완료",
        "skipped" => "발주안함",
        _ => "미처리",
    };

    public string InPlanLabel => InPlan ? string.Empty : "식단에서 제외됨";

    public string GroupDisplay => OrderGroupId is null ? string.Empty : $"묶음 {OrderGroupQuantity:0.###} {OrderGroupUnit}";

    public string SourceMenusDisplay
    {
        get
        {
            if (SourceMenus.Count == 0) return "-";
            var first = SourceMenus[0].MenuName;
            if (SourceMenus.Count == 1) return first;
            return $"{first} 외 {SourceMenus.Count - 1}";
        }
    }

    public string SourceMenusTooltip => SourceMenus.Count == 0
        ? string.Empty
        : string.Join("\n", SourceMenus.Select(m => $"{m.ServiceDate:M/d} {m.MealTypeName} {m.MenuName} ({m.Quantity:0.###}{m.Unit})"));
}

/// <summary>조회 기간 옵션.</summary>
public sealed record PeriodOption(string Code, string Label);

/// <summary>정렬 옵션.</summary>
public sealed record SortOption(string Code, string Label);

/// <summary>
/// 발주 관리 화면 ViewModel.
/// 기간 조회 → 식단 Snapshot 집계(필요량) + 저장된 사용자 입력 병합 → 편집/묶음/일괄 변경.
/// </summary>
public partial class OrdersViewModel : ObservableObject
{
    private readonly OrderService _service;
    private readonly IMessageService _messages;
    private readonly IDialogService _dialogs;
    private readonly ILogger<OrdersViewModel> _logger;
    private bool _suppressDirty;

    public OrdersViewModel(
        OrderService service,
        IMessageService messages,
        IDialogService dialogs,
        ILogger<OrdersViewModel> logger)
    {
        _service = service;
        _messages = messages;
        _dialogs = dialogs;
        _logger = logger;

        QueryCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        GroupOrderCommand = new AsyncRelayCommand(GroupOrderAsync);
        BulkUpdateCommand = new AsyncRelayCommand(BulkUpdateAsync);
        SelectAllCommand = new RelayCommand<bool>(SelectAll);

        var today = DateTime.Today;
        FromDate = today.AddDays(-7);
        ToDate = today.AddDays(7);

        _ = LoadAsync();
    }

    // ---- 기간 ----

    public IReadOnlyList<PeriodOption> PeriodOptions { get; } =
    [
        new("2weeks", "2주"),
        new("1week", "1주"),
        new("1month", "1개월"),
        new("custom", "직접 지정"),
    ];

    [ObservableProperty]
    private PeriodOption selectedPeriod = new("2weeks", "2주");

    [ObservableProperty]
    private DateTime fromDate;

    [ObservableProperty]
    private DateTime toDate;

    [ObservableProperty]
    private string periodLabel = string.Empty;

    // ---- 보기 모드 / 정렬 ----

    [ObservableProperty]
    private bool isByIngredient = true;

    [ObservableProperty]
    private bool isByDate;

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new("ingredient", "재료명"),
        new("date", "사용일"),
        new("orderDate", "발주일"),
        new("deliveryDate", "배송일"),
        new("status", "상태"),
    ];

    [ObservableProperty]
    private SortOption selectedSort = new("ingredient", "재료명");

    // ---- 목록 ----

    public ObservableCollection<OrderItemRowViewModel> Items { get; } = [];

    [ObservableProperty]
    private OrderItemRowViewModel? selectedItem;

    public ObservableCollection<OrderSourceMenuDto> SelectedSourceMenus { get; } = [];

    [ObservableProperty]
    private bool allSelected;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private bool isBusy;

    public int SelectedCount => Items.Count(r => r.IsSelected);

    public string SaveStatusText => IsDirty ? "● 변경사항 있음" : "✓ 저장됨";

    public string SaveStatusColor => IsDirty ? "Warning" : "Success";

    // ---- Commands ----

    public IAsyncRelayCommand QueryCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand GroupOrderCommand { get; }

    public IAsyncRelayCommand BulkUpdateCommand { get; }

    public RelayCommand<bool> SelectAllCommand { get; }

    // ---- 기간/보기 변경 ----

    partial void OnSelectedPeriodChanged(PeriodOption value)
    {
        var today = DateTime.Today;
        var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        switch (value.Code)
        {
            case "1week":
                FromDate = monday;
                ToDate = monday.AddDays(4);
                break;
            case "2weeks":
                FromDate = monday;
                ToDate = monday.AddDays(11);
                break;
            case "1month":
                FromDate = today.AddDays(-15);
                ToDate = today.AddDays(15);
                break;
        }

        if (value.Code != "custom")
        {
            _ = LoadAsync();
        }
    }

    partial void OnIsByIngredientChanged(bool value)
    {
        if (value)
        {
            SelectedSort = SortOptions[0]; // 재료명
        }
        else
        {
            SelectedSort = SortOptions[1]; // 사용일
        }
    }

    partial void OnSelectedSortChanged(SortOption value) => ApplySort();

    partial void OnSelectedItemChanged(OrderItemRowViewModel? value)
    {
        SelectedSourceMenus.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var menu in value.SourceMenus)
        {
            SelectedSourceMenus.Add(menu);
        }
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(SaveStatusColor));
    }

    partial void OnAllSelectedChanged(bool value) => SelectAll(value);

    // ---- 조회 ----

    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (IsDirty && !_messages.Confirm("변경 내용이 있습니다. 조회하면 저장하지 않은 변경이 사라집니다. 계속하시겠습니까?"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _service.GetOrdersAsync(
                    DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate));

                _suppressDirty = true;
                Items.Clear();
                foreach (var dto in result.Items)
                {
                    var row = new OrderItemRowViewModel
                    {
                        Id = dto.Id,
                        ServiceDate = dto.ServiceDate,
                        IngredientId = dto.IngredientId,
                        IngredientName = dto.IngredientName,
                        RequiredQuantity = dto.RequiredQuantity,
                        RequiredUnit = dto.RequiredUnit,
                        SuggestedOrderQuantity = dto.SuggestedOrderQuantity,
                        SuggestedUnit = dto.SuggestedUnit,
                        PackageCompatible = dto.PackageCompatible,
                        PackageQuantity = dto.PackageQuantity,
                        PackageUnit = dto.PackageUnit,
                        InPlan = dto.InPlan,
                        OrderGroupId = dto.OrderGroupId,
                        OrderGroupQuantity = dto.OrderGroupQuantity,
                        OrderGroupUnit = dto.OrderGroupUnit,
                        SourceMenus = dto.SourceMenus,
                        OrderQuantity = dto.OrderQuantity,
                        OrderUnit = dto.OrderUnit,
                        OrderDate = dto.OrderDate?.ToDateTime(TimeOnly.MinValue),
                        DeliveryDate = dto.DeliveryDate?.ToDateTime(TimeOnly.MinValue),
                        Status = dto.Status,
                        OrderNote = dto.OrderNote,
                    };
                    row.PropertyChanged += OnRowPropertyChanged;
                    Items.Add(row);
                }

                _suppressDirty = false;
                IsDirty = false;
                AllSelected = false;
                SelectedItem = null;
                PeriodLabel = $"{result.StartDate:yyyy-MM-dd} ~ {result.EndDate:yyyy-MM-dd} ({result.Items.Count}건)";
                ApplySort();
            }
            finally
            {
                IsBusy = false;
            }
        });
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDirty)
        {
            return;
        }

        if (e.PropertyName is nameof(OrderItemRowViewModel.OrderQuantity)
            or nameof(OrderItemRowViewModel.OrderUnit)
            or nameof(OrderItemRowViewModel.OrderDate)
            or nameof(OrderItemRowViewModel.DeliveryDate)
            or nameof(OrderItemRowViewModel.Status)
            or nameof(OrderItemRowViewModel.OrderNote))
        {
            IsDirty = true;
        }

        if (e.PropertyName == nameof(OrderItemRowViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    private void ApplySort()
    {
        if (Items.Count == 0)
        {
            return;
        }

        var sorted = SelectedSort.Code switch
        {
            "date" => Items.OrderBy(r => r.ServiceDate).ThenBy(r => r.IngredientName),
            "orderDate" => Items.OrderBy(r => r.OrderDate ?? DateTime.MaxValue).ThenBy(r => r.IngredientName),
            "deliveryDate" => Items.OrderBy(r => r.DeliveryDate ?? DateTime.MaxValue).ThenBy(r => r.IngredientName),
            "status" => Items.OrderBy(r => r.Status).ThenBy(r => r.IngredientName),
            _ => Items.OrderBy(r => r.IngredientName).ThenBy(r => r.ServiceDate),
        };

        var ordered = sorted.ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var index = Items.IndexOf(ordered[i]);
            if (index != i)
            {
                Items.Move(index, i);
            }
        }
    }

    private void SelectAll(bool value)
    {
        foreach (var row in Items)
        {
            row.IsSelected = value;
        }
    }

    // ---- 저장 ----

    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var inputs = Items.Select(ToSaveInput).ToList();
            await _service.SaveItemsAsync(inputs);
            IsDirty = false;
            _messages.ShowInfo("저장되었습니다.");
            await LoadAsync();
        });
    }

    // ---- 묶음 발주 ----

    private async Task GroupOrderAsync()
    {
        var selected = Items.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _messages.ShowError("묶을 항목을 선택해 주세요.");
            return;
        }

        var firstKey = IngredientKey(selected[0]);
        if (selected.Any(r => IngredientKey(r) != firstKey))
        {
            _messages.ShowError("서로 다른 식재료를 하나의 묶음 발주로 묶을 수 없습니다.");
            return;
        }

        var totalRequired = selected.Sum(r => r.RequiredQuantity ?? 0);
        var requiredUnit = selected.FirstOrDefault(r => r.RequiredUnit is not null)?.RequiredUnit;
        var suggested = OrderQuantityCalculator.CalculateSuggested(
            totalRequired, requiredUnit, selected[0].PackageQuantity, selected[0].PackageUnit);
        var suggestedUnit = OrderQuantityCalculator.SuggestedUnit(requiredUnit, selected[0].PackageUnit);
        var defaultOrderDate = selected.Min(r => r.OrderDate) ?? selected.Min(r => r.ServiceDate).AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var defaultDeliveryDate = selected.Max(r => r.DeliveryDate) ?? selected.Max(r => r.ServiceDate).ToDateTime(TimeOnly.MinValue);

        var selection = _dialogs.ShowGroupOrderDialog(
            selected[0].IngredientName,
            totalRequired,
            requiredUnit,
            suggested,
            suggestedUnit,
            DateOnly.FromDateTime(defaultOrderDate),
            DateOnly.FromDateTime(defaultDeliveryDate));
        if (selection is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var inputs = selected.Select(ToSaveInput).ToList();
            await _service.CreateOrderGroupAsync(new OrderGroupCreateInput(
                inputs, selection.OrderQuantity, selection.OrderUnit, selection.OrderDate, selection.DeliveryDate));
            IsDirty = false;
            _messages.ShowInfo("묶음 발주가 생성되었습니다.");
            await LoadAsync();
        });
    }

    // ---- 일괄 변경 ----

    private async Task BulkUpdateAsync()
    {
        var selected = Items.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _messages.ShowError("변경할 항목을 선택해 주세요.");
            return;
        }

        var selection = _dialogs.ShowBulkUpdateDialog();
        if (selection is null)
        {
            return;
        }

        if (selection.OrderDate is null && selection.DeliveryDate is null && selection.Status is null)
        {
            _messages.ShowError("변경할 항목이 없습니다.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            var inputs = selected.Select(ToSaveInput).ToList();
            var updated = await _service.BulkUpdateAsync(new OrderBulkUpdateInput(
                inputs, selection.OrderDate, selection.DeliveryDate, selection.Status));
            _messages.ShowInfo($"{updated}개 항목이 변경되었습니다.");
            await LoadAsync();
        });
    }

    // ---- 내부 ----

    private static string IngredientKey(OrderItemRowViewModel row)
        => row.IngredientId is int id ? id.ToString() : $"name:{row.IngredientName}";

    private static OrderItemSaveInput ToSaveInput(OrderItemRowViewModel row)
        => new(
            row.ServiceDate,
            row.IngredientId,
            row.IngredientName,
            row.RequiredQuantity,
            row.RequiredUnit,
            row.OrderQuantity,
            row.OrderUnit,
            row.OrderDate is null ? null : DateOnly.FromDateTime(row.OrderDate.Value),
            row.DeliveryDate is null ? null : DateOnly.FromDateTime(row.DeliveryDate.Value),
            row.Status,
            row.OrderNote);

    private async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OrderException ex)
        {
            _messages.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "발주 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
    }
}
