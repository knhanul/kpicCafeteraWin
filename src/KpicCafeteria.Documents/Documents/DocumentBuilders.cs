using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Documents.Hwpx;

namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 엔티티 → 문서 DTO 빌더.
/// 기존 document_builders.py에 대응.
/// </summary>
public static class MealPlanDocumentBuilder
{
    public static MealPlanDocumentDto Build(IReadOnlyList<MealService> services, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        if (services.Count == 0)
        {
            throw new InvalidOperationException("출력할 배식이 없습니다.");
        }

        var servicesByDate = services
            .GroupBy(s => s.ServiceDate)
            .ToDictionary(g => g.Key, g => g.ToDictionary(s => s.MealType, s => s));

        var first = startDate ?? services.Min(s => s.ServiceDate);
        var last = endDate ?? services.Max(s => s.ServiceDate);
        var monday = first.AddDays(-((int)first.DayOfWeek + 6) % 7);

        var weeks = new List<MealPlanWeekDto>();
        var cursor = monday;
        while (cursor <= last)
        {
            var days = Enumerable.Range(0, 5)
                .Select(offset => BuildDay(cursor.AddDays(offset), servicesByDate))
                .ToList();
            weeks.Add(new MealPlanWeekDto(cursor, cursor.AddDays(4), days));
            cursor = cursor.AddDays(7);
        }

        return new MealPlanDocumentDto(new DocumentPeriodDto(first, last), "식단표", weeks);
    }

    private static MealPlanDayDto BuildDay(DateOnly current, Dictionary<DateOnly, Dictionary<MealType, MealService>> servicesByDate)
    {
        servicesByDate.TryGetValue(current, out var dateServices);
        dateServices ??= [];
        var lunch = BuildMealBlock("lunch", dateServices.GetValueOrDefault(MealType.LUNCH));
        var dinner = BuildMealBlock("dinner", dateServices.GetValueOrDefault(MealType.DINNER));
        var weekday = WeekLabel.WeekdayLabels[(int)current.DayOfWeek];
        return new MealPlanDayDto(
            current,
            $"{current:MM.dd}({weekday[0]})",
            weekday,
            lunch,
            dinner);
    }

    private static MealPlanMealDto BuildMealBlock(string mealType, MealService? service)
    {
        if (service is null)
        {
            return new MealPlanMealDto(mealType, MealNames.Get(mealType), null, null, null, []);
        }

        return new MealPlanMealDto(
            mealType,
            MealNames.Get(service.MealType),
            service.PlannedCount,
            service.ServiceTime,
            service.ConceptTitle,
            service.Menus.Select(m => m.MenuNameSnapshot).ToList());
    }
}

/// <summary>조리지시서 빌더.</summary>
public static class CookingInstructionDocumentBuilder
{
    public static CookingInstructionDocumentDto Build(IReadOnlyList<MealService> services, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        if (services.Count == 0)
        {
            throw new InvalidOperationException("출력할 배식이 없습니다.");
        }

        var grouped = services
            .GroupBy(s => s.ServiceDate)
            .OrderBy(g => g.Key)
            .ToList();

        var days = new List<CookingInstructionDayDto>();
        foreach (var group in grouped)
        {
            var byType = group.ToDictionary(s => s.MealType, s => s);
            var current = group.Key;
            days.Add(new CookingInstructionDayDto(
                current,
                $"{current:yyyy년 MM월 dd일}",
                WeekLabel.WeekdayLabels[(int)current.DayOfWeek],
                BuildMealBlock("lunch", byType.GetValueOrDefault(MealType.LUNCH)),
                BuildMealBlock("dinner", byType.GetValueOrDefault(MealType.DINNER))));
        }

        return new CookingInstructionDocumentDto("조리지시서", days);
    }

    private static CookingInstructionMealDto BuildMealBlock(string mealType, MealService? service)
    {
        if (service is null)
        {
            return new CookingInstructionMealDto(mealType, MealNames.Get(mealType), null, null, []);
        }

        return new CookingInstructionMealDto(
            mealType,
            MealNames.Get(service.MealType),
            service.PlannedCount,
            service.ServiceTime,
            service.Menus.Select(BuildMenu).ToList());
    }

    private static CookingInstructionMenuDto BuildMenu(MealServiceMenu menu)
    {
        return new CookingInstructionMenuDto(
            menu.MenuNameSnapshot,
            menu.Ingredients.Select(BuildIngredient).ToList(),
            menu.CookingInstruction ?? "",
            menu.CookingNote ?? menu.Note ?? "");
    }

    private static CookingInstructionIngredientDto BuildIngredient(MealServiceMenuIngredient item)
    {
        return new CookingInstructionIngredientDto(
            item.IngredientNameSnapshot,
            item.QuantityTotal,
            item.QuantityPer100,
            item.Unit,
            item.SourceNote);
    }
}

/// <summary>보존식 기록지 빌더.</summary>
public static class PreservationRecordDocumentBuilder
{
    public static PreservationRecordDocumentDto Build(IReadOnlyList<MealService> services, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        if (services.Count == 0)
        {
            throw new InvalidOperationException("출력할 배식이 없습니다.");
        }

        var ordered = services
            .OrderBy(s => s.ServiceDate)
            .ThenBy(s => MealSort.Get(s.MealType))
            .ToList();

        var records = new List<PreservationRecordBlockDto>();
        foreach (var service in ordered)
        {
            var record = service.Preservation;
            records.Add(new PreservationRecordBlockDto(
                service.ServiceDate,
                $"{service.ServiceDate:yyyy년 MM월 dd일}",
                WeekLabel.WeekdayLabels[(int)service.ServiceDate.DayOfWeek],
                service.MealType == MealType.LUNCH ? "lunch" : "dinner",
                MealNames.Get(service.MealType),
                service.Menus.Select(m => m.MenuNameSnapshot).ToList(),
                record?.CollectionTime,
                record?.CollectedAt,
                record?.ManagerName,
                record?.FreezerTemperature,
                record?.DisposalAt is { } disposal ? DateOnly.FromDateTime(disposal) : null,
                record?.DisposalAt,
                record?.CollectorName));
        }

        return new PreservationRecordDocumentDto("보존식 기록지", records);
    }
}

/// <summary>식사 이름/정렬 (serializers.py MEAL_NAMES/MEAL_SORT 대응).</summary>
public static class MealNames
{
    public static string Get(string mealType) => mealType.ToUpperInvariant() switch
    {
        "LUNCH" => "중식",
        "DINNER" => "석식",
        _ => mealType,
    };

    public static string Get(MealType mealType) => mealType switch
    {
        MealType.LUNCH => "중식",
        MealType.DINNER => "석식",
        _ => mealType.ToString(),
    };
}

/// <summary>식사 정렬 순서.</summary>
public static class MealSort
{
    public static int Get(string mealType) => mealType.ToUpperInvariant() switch
    {
        "LUNCH" => 1,
        "DINNER" => 2,
        _ => 99,
    };

    public static int Get(MealType mealType) => mealType switch
    {
        MealType.LUNCH => 1,
        MealType.DINNER => 2,
        _ => 99,
    };
}
