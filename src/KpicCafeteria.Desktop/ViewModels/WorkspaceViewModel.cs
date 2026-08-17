using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Desktop.Services;
using KpicCafeteria.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>주간 보드의 하루(월~금) 컬럼.</summary>
public partial class WorkspaceDayViewModel : ObservableObject
{
    public DateOnly Date { get; init; }

    public string Weekday { get; init; } = string.Empty;

    public string DateLabel { get; init; } = string.Empty;

    public string WeekLabel { get; init; } = string.Empty;

    public bool IsToday { get; init; }

    public ObservableCollection<MealServiceBoardViewModel> Services { get; } = [];

    public MealServiceBoardViewModel? LunchService =>
        Services.FirstOrDefault(s => s.MealType == MealType.LUNCH);

    public MealServiceBoardViewModel? DinnerService =>
        Services.FirstOrDefault(s => s.MealType == MealType.DINNER);

    public IReadOnlyList<MealServiceBoardViewModel> ExtraServices =>
        Services.Where(s => s.MealType != MealType.LUNCH && s.MealType != MealType.DINNER).ToList();
}

/// <summary>주간 보드의 배식 카드.</summary>
public partial class MealServiceBoardViewModel : ObservableObject
{
    public int Id { get; init; }

    public DateOnly ServiceDate { get; init; }

    public MealType MealType { get; init; }

    public string MealTypeName { get; init; } = string.Empty;

    [ObservableProperty]
    private int plannedCount;

    [ObservableProperty]
    private string menuSummary = string.Empty;

    [ObservableProperty]
    private bool preservationCompleted;

    [ObservableProperty]
    private bool actualRecorded;

    [ObservableProperty]
    private bool isSelected;

    public string StatusText
    {
        get
        {
            var parts = new List<string>();
            if (PreservationCompleted)
            {
                parts.Add("보존식✓");
            }

            if (ActualRecorded)
            {
                parts.Add("실제식수✓");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : string.Empty;
        }
    }
}

/// <summary>식단 작성 모드의 메뉴 행.</summary>
public partial class ServiceMenuRowViewModel : ObservableObject
{
    private readonly Func<string, int?> _resolveIngredientId;

    public ServiceMenuRowViewModel(Func<string, int?> resolveIngredientId)
    {
        _resolveIngredientId = resolveIngredientId;
    }

    public int Id { get; init; }

    public int? MenuId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? RecipeName { get; init; }

    public int? RecipeVersion { get; init; }

    [ObservableProperty]
    private string? note;

    [ObservableProperty]
    private bool isRepresentative;

    public ObservableCollection<ServiceIngredientRowViewModel> Ingredients { get; } = [];
}

/// <summary>식단 작성 모드의 재료 행 (재료명 단일 소스, ID 파생).</summary>
public partial class ServiceIngredientRowViewModel : ObservableObject
{
    private readonly Func<string, int?> _resolveIngredientId;

    public ServiceIngredientRowViewModel(Func<string, int?> resolveIngredientId)
    {
        _resolveIngredientId = resolveIngredientId;
    }

    public int? IngredientId { get; private set; }

    [ObservableProperty]
    private string ingredientName = string.Empty;

    [ObservableProperty]
    private double? quantityTotal;

    [ObservableProperty]
    private double? quantityPer100;

    [ObservableProperty]
    private string? unit;

    public void SetLoaded(int? ingredientId, string name)
    {
        IngredientId = ingredientId;
        IngredientName = name;
    }

    partial void OnIngredientNameChanged(string value)
        => IngredientId = _resolveIngredientId(value.Trim());
}

/// <summary>조리지시 모드의 메뉴 행.</summary>
public partial class CookingInstructionRowViewModel : ObservableObject
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private string? cookingInstruction;

    [ObservableProperty]
    private string? cookingNote;
}

