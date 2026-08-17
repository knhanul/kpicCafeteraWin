using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 배식 생성/수정/삭제, 기간 조회, 계획식수 재계산, 보존식, 실제 식수 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py
/// </summary>
public class WorkspaceServiceTests
{
    private static readonly DateOnly Monday = new(2026, 8, 17);

    // =======================================================================
    // 배식 생성
    // =======================================================================

    [Fact]
    public async Task CreateService_Weekday_SucceedsWithDefaults()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        Assert.Equal(Monday, created.ServiceDate);
        Assert.Equal(MealType.LUNCH, created.MealType);
        Assert.Equal("중식", created.MealTypeName);
        Assert.Equal(400, created.PlannedCount); // 기본 계획식수 복사
        Assert.Equal(new TimeOnly(11, 40), created.ServiceTime); // 기본 배식시간 복사
    }

    [Fact]
    public async Task CreateService_Dinner_CopiesDinnerDefaults()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.DINNER));

        Assert.Equal(100, created.PlannedCount);
        Assert.Equal(new TimeOnly(17, 30), created.ServiceTime);
    }

    [Fact]
    public async Task CreateService_SameDateAndType_ReturnsExisting()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var first = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var second = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateService_SameDateDifferentType_CreatesBoth()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var lunch = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var dinner = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.DINNER));

        Assert.NotEqual(lunch.Id, dinner.Id);
    }

    [Theory]
    [InlineData(2026, 8, 15)] // 토
    [InlineData(2026, 8, 16)] // 일
    public async Task CreateService_Weekend_IsRejected(int year, int month, int day)
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        await Assert.ThrowsAsync<WeekendServiceNotAllowedException>(() =>
            service.CreateServiceAsync(new ServiceCreateInput(new DateOnly(year, month, day), MealType.LUNCH)));
    }

    // =======================================================================
    // 기간 조회
    // =======================================================================

    [Fact]
    public async Task GetPeriod_AnyWeekday_StartsFromMonday()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        // 수요일 선택 → 월요일 시작
        var period = await service.GetPeriodAsync(new DateOnly(2026, 8, 19), 2);

        Assert.Equal(Monday, period.StartDate);
        Assert.Equal(2, period.WeekCount);
        Assert.Equal(2, period.Weeks.Count);
        Assert.Equal(5, period.Weeks[0].Days.Count); // 월~금만
        Assert.Equal("월요일", period.Weeks[0].Days[0].Weekday);
        Assert.Equal("금요일", period.Weeks[0].Days[4].Weekday);
    }

    [Fact]
    public async Task GetPeriod_IncludesServicesGroupedByDay()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.DINNER));
        await service.CreateServiceAsync(new ServiceCreateInput(Monday.AddDays(1), MealType.LUNCH));

        var period = await service.GetPeriodAsync(Monday, 1);
        var mondayDay = period.Weeks[0].Days[0];
        var tuesdayDay = period.Weeks[0].Days[1];

        Assert.Equal(2, mondayDay.Services.Count); // 중식 + 석식 (정렬)
        Assert.Equal(MealType.LUNCH, mondayDay.Services[0].MealType);
        Assert.Equal(MealType.DINNER, mondayDay.Services[1].MealType);
        Assert.Single(tuesdayDay.Services);
        Assert.Empty(period.Weeks[0].Days[2].Services); // 수요일
    }

    // =======================================================================
    // 배식 수정 / 계획식수 재계산
    // =======================================================================

    [Fact]
    public async Task UpdateService_PlannedCount_RecalculatesQuantityTotal()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        // 재료 + 메뉴 + 레시피(100인 기준 10) 생성
        var ingredient = await master.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        var menu = await master.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        await master.CreateRecipeAsync(menu.Id, new RecipeInput("기본", null, false, true,
            [new RecipeItemInput(ingredient.Id, null, 10, null, false)]));

        // 배식 생성 (400명) + 메뉴 추가 → total = 10 * 400 / 100 = 40
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menu.Id, null));
        Assert.Equal(40.0, withMenu.Menus[0].Ingredients[0].QuantityTotal);

        // 계획식수 500으로 변경 → total = 10 * 500 / 100 = 50
        var updated = await workspace.UpdateServiceAsync(serviceDto.Id, new ServiceUpdateInput(500, "11:40", null, null));
        Assert.Equal(500, updated.PlannedCount);
        Assert.Equal(50.0, updated.Menus[0].Ingredients[0].QuantityTotal);
    }

    [Fact]
    public async Task UpdateService_SavesConceptTitleAndNote()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var updated = await service.UpdateServiceAsync(created.Id, new ServiceUpdateInput(350, "12:00", "여름 보양식", "내부 메모"));

        Assert.Equal(350, updated.PlannedCount);
        Assert.Equal(new TimeOnly(12, 0), updated.ServiceTime);
        Assert.Equal("여름 보양식", updated.ConceptTitle);
        Assert.Equal("내부 메모", updated.Note);
    }

    [Fact]
    public async Task UpdateService_NegativePlannedCount_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<KpicCafeteria.Application.Workspace.InvalidPlannedCountException>(() =>
            service.UpdateServiceAsync(created.Id, new ServiceUpdateInput(-1, null, null, null)));
    }

    [Fact]
    public async Task DeleteService_RemovesService()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await service.DeleteServiceAsync(created.Id);

        var period = await service.GetPeriodAsync(Monday, 1);
        Assert.Empty(period.Weeks[0].Days[0].Services);
    }

    // =======================================================================
    // 보존식
    // =======================================================================

    [Fact]
    public async Task Preservation_SaveAndUpdate()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var saved = await service.SavePreservationAsync(created.Id, new PreservationInput(
            new DateTime(2026, 8, 17, 13, 20, 0, DateTimeKind.Utc), "홍길동", "-18",
            new DateTime(2026, 8, 18, 13, 20, 0, DateTimeKind.Utc), "김수거", "13:20", "비고", true));

        Assert.Equal("홍길동", saved.ManagerName);
        Assert.Equal("-18", saved.FreezerTemperature);
        Assert.True(saved.Completed);
        Assert.NotNull(saved.CompletedAt);

        var updated = await service.SavePreservationAsync(created.Id, new PreservationInput(
            null, "이관리", null, null, null, null, null, false));
        Assert.Equal("이관리", updated.ManagerName);
        Assert.False(updated.Completed);
        Assert.Null(updated.CompletedAt);
    }

    [Fact]
    public async Task Preservation_CompletedToggle_SetsAndClearsCompletedAt()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        var completed = await service.SavePreservationAsync(created.Id, new PreservationInput(null, null, null, null, null, null, null, true));
        Assert.True(completed.Completed);
        Assert.NotNull(completed.CompletedAt);

        var cleared = await service.SavePreservationAsync(created.Id, new PreservationInput(null, null, null, null, null, null, null, false));
        Assert.False(cleared.Completed);
        Assert.Null(cleared.CompletedAt);
    }

    [Fact]
    public async Task Preservation_OnePerService()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await service.SavePreservationAsync(created.Id, new PreservationInput(null, "홍길동", null, null, null, null, null, true));
        await service.SavePreservationAsync(created.Id, new PreservationInput(null, "이관리", null, null, null, null, null, true));

        using var db = harness.CreateContext();
        Assert.Single(db.PreservationRecords.Where(r => r.MealServiceId == created.Id));
    }

    // =======================================================================
    // 실제 식수
    // =======================================================================

    [Fact]
    public async Task Actual_SaveSetsRecordedAt_ClearSetsNull()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        var saved = await service.SaveActualAsync(created.Id, new ActualInput(380, "비고"));
        Assert.Equal(380, saved.ActualCount);
        Assert.NotNull(saved.RecordedAt);

        var cleared = await service.SaveActualAsync(created.Id, new ActualInput(null, null));
        Assert.Null(cleared.ActualCount);
        Assert.Null(cleared.RecordedAt);
    }

    [Fact]
    public async Task Actual_NegativeCount_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<InvalidActualCountException>(() =>
            service.SaveActualAsync(created.Id, new ActualInput(-1, null)));
    }

    [Fact]
    public async Task Actual_IsIndependentFromPreservation()
    {
        using var harness = new WorkspaceTestHarness();
        var service = harness.CreateWorkspaceService();

        var created = await service.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        await service.SavePreservationAsync(created.Id, new PreservationInput(null, "홍길동", null, null, null, null, null, true));
        var actual = await service.SaveActualAsync(created.Id, new ActualInput(380, null));

        Assert.Equal(380, actual.ActualCount);
        Assert.NotNull(actual.RecordedAt);

        // 보존식 완료 상태는 실제 식수와 무관하게 유지
        var preservation = await service.GetPreservationAsync(created.Id);
        Assert.True(preservation.Completed);
    }
}
