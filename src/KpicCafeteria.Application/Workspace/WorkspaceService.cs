using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Domain.Domain;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Application.Workspace;

/// <summary>
/// 주간 급식 운영 업무 서비스.
/// 기존 Python workspace.py / serializers.py의 업무규칙을 그대로 유지한다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py
/// C:\Pjt\kpicCafeteria\backend\app\serializers.py
/// </summary>
public sealed class WorkspaceService
{
    private static readonly IReadOnlyDictionary<MealType, string> MealTypeNames =
        new Dictionary<MealType, string> { [MealType.LUNCH] = "중식", [MealType.DINNER] = "석식" };

    private static readonly IReadOnlyDictionary<MealType, int> MealTypeSort =
        new Dictionary<MealType, int> { [MealType.LUNCH] = 1, [MealType.DINNER] = 2 };

    private static readonly string[] WeekdayNames = ["월요일", "화요일", "수요일", "목요일", "금요일"];

    private readonly IMealServiceRepositoryFactory _factory;

    public WorkspaceService(IMealServiceRepositoryFactory factory)
    {
        _factory = factory;
    }

    private IMealServiceRepository CreateRepository() => _factory.Create();

    // =======================================================================
    // 기간 조회
    // =======================================================================

    /// <summary>
    /// 주간 식단 조회. 기준일이 어느 요일이든 해당 주 월요일부터 시작하며, 월~금만 표시한다.
    /// </summary>
    public async Task<WorkspacePeriodDto> GetPeriodAsync(DateOnly weekStart, int weeks, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var monday = MondayOf(weekStart);
        var end = monday.AddDays(weeks * 7 - 1);

        var services = await repository.GetServicesInRangeAsync(monday, end, cancellationToken);
        var byDate = services
            .GroupBy(x => x.ServiceDate)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => MealTypeSort.GetValueOrDefault(x.MealType, 99)).ToList());

        var weekList = new List<WorkspaceWeekDto>();
        for (var weekIndex = 0; weekIndex < weeks; weekIndex++)
        {
            var start = monday.AddDays(weekIndex * 7);
            var days = new List<WorkspaceDayDto>();
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                var current = start.AddDays(dayIndex);
                var serviceRows = byDate.GetValueOrDefault(current, []);
                days.Add(new WorkspaceDayDto(
                    current,
                    WeekdayNames[dayIndex],
                    serviceRows.Select(s => MapService(s, includeIngredients: false)).ToList()));
            }

            weekList.Add(new WorkspaceWeekDto(start, start.AddDays(4), days));
        }

        return new WorkspacePeriodDto(monday, end, weeks, weekList);
    }

    private static DateOnly MondayOf(DateOnly value)
    {
        var dayOfWeek = (int)value.DayOfWeek; // 0=일
        return dayOfWeek == 0 ? value.AddDays(-6) : value.AddDays(1 - dayOfWeek);
    }

    // =======================================================================
    // 배식 CRUD
    // =======================================================================

    public async Task<MealServiceDto> GetServiceAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        return await ReloadAndMapAsync(repository, id, cancellationToken);
    }

    /// <summary>
    /// 배식 생성. 평일만 허용하며, 같은 (날짜, 유형)이 있으면 기존 배식을 반환한다.
    /// MealTypeSetting의 기본 계획식수/배식시간을 복사한다.
    /// </summary>
    public async Task<MealServiceDto> CreateServiceAsync(ServiceCreateInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();

        if (input.ServiceDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            throw new WeekendServiceNotAllowedException();
        }

        var existing = await repository.FindServiceAsync(input.ServiceDate, input.MealType, cancellationToken);
        if (existing is not null)
        {
            return await ReloadAndMapAsync(repository, existing.Id, cancellationToken);
        }

        var setting = await repository.FindActiveMealTypeSettingAsync(input.MealType.ToString(), cancellationToken)
            ?? throw new MealTypeSettingNotFoundException();

        var service = new MealService
        {
            ServiceDate = input.ServiceDate,
            MealType = input.MealType,
            PlannedCount = setting.DefaultPlannedCount,
            ServiceTime = setting.DefaultServiceTime,
        };
        repository.Add(service);
        await repository.SaveChangesAsync(cancellationToken);

        return await ReloadAndMapAsync(repository, service.Id, cancellationToken);
    }

    /// <summary>
    /// 배식 기본정보 수정. 계획식수 변경 시 재료 스냅샷의 quantity_total을 재계산한다.
    /// </summary>
    public async Task<MealServiceDto> UpdateServiceAsync(int id, ServiceUpdateInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(id, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        if (input.PlannedCount < 0)
        {
            throw new InvalidPlannedCountException();
        }

        service.PlannedCount = input.PlannedCount;
        service.ServiceTime = ParseClock(input.ServiceTime);
        service.ConceptTitle = input.ConceptTitle;
        service.Note = input.Note;

        // 계획식수 변경 시 quantity_total 재계산 (per_100이 null이 아닌 행만)
        foreach (var menu in service.Menus)
        {
            foreach (var ingredient in menu.Ingredients)
            {
                if (ingredient.QuantityPer100 is not null)
                {
                    ingredient.QuantityTotal = QuantityCalculator.CalculateTotal(ingredient.QuantityPer100, service.PlannedCount);
                }
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return MapService(service);
    }

    public async Task DeleteServiceAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(id, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        repository.Remove(service);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // =======================================================================
    // 메뉴 추가
    // =======================================================================

    /// <summary>
    /// 메뉴 단건 추가. 첫 주찬 메뉴는 자동 대표 지정. 레시피 재료를 스냅샷으로 복사한다.
    /// </summary>
    public async Task<MealServiceDto> AddMenuAsync(int serviceId, AddMenuInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        var menu = await repository.GetMenuWithRecipesAsync(input.MenuId, cancellationToken);
        if (menu is null || !menu.Active)
        {
            throw new MenuNotFoundException(input.MenuId);
        }

        if (service.Menus.Any(m => m.MenuId == menu.Id))
        {
            throw new MenuAlreadyAddedException();
        }

        var recipe = SelectRecipe(menu, input.RecipeId);
        var item = new MealServiceMenu
        {
            Service = service,
            MenuId = menu.Id,
            SortOrder = service.Menus.Count + 1,
            MenuNameSnapshot = menu.Name,
            IsRepresentative = !service.Menus.Any(m => m.IsRepresentative) && menu.Role == "주찬",
        };
        repository.Add(item);
        await repository.SaveChangesAsync(cancellationToken);

        CopyRecipeToServiceMenu(item, recipe, service.PlannedCount);
        await repository.SaveChangesAsync(cancellationToken);

        return await ReloadAndMapAsync(repository, serviceId, cancellationToken);
    }

    /// <summary>
    /// 메뉴 일괄 추가. 요청 내 중복/기존 중복/비활성 메뉴/잘못된 레시피를 검증한다.
    /// </summary>
    public async Task<MealServiceDto> BatchAddMenusAsync(
        int serviceId, IReadOnlyList<BatchAddMenuItemInput> items, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        if (items.Count == 0)
        {
            throw new EmptyMenuSelectionException();
        }

        var menuIds = items.Select(i => i.MenuId).ToList();
        if (menuIds.Distinct().Count() != menuIds.Count)
        {
            throw new DuplicateMenuInRequestException();
        }

        var sortOrders = items.Select(i => i.SortOrder).ToList();
        if (sortOrders.Distinct().Count() != sortOrders.Count)
        {
            throw new DuplicateSortOrderInRequestException();
        }

        var existingMenuIds = service.Menus.Select(m => m.MenuId ?? 0).ToHashSet();
        if (menuIds.Any(existingMenuIds.Contains))
        {
            throw new MenuAlreadyAddedException();
        }

        var menuRows = await repository.GetMenusWithRecipesAsync(menuIds, cancellationToken);
        var menuMap = menuRows.ToDictionary(m => m.Id);
        foreach (var mid in menuIds)
        {
            if (!menuMap.TryGetValue(mid, out var menu) || !menu.Active)
            {
                throw new MenuNotFoundException(mid);
            }
        }

        foreach (var item in items)
        {
            var menu = menuMap[item.MenuId];
            if (item.RecipeId is not null)
            {
                var recipe = menu.Recipes.FirstOrDefault(r => r.Id == item.RecipeId);
                if (recipe is null)
                {
                    throw new RecipeNotInMenuException(menu.Name);
                }

                if (!recipe.Active)
                {
                    throw new RecipeInactiveException(menu.Name);
                }
            }
        }

        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var baseSort = service.Menus.Count;
            foreach (var item in items)
            {
                var menu = menuMap[item.MenuId];
                var activeRecipes = menu.Recipes.Where(r => r.Active).ToList();
                Recipe? recipe;
                if (item.RecipeId is not null)
                {
                    recipe = activeRecipes.First(r => r.Id == item.RecipeId);
                }
                else
                {
                    recipe = activeRecipes.FirstOrDefault(r => r.IsDefault) ?? activeRecipes.FirstOrDefault();
                }

                var newItem = new MealServiceMenu
                {
                    Service = service,
                    MenuId = menu.Id,
                    SortOrder = baseSort + item.SortOrder,
                    MenuNameSnapshot = menu.Name,
                    IsRepresentative = false,
                };
                repository.Add(newItem);
                await repository.SaveChangesAsync(cancellationToken);

                CopyRecipeToServiceMenu(newItem, recipe, service.PlannedCount);
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return await ReloadAndMapAsync(repository, serviceId, cancellationToken);
    }

    // =======================================================================
    // 레시피 변경 / 식단 메뉴 편집
    // =======================================================================

    /// <summary>
    /// 식단 메뉴의 레시피 변경. 기존 재료 스냅샷을 전체 삭제 후 새 레시피 재료로 재복사한다.
    /// </summary>
    public async Task<MealServiceDto> ChangeServiceMenuRecipeAsync(int itemId, int recipeId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var item = await repository.GetServiceMenuWithMenuRecipesAsync(itemId, cancellationToken)
            ?? throw new ServiceMenuNotFoundException();
        if (item.Menu is null || item.Service is null)
        {
            throw new ServiceMenuNotFoundException();
        }

        var recipe = SelectRecipe(item.Menu, recipeId);
        CopyRecipeToServiceMenu(item, recipe, item.Service.PlannedCount);
        await repository.SaveChangesAsync(cancellationToken);

        return await ReloadAndMapAsync(repository, item.MealServiceId, cancellationToken);
    }

    /// <summary>식단 메뉴 비고/대표/조리지시 저장.</summary>
    public async Task<MealServiceDto> UpdateServiceMenuAsync(int itemId, ServiceMenuInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var item = await repository.GetServiceMenuWithMenuRecipesAsync(itemId, cancellationToken)
            ?? throw new ServiceMenuNotFoundException();

        item.Note = input.Note;
        item.CookingInstruction = input.CookingInstruction;
        item.CookingNote = input.CookingNote;

        if (input.IsRepresentative)
        {
            foreach (var sibling in item.Service!.Menus)
            {
                sibling.IsRepresentative = sibling.Id == item.Id;
            }
        }
        else
        {
            item.IsRepresentative = false;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapAsync(repository, item.MealServiceId, cancellationToken);
    }

    /// <summary>
    /// 식단 재료 스냅샷 직접 편집 (전체 교체).
    /// total/per_100 역산은 QuantityCalculator를 사용한다.
    /// </summary>
    public async Task<MealServiceDto> UpdateServiceMenuIngredientsAsync(
        int itemId, IReadOnlyList<IngredientSnapshotInput> rows, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var item = await repository.GetServiceMenuWithMenuRecipesAsync(itemId, cancellationToken)
            ?? throw new ServiceMenuNotFoundException();

        var plannedCount = item.Service!.PlannedCount;
        item.Ingredients.Clear();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var per100 = row.QuantityPer100;
            var total = row.QuantityTotal;
            if (per100 is null && total is not null)
            {
                per100 = QuantityCalculator.CalculatePer100(total, plannedCount);
            }

            if (total is null && per100 is not null)
            {
                total = QuantityCalculator.CalculateTotal(per100, plannedCount);
            }

            var ingredient = row.IngredientId is not null
                ? await repository.GetIngredientAsync(row.IngredientId.Value, cancellationToken)
                : null;

            item.Ingredients.Add(new MealServiceMenuIngredient
            {
                IngredientId = ingredient?.Id,
                SortOrder = index + 1,
                IngredientNameSnapshot = row.Name.Trim(),
                QuantityTotal = total,
                QuantityPer100 = per100,
                Unit = row.Unit ?? ingredient?.DefaultUnit,
                SourceNote = row.SourceNote,
            });
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapAsync(repository, item.MealServiceId, cancellationToken);
    }

    /// <summary>
    /// 식단 편집 일괄 저장 (배식 기본정보 + 메뉴별 비고/대표/재료).
    /// 재료는 전체 교체, 대표는 첫 True만 인정, 트랜잭션으로 처리한다.
    /// </summary>
    public async Task<MealServiceDto> SaveMealEditorAsync(int serviceId, MealEditorInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        if (input.PlannedCount < 0)
        {
            throw new InvalidPlannedCountException();
        }

        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            service.PlannedCount = input.PlannedCount;
            service.ServiceTime = ParseClock(input.ServiceTime);
            service.ConceptTitle = input.ConceptTitle;
            service.Note = input.Note;

            var representativeSet = false;
            foreach (var menuBody in input.Menus)
            {
                if (menuBody.ServiceMenuId is null)
                {
                    continue;
                }

                var item = service.Menus.FirstOrDefault(m => m.Id == menuBody.ServiceMenuId);
                if (item is null)
                {
                    continue;
                }

                item.Note = menuBody.Note;
                if (menuBody.IsRepresentative && !representativeSet)
                {
                    item.IsRepresentative = true;
                    representativeSet = true;
                }
                else
                {
                    item.IsRepresentative = false;
                }

                item.Ingredients.Clear();
                for (var index = 0; index < menuBody.Ingredients.Count; index++)
                {
                    var row = menuBody.Ingredients[index];
                    var ingredient = row.IngredientId is not null
                        ? await repository.GetIngredientAsync(row.IngredientId.Value, cancellationToken)
                        : null;
                    var per100 = QuantityCalculator.CalculatePer100(row.QuantityTotal, service.PlannedCount);

                    item.Ingredients.Add(new MealServiceMenuIngredient
                    {
                        IngredientId = ingredient?.Id,
                        SortOrder = index + 1,
                        IngredientNameSnapshot = row.Name.Trim(),
                        QuantityTotal = row.QuantityTotal,
                        QuantityPer100 = per100,
                        Unit = row.Unit ?? ingredient?.DefaultUnit,
                    });
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return await ReloadAndMapAsync(repository, serviceId, cancellationToken);
    }

    /// <summary>식단 메뉴 삭제 후 남은 메뉴 sort_order 재계산.</summary>
    public async Task<MealServiceDto> DeleteServiceMenuAsync(int itemId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var item = await repository.GetServiceMenuWithMenuRecipesAsync(itemId, cancellationToken)
            ?? throw new ServiceMenuNotFoundException();

        var serviceId = item.MealServiceId;
        var siblings = item.Service!.Menus.Where(m => m.Id != itemId).OrderBy(m => m.SortOrder).ToList();

        repository.Remove(item);
        for (var index = 0; index < siblings.Count; index++)
        {
            siblings[index].SortOrder = index + 1;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapAsync(repository, serviceId, cancellationToken);
    }

    /// <summary>메뉴 순서 변경. 전체 메뉴 ID 목록이 현재 식단과 일치해야 한다.</summary>
    public async Task<MealServiceDto> ReorderMenusAsync(int serviceId, IReadOnlyList<int> menuIds, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        var itemMap = service.Menus.ToDictionary(m => m.Id);
        if (!menuIds.ToHashSet().SetEquals(itemMap.Keys))
        {
            throw new MenuListMismatchException();
        }

        for (var index = 0; index < menuIds.Count; index++)
        {
            itemMap[menuIds[index]].SortOrder = index + 1;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapAsync(repository, serviceId, cancellationToken);
    }

    // =======================================================================
    // 보존식 / 실제 식수
    // =======================================================================

    public async Task<PreservationRecordDto> GetPreservationAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();
        return MapPreservation(service);
    }

    /// <summary>보존식 저장. 완료 체크 시 CompletedAt=현재 시각, 해제 시 null.</summary>
    public async Task<PreservationRecordDto> SavePreservationAsync(int serviceId, PreservationInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        var record = service.Preservation ?? new PreservationRecord { Service = service };
        record.CollectedAt = input.CollectedAt;
        record.ManagerName = input.ManagerName;
        record.FreezerTemperature = input.FreezerTemperature;
        record.DisposalAt = input.DisposalAt;
        record.CollectorName = input.CollectorName;
        record.CollectionTime = input.CollectionTime;
        record.Note = input.Note;
        record.CompletedAt = input.Completed ? DateTime.UtcNow : null;

        if (record.Id == 0)
        {
            repository.Add(record);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return MapPreservation(service);
    }

    public async Task<MealActualDto> GetActualAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();
        return MapActual(service);
    }

    /// <summary>실제 식수 저장. 값 입력 시 RecordedAt=현재 시각, 비우면 null.</summary>
    public async Task<MealActualDto> SaveActualAsync(int serviceId, ActualInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var service = await repository.GetServiceAsync(serviceId, cancellationToken)
            ?? throw new MealServiceNotFoundException();

        if (input.ActualCount is < 0)
        {
            throw new InvalidActualCountException();
        }

        var actual = service.Actual ?? new MealActual { Service = service };
        actual.ActualCount = input.ActualCount;
        actual.Note = input.Note;
        actual.RecordedAt = input.ActualCount is not null ? DateTime.UtcNow : null;

        if (actual.Id == 0)
        {
            repository.Add(actual);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return MapActual(service);
    }

    // =======================================================================
    // 메뉴 선택기
    // =======================================================================

    /// <summary>
    /// 메뉴 선택기 검색. 활성 메뉴만 기본 표시하며, 검색어/역할 필터로 200건 이상도 검색 가능하다.
    /// </summary>
    public async Task<MenuPickerResultDto> SearchMenuPickerAsync(
        string? query, string? role, int? serviceId, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();

        var rows = await repository.SearchMenusWithRecipesAsync(query, role, true, limit + 1, offset, cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).ToList();

        var addedMenuIds = new HashSet<int>();
        if (serviceId is not null)
        {
            var service = await repository.GetServiceAsync(serviceId.Value, cancellationToken);
            addedMenuIds = service?.Menus.Select(m => m.MenuId ?? 0).ToHashSet() ?? [];
        }

        var pickerItems = items.Select(menu =>
        {
            var activeRecipes = menu.Recipes.Where(r => r.Active).ToList();
            var defaultRecipe = activeRecipes.FirstOrDefault(r => r.IsDefault);
            return new MenuPickerItemDto(
                menu.Id,
                menu.Name,
                menu.Role,
                addedMenuIds.Contains(menu.Id),
                defaultRecipe?.Id ?? activeRecipes.FirstOrDefault()?.Id,
                activeRecipes.Select(r => new MenuPickerRecipeDto(
                    r.Id,
                    r.Name,
                    r.Version,
                    r.IsDefault,
                    r.Ingredients.Count,
                    r.Ingredients.OrderBy(i => i.SortOrder).Take(5).Select(i => i.Ingredient?.Name ?? string.Empty).ToList())).ToList());
        }).ToList();

        var total = await repository.CountMenusAsync(query, role, true, cancellationToken);
        return new MenuPickerResultDto(pickerItems, total);
    }

    // =======================================================================
    // 내부 헬퍼
    // =======================================================================

    /// <summary>
    /// 레시피 선택 규칙: 명시된 레시피 → 활성 기본 레시피 → 활성 첫 레시피 → 없음.
    /// </summary>
    private static Recipe? SelectRecipe(Menu menu, int? recipeId)
    {
        var active = menu.Recipes.Where(r => r.Active).ToList();
        if (recipeId is not null)
        {
            var recipe = active.FirstOrDefault(r => r.Id == recipeId);
            if (recipe is null)
            {
                throw new RecipeNotAvailableException();
            }

            return recipe;
        }

        return active.FirstOrDefault(r => r.IsDefault) ?? active.FirstOrDefault();
    }

    /// <summary>
    /// 레시피 재료를 식단 메뉴 스냅샷으로 복사 (기존 스냅샷 전체 삭제 후 재복사).
    /// </summary>
    private static void CopyRecipeToServiceMenu(MealServiceMenu item, Recipe? recipe, int plannedCount)
    {
        item.Ingredients.Clear();
        item.RecipeId = recipe?.Id;
        item.RecipeNameSnapshot = recipe?.Name;
        item.RecipeVersionSnapshot = recipe?.Version;
        if (recipe is null)
        {
            return;
        }

        foreach (var recipeItem in recipe.Ingredients.OrderBy(i => i.SortOrder))
        {
            item.Ingredients.Add(new MealServiceMenuIngredient
            {
                IngredientId = recipeItem.IngredientId,
                SortOrder = recipeItem.SortOrder,
                IngredientNameSnapshot = recipeItem.Ingredient?.Name ?? string.Empty,
                QuantityTotal = QuantityCalculator.CalculateTotal(recipeItem.QuantityPer100, plannedCount),
                QuantityPer100 = recipeItem.QuantityPer100,
                Unit = recipeItem.Unit ?? recipeItem.Ingredient?.DefaultUnit,
            });
        }
    }

    private static TimeOnly? ParseClock(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = TimeInput24.Normalize(value) ?? throw new InvalidServiceTimeException();
        return TimeOnly.Parse(normalized);
    }

    private static async Task<MealServiceDto> ReloadAndMapAsync(IMealServiceRepository repository, int id, CancellationToken cancellationToken)
    {
        var service = await repository.GetServiceAsync(id, cancellationToken)
            ?? throw new MealServiceNotFoundException();
        return MapService(service);
    }

    // =======================================================================
    // DTO 매핑
    // =======================================================================

    private static MealServiceDto MapService(MealService service, bool includeIngredients = true)
    {
        var preservation = service.Preservation;
        var actual = service.Actual;
        return new MealServiceDto(
            service.Id,
            service.ServiceDate,
            service.MealType,
            MealTypeNames.GetValueOrDefault(service.MealType, service.MealType.ToString()),
            service.PlannedCount,
            service.ServiceTime,
            service.ConceptTitle,
            service.Note,
            preservation?.CompletedAt is not null,
            actual?.ActualCount is not null,
            actual?.ActualCount,
            service.Menus.OrderBy(m => m.SortOrder).Select(m => MapMenu(m, includeIngredients)).ToList());
    }

    private static MealServiceMenuDto MapMenu(MealServiceMenu item, bool includeIngredients)
        => new(
            item.Id,
            item.MenuId,
            item.RecipeId,
            item.RecipeNameSnapshot ?? item.SourceRecipe?.Name,
            item.RecipeVersionSnapshot ?? item.SourceRecipe?.Version,
            item.MenuNameSnapshot,
            item.SortOrder,
            item.Note,
            item.IsRepresentative,
            item.CookingInstruction,
            item.CookingNote,
            includeIngredients
                ? item.Ingredients.OrderBy(i => i.SortOrder).Select(MapIngredient).ToList()
                : []);

    private static MealServiceIngredientDto MapIngredient(MealServiceMenuIngredient item)
        => new(
            item.Id,
            item.IngredientId,
            item.IngredientNameSnapshot,
            item.Ingredient?.StatGroup ?? "기타",
            item.QuantityTotal,
            item.QuantityPer100,
            item.Unit,
            item.SourceNote);

    private static PreservationRecordDto MapPreservation(MealService service)
    {
        var record = service.Preservation;
        return new PreservationRecordDto(
            service.Id,
            record?.CollectedAt,
            record?.ManagerName,
            record?.FreezerTemperature,
            record?.DisposalAt,
            record?.CollectorName,
            record?.CollectionTime,
            record?.Note,
            record?.CompletedAt is not null,
            record?.CompletedAt);
    }

    private static MealActualDto MapActual(MealService service)
    {
        var actual = service.Actual;
        return new MealActualDto(
            service.Id,
            service.PlannedCount,
            actual?.ActualCount,
            actual?.Note,
            actual?.RecordedAt);
    }
}