/// <summary>
/// 주간 급식 운영 화면 ViewModel.
/// ViewModel → WorkspaceService → Repository → DbContext 구조를 유지한다.
/// </summary>
public partial class WorkspaceViewModel : ObservableObject
{
    private readonly WorkspaceService _service;
    private readonly MasterDataService _masterData;
    private readonly IMessageService _messages;
    private readonly IDialogService _dialogs;
    private readonly ILogger<WorkspaceViewModel> _logger;
    private readonly Dictionary<string, int> _ingredientNameToId = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly _weekStart;
    private int _normalWeekCount = 2;
    private int? _selectedServiceId;
    private bool _suppressSelection;

    public WorkspaceViewModel(
        WorkspaceService service,
        MasterDataService masterData,
        IMessageService messages,
        IDialogService dialogs,
        ILogger<WorkspaceViewModel> logger)
    {
        _service = service;
        _masterData = masterData;
        _messages = messages;
        _dialogs = dialogs;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(() => LoadPeriodAsync());
        PrevWeekCommand = new AsyncRelayCommand(PrevWeekAsync);
        NextWeekCommand = new AsyncRelayCommand(NextWeekAsync);
        TodayCommand = new AsyncRelayCommand(TodayAsync);
        CreateServiceCommand = new AsyncRelayCommand<WorkspaceDayViewModel>(CreateServiceAsync);
        SelectServiceCommand = new RelayCommand<MealServiceBoardViewModel>(SelectService);
        OpenMenuPickerCommand = new AsyncRelayCommand(OpenMenuPickerAsync);
        SaveMealEditorCommand = new AsyncRelayCommand(SaveMealEditorAsync);
        MoveMenuUpCommand = new RelayCommand<ServiceMenuRowViewModel>(MoveMenuUp);
        MoveMenuDownCommand = new RelayCommand<ServiceMenuRowViewModel>(MoveMenuDown);
        DeleteMenuCommand = new AsyncRelayCommand<ServiceMenuRowViewModel>(DeleteMenuAsync);
        ChangeRecipeCommand = new AsyncRelayCommand<ServiceMenuRowViewModel>(ChangeRecipeAsync);
        AddIngredientRowCommand = new RelayCommand(AddIngredientRow);
        RemoveIngredientRowCommand = new RelayCommand<ServiceIngredientRowViewModel>(RemoveIngredientRow);
        SaveCookingCommand = new AsyncRelayCommand(SaveCookingAsync);
        SavePreservationCommand = new AsyncRelayCommand(SavePreservationAsync);
        SaveActualCommand = new AsyncRelayCommand(SaveActualAsync);
        ToggleFocusCommand = new RelayCommand(ToggleFocus);
        CloseEditorCommand = new RelayCommand(CloseEditor);

        _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Today));
        _ = LoadPeriodAsync();
        _ = LoadIngredientOptionsAsync();
    }

    // ---- 기간 ----

    public ObservableCollection<WorkspaceDayViewModel> Days { get; } = [];

    [ObservableProperty]
    private string periodLabel = string.Empty;

    [ObservableProperty]
    private int weekCount = 2;

    [ObservableProperty]
    private bool isFocusMode;

    public IReadOnlyList<int> WeekCountOptions => [1, 2, 4, 6, 8];

    // ---- 선택 ----

    [ObservableProperty]
    private WorkspaceDayViewModel? selectedDay;

    [ObservableProperty]
    private MealServiceBoardViewModel? selectedService;

    [ObservableProperty]
    private int selectedMode; // 0=식단작성 1=조리지시 2=보존식 3=실제식수

    // ---- 식단 작성 편집 ----

    [ObservableProperty]
    private int plannedCount;

    [ObservableProperty]
    private string serviceTime = string.Empty;

    [ObservableProperty]
    private string? conceptTitle;

    [ObservableProperty]
    private string? serviceNote;

    [ObservableProperty]
    private bool editorDirty;

    public string SaveStatusText => EditorDirty ? "변경사항 있음" : "저장됨";

    public string SaveStatusColor => EditorDirty ? "Warning" : "Success";

    public ObservableCollection<ServiceMenuRowViewModel> EditorMenus { get; } = [];

    [ObservableProperty]
    private ServiceMenuRowViewModel? selectedEditorMenu;

    public ObservableCollection<IngredientOption> IngredientOptions { get; } = [];

    // ---- 조리지시 ----

    public ObservableCollection<CookingInstructionRowViewModel> CookingRows { get; } = [];

    // ---- 보존식 ----

    [ObservableProperty]
    private string? preservationManager;

    [ObservableProperty]
    private string? preservationFreezerTemp;

    [ObservableProperty]
    private string? preservationCollectionTime;

    [ObservableProperty]
    private string? preservationCollector;

    [ObservableProperty]
    private string? preservationNote;

    [ObservableProperty]
    private DateTime? preservationCollectedAt;

    [ObservableProperty]
    private DateTime? preservationDisposalAt;

    [ObservableProperty]
    private bool preservationCompleted;

    // ---- 실제 식수 ----

    [ObservableProperty]
    private int? actualCount;

    [ObservableProperty]
    private string? actualNote;

    // ---- Commands ----

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand PrevWeekCommand { get; }

    public IAsyncRelayCommand NextWeekCommand { get; }

    public IAsyncRelayCommand TodayCommand { get; }

    public IAsyncRelayCommand<WorkspaceDayViewModel> CreateServiceCommand { get; }

    public RelayCommand<MealServiceBoardViewModel> SelectServiceCommand { get; }

    public IAsyncRelayCommand OpenMenuPickerCommand { get; }

    public IAsyncRelayCommand SaveMealEditorCommand { get; }

    public RelayCommand<ServiceMenuRowViewModel> MoveMenuUpCommand { get; }

    public RelayCommand<ServiceMenuRowViewModel> MoveMenuDownCommand { get; }

    public IAsyncRelayCommand<ServiceMenuRowViewModel> DeleteMenuCommand { get; }

    public IAsyncRelayCommand<ServiceMenuRowViewModel> ChangeRecipeCommand { get; }

    public RelayCommand AddIngredientRowCommand { get; }

    public RelayCommand<ServiceIngredientRowViewModel> RemoveIngredientRowCommand { get; }

    public IAsyncRelayCommand SaveCookingCommand { get; }

    public IAsyncRelayCommand SavePreservationCommand { get; }

    public IAsyncRelayCommand SaveActualCommand { get; }

    public RelayCommand ToggleFocusCommand { get; }

    public RelayCommand CloseEditorCommand { get; }

    // ---- 선택 처리 ----

    partial void OnSelectedServiceChanged(MealServiceBoardViewModel? value)
    {
        if (value is null || _suppressSelection)
        {
            return;
        }

        if (EditorDirty && !_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 이동하시겠습니까?"))
        {
            _suppressSelection = true;
            SelectedService = null;
            _suppressSelection = false;
            return;
        }

        _selectedServiceId = value.Id;

        foreach (var day in Days)
        {
            foreach (var svc in day.Services)
            {
                svc.IsSelected = svc.Id == value.Id;
            }
        }

        _ = ExecuteAsync(() => LoadServiceDetailAsync(value.Id));
    }

    partial void OnSelectedModeChanged(int value)
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        _ = ExecuteAsync(() => LoadServiceDetailAsync(_selectedServiceId.Value));
    }

    partial void OnWeekCountChanged(int value)
    {
        if (!IsFocusMode)
        {
            _normalWeekCount = value;
        }

        _ = LoadPeriodAsync();
    }

    partial void OnPlannedCountChanged(int value) => EditorDirty = true;

    partial void OnServiceTimeChanged(string value) => EditorDirty = true;

    partial void OnConceptTitleChanged(string? value) => EditorDirty = true;

    partial void OnServiceNoteChanged(string? value) => EditorDirty = true;

    partial void OnEditorDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(SaveStatusColor));
    }

    // ---- 기간 로드/이동 ----

    private async Task LoadPeriodAsync()
    {
        await ExecuteAsync(async () =>
        {
            var period = await _service.GetPeriodAsync(_weekStart, WeekCount);
            Days.Clear();
            for (var weekIndex = 0; weekIndex < period.Weeks.Count; weekIndex++)
            {
                var week = period.Weeks[weekIndex];
                for (var dayIndex = 0; dayIndex < week.Days.Count; dayIndex++)
                {
                    var day = week.Days[dayIndex];
                    var dayVm = new WorkspaceDayViewModel
                    {
                        Date = day.Date,
                        Weekday = day.Weekday,
                        DateLabel = $"{day.Date.Month}/{day.Date.Day}",
                        WeekLabel = dayIndex == 0 ? $"{weekIndex + 1}주차" : string.Empty,
                        IsToday = day.Date == DateOnly.FromDateTime(DateTime.Today),
                    };
                    foreach (var service in day.Services)
                    {
                        dayVm.Services.Add(new MealServiceBoardViewModel
                        {
                            Id = service.Id,
                            ServiceDate = service.ServiceDate,
                            MealType = service.MealType,
                            MealTypeName = service.MealTypeName,
                            PlannedCount = service.PlannedCount,
                            MenuSummary = string.Join("\n", service.Menus.Select(m => m.Name)),
                            PreservationCompleted = service.PreservationCompleted,
                            ActualRecorded = service.ActualRecorded,
                        });
                    }

                    Days.Add(dayVm);
                }
            }

            PeriodLabel = $"{period.StartDate:yyyy-MM-dd} ~ {period.EndDate:yyyy-MM-dd} ({WeekCount}주)";
        });
    }

    private async Task PrevWeekAsync()
    {
        _weekStart = _weekStart.AddDays(-7 * WeekCount);
        await LoadPeriodAsync();
    }

    private async Task NextWeekAsync()
    {
        _weekStart = _weekStart.AddDays(7 * WeekCount);
        await LoadPeriodAsync();
    }

    private async Task TodayAsync()
    {
        _weekStart = MondayOf(DateOnly.FromDateTime(DateTime.Today));
        await LoadPeriodAsync();
    }

    private static DateOnly MondayOf(DateOnly value)
    {
        var dayOfWeek = (int)value.DayOfWeek;
        return dayOfWeek == 0 ? value.AddDays(-6) : value.AddDays(1 - dayOfWeek);
    }

    // ---- 배식 생성 ----

    private async Task CreateServiceAsync(WorkspaceDayViewModel? day)
    {
        if (day is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            // 중식 먼저, 이미 있으면 석식
            MealType type = MealType.LUNCH;
            if (day.Services.Any(s => s.MealType == MealType.LUNCH))
            {
                type = MealType.DINNER;
            }

            if (day.Services.Any(s => s.MealType == type))
            {
                _messages.ShowInfo("이미 배식이 있습니다.");
                return;
            }

            var created = await _service.CreateServiceAsync(new ServiceCreateInput(day.Date, type));
            await LoadPeriodAsync();
            var board = Days.First(d => d.Date == day.Date).Services.First(s => s.Id == created.Id);
            _suppressSelection = true;
            SelectedDay = Days.First(d => d.Date == day.Date);
            SelectedService = board;
            _suppressSelection = false;
            await LoadServiceDetailAsync(created.Id);
        });
    }

    private void SelectService(MealServiceBoardViewModel? service)
    {
        if (service is null)
        {
            return;
        }

        // OnSelectedServiceChanged가 dirty 확인 후 상세를 로드한다.
        SelectedService = service;
    }

    // ---- 상세 로드 ----

    private async Task LoadServiceDetailAsync(int serviceId)
    {
        await ExecuteAsync(async () =>
        {
            var dto = await _service.GetServiceAsync(serviceId);

            PlannedCount = dto.PlannedCount;
            ServiceTime = dto.ServiceTime?.ToString("HH:mm") ?? string.Empty;
            ConceptTitle = dto.ConceptTitle;
            ServiceNote = dto.Note;
            EditorDirty = false;

            // 식단 작성 모드 메뉴
            EditorMenus.Clear();
            foreach (var menu in dto.Menus)
            {
                var row = new ServiceMenuRowViewModel(ResolveIngredientId)
                {
                    Id = menu.Id,
                    MenuId = menu.MenuId,
                    Name = menu.Name,
                    RecipeName = menu.RecipeName,
                    RecipeVersion = menu.RecipeVersion,
                    Note = menu.Note,
                    IsRepresentative = menu.IsRepresentative,
                };
                foreach (var ingredient in menu.Ingredients)
                {
                    var ingRow = new ServiceIngredientRowViewModel(ResolveIngredientId);
                    ingRow.SetLoaded(ingredient.IngredientId, ingredient.Name);
                    ingRow.QuantityTotal = ingredient.QuantityTotal;
                    ingRow.QuantityPer100 = ingredient.QuantityPer100;
                    ingRow.Unit = ingredient.Unit;
                    row.Ingredients.Add(ingRow);
                }

                EditorMenus.Add(row);
            }

            // 조리지시 모드 메뉴
            CookingRows.Clear();
            foreach (var menu in dto.Menus)
            {
                CookingRows.Add(new CookingInstructionRowViewModel
                {
                    Id = menu.Id,
                    Name = menu.Name,
                    CookingInstruction = menu.CookingInstruction,
                    CookingNote = menu.CookingNote,
                });
            }

            // 보존식
            var preservation = await _service.GetPreservationAsync(serviceId);
            PreservationManager = preservation.ManagerName;
            PreservationFreezerTemp = preservation.FreezerTemperature;
            PreservationCollectionTime = preservation.CollectionTime;
            PreservationCollector = preservation.CollectorName;
            PreservationNote = preservation.Note;
            PreservationCollectedAt = preservation.CollectedAt;
            PreservationDisposalAt = preservation.DisposalAt;
            PreservationCompleted = preservation.Completed;

            // 실제 식수
            var actual = await _service.GetActualAsync(serviceId);
            ActualCount = actual.ActualCount;
            ActualNote = actual.Note;
        });
    }

    // ---- 식단 작성 저장 ----

    private async Task SaveMealEditorAsync()
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var menus = EditorMenus.Select(menu => new MealEditorMenuInput(
                menu.Id,
                menu.Note,
                menu.IsRepresentative,
                menu.Ingredients
                    .Where(i => !string.IsNullOrWhiteSpace(i.IngredientName))
                    .Select(i => new MealEditorIngredientInput(i.IngredientId, i.IngredientName, i.QuantityTotal, i.Unit))
                    .ToList())).ToList();

            await _service.SaveMealEditorAsync(_selectedServiceId.Value, new MealEditorInput(
                PlannedCount, ServiceTime, ConceptTitle, ServiceNote, menus));

            EditorDirty = false;
            await LoadPeriodAsync();
            await LoadServiceDetailAsync(_selectedServiceId.Value);
            _messages.ShowInfo("저장되었습니다.");
        });
    }

    // ---- 메뉴 추가/삭제/순서/레시피 ----

    private async Task OpenMenuPickerAsync()
    {
        if (_selectedServiceId is null)
        {
            _messages.ShowInfo("먼저 배식을 선택해 주세요.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            var selected = _dialogs.ShowMenuPicker(_selectedServiceId.Value);
            if (selected is not { Count: > 0 })
            {
                return;
            }

            var items = selected.Select((item, index) => new BatchAddMenuItemInput(item.MenuId, item.SelectedRecipeId, index + 1)).ToList();
            await _service.BatchAddMenusAsync(_selectedServiceId.Value, items);
            await LoadPeriodAsync();
            await LoadServiceDetailAsync(_selectedServiceId.Value);
        });
    }

    private void MoveMenuUp(ServiceMenuRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = EditorMenus.IndexOf(row);
        if (index <= 0)
        {
            return;
        }

        EditorMenus.Move(index, index - 1);
        _ = ApplyReorderAsync();
    }

    private void MoveMenuDown(ServiceMenuRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = EditorMenus.IndexOf(row);
        if (index < 0 || index >= EditorMenus.Count - 1)
        {
            return;
        }

        EditorMenus.Move(index, index + 1);
        _ = ApplyReorderAsync();
    }

    private async Task ApplyReorderAsync()
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.ReorderMenusAsync(_selectedServiceId.Value, EditorMenus.Select(m => m.Id).ToList());
            await LoadPeriodAsync();
            await LoadServiceDetailAsync(_selectedServiceId.Value);
        });
    }

    private async Task DeleteMenuAsync(ServiceMenuRowViewModel? row)
    {
        if (row is null || _selectedServiceId is null)
        {
            return;
        }

        if (!_messages.Confirm($"메뉴 '{row.Name}'을(를) 식단에서 삭제하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.DeleteServiceMenuAsync(row.Id);
            await LoadPeriodAsync();
            await LoadServiceDetailAsync(_selectedServiceId.Value);
        });
    }

    private async Task ChangeRecipeAsync(ServiceMenuRowViewModel? row)
    {
        if (row is null || row.MenuId is null || _selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var recipeId = _dialogs.ShowRecipePicker(row.MenuId.Value);
            if (recipeId is null)
            {
                return;
            }

            await _service.ChangeServiceMenuRecipeAsync(row.Id, recipeId.Value);
            await LoadServiceDetailAsync(_selectedServiceId.Value);
            _messages.ShowInfo("레시피가 변경되었습니다. 기존 재료는 새 레시피 재료로 교체됩니다.");
        });
    }

    private void AddIngredientRow()
        => SelectedEditorMenu?.Ingredients.Add(new ServiceIngredientRowViewModel(ResolveIngredientId));

    private void RemoveIngredientRow(ServiceIngredientRowViewModel? row)
    {
        if (row is null || SelectedEditorMenu is null)
        {
            return;
        }

        SelectedEditorMenu.Ingredients.Remove(row);
    }

    private int? ResolveIngredientId(string name)
        => _ingredientNameToId.TryGetValue(name, out var id) ? id : null;

    // ---- 조리지시 저장 ----

    private async Task SaveCookingAsync()
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            foreach (var row in CookingRows)
            {
                await _service.UpdateServiceMenuAsync(row.Id, new ServiceMenuInput(null, false, row.CookingInstruction, row.CookingNote));
            }

            _messages.ShowInfo("저장되었습니다.");
        });
    }

    // ---- 보존식 저장 ----

    private async Task SavePreservationAsync()
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.SavePreservationAsync(_selectedServiceId.Value, new PreservationInput(
                PreservationCollectedAt, PreservationManager, PreservationFreezerTemp,
                PreservationDisposalAt, PreservationCollector, PreservationCollectionTime,
                PreservationNote, PreservationCompleted));

            await LoadPeriodAsync();
            _messages.ShowInfo("저장되었습니다.");
        });
    }

    // ---- 실제 식수 저장 ----

    private async Task SaveActualAsync()
    {
        if (_selectedServiceId is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.SaveActualAsync(_selectedServiceId.Value, new ActualInput(ActualCount, ActualNote));
            await LoadPeriodAsync();
            _messages.ShowInfo("저장되었습니다.");
        });
    }

    // ---- 집중 작성 모드 ----

    private void ToggleFocus()
    {
        IsFocusMode = !IsFocusMode;
        if (IsFocusMode)
        {
            _normalWeekCount = WeekCount;
            WeekCount = 1;
        }
        else
        {
            WeekCount = _normalWeekCount;
        }
    }

    private void CloseEditor()
    {
        _suppressSelection = true;
        SelectedService = null;
        _selectedServiceId = null;
        _suppressSelection = false;
        foreach (var day in Days)
        {
            foreach (var svc in day.Services)
            {
                svc.IsSelected = false;
            }
        }
    }

    // ---- 재료 옵션 ----

    private async Task LoadIngredientOptionsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await _masterData.SearchIngredientsAsync(null, null, true, limit: 1000);
            IngredientOptions.Clear();
            _ingredientNameToId.Clear();
            foreach (var item in result.Items)
            {
                IngredientOptions.Add(new IngredientOption(item.Id, item.Name, item.DefaultUnit));
                _ingredientNameToId[item.Name] = item.Id;
            }
        });
    }

    // ---- 공통 ----

    private async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (WorkspaceException ex)
        {
            _messages.ShowError(ex.Message);
        }
        catch (MasterDataException ex)
        {
            _messages.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "주간 급식 운영 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
    }
}
