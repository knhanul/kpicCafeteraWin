using KpicCafeteria.Documents.Hwpx;

namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 문서 렌더러.
/// 기존 hwpx_engine.py MealPlanRenderer/CookingInstructionRenderer/PreservedFoodRenderer에 대응.
/// </summary>
public interface IDocumentRenderer
{
    void Render(HwpxTemplateEngine engine, object payload);
}

public static class DocumentRendererFactory
{
    public static IDocumentRenderer For(string documentType)
    {
        return documentType switch
        {
            "MEAL_PLAN" => new MealPlanRenderer(),
            "COOKING_INSTRUCTION" => new CookingInstructionRenderer(),
            "PRESERVATION_RECORD" => new PreservedFoodRenderer(),
            _ => throw new HwpxTemplateError($"지원하지 않는 문서 유형입니다: {documentType}"),
        };
    }
}

public sealed class MealPlanRenderer : IDocumentRenderer
{
    private static readonly string[] RemovedPlaceholderFields = ["ORIGIN_INFO", "NOTICE", "W1_LUNCH_TIME_INFO", "W2_LUNCH_TIME_INFO", "DINNER_TIME_INFO"];

    public void Render(HwpxTemplateEngine engine, object payload)
    {
        var mealPlan = (MealPlanPayload)payload;
        var weeks = mealPlan.Weeks;
        var repeatConfig = RepeatPageConfigProvider.ByDocumentType.GetValueOrDefault("MEAL_PLAN");
        if (repeatConfig is not null && engine.ApplyRepeatPages(RepeatPages(mealPlan, repeatConfig.ItemsPerPage)))
        {
            return;
        }

        if (weeks.Count == 0)
        {
            Clear(engine);
            return;
        }

        engine.SetField("PERIOD_TITLE", PeriodTitle(weeks));
        ClearRemovedPlaceholders(engine);

        for (var weekIndex = 0; weekIndex < 2; weekIndex++)
        {
            var week = weekIndex < weeks.Count ? weeks[weekIndex] : null;
            var days = week?.Days ?? [];
            engine.SetField($"W{weekIndex + 1}_WEEK_LABEL", week?.WeekLabel ?? "");
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                var day = dayIndex < days.Count ? days[dayIndex] : null;
                var prefix = $"W{weekIndex + 1}_D{dayIndex + 1}";
                engine.SetField($"{prefix}_DATE", day?.DateLabel ?? "");
                engine.SetMultilineField($"{prefix}_LUNCH_MENU", MenuLines(day?.Lunch));
                engine.SetMultilineField($"{prefix}_DINNER_MENU", MenuLines(day?.Dinner));
            }
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> RepeatPages(MealPlanPayload payload, int weeksPerPage)
    {
        var weeks = payload.Weeks;
        var chunks = weeks.Count > 0
            ? Enumerable.Range(0, (weeks.Count + weeksPerPage - 1) / weeksPerPage)
                .Select(index => weeks.Skip(index * weeksPerPage).Take(weeksPerPage).ToList())
                .ToList()
            : new List<List<MealPlanWeekPayload>> { new() };

        var pages = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var chunk in chunks)
        {
            var fields = new Dictionary<string, object?>
            {
                ["PERIOD_TITLE"] = PeriodTitle(weeks),
            };
            foreach (var fieldName in RemovedPlaceholderFields)
            {
                fields[fieldName] = "";
            }

            for (var weekIndex = 0; weekIndex < 2; weekIndex++)
            {
                var week = weekIndex < chunk.Count ? chunk[weekIndex] : null;
                var days = week?.Days ?? [];
                fields[$"W{weekIndex + 1}_WEEK_LABEL"] = week?.WeekLabel ?? "";
                for (var dayIndex = 0; dayIndex < 5; dayIndex++)
                {
                    var day = dayIndex < days.Count ? days[dayIndex] : null;
                    var prefix = $"W{weekIndex + 1}_D{dayIndex + 1}";
                    fields[$"{prefix}_DATE"] = day?.DateLabel ?? "";
                    fields[$"{prefix}_LUNCH_MENU"] = MenuLines(day?.Lunch);
                    fields[$"{prefix}_DINNER_MENU"] = MenuLines(day?.Dinner);
                }
            }

            pages.Add(fields);
        }

        return pages;
    }

    private static void Clear(HwpxTemplateEngine engine)
    {
        engine.SetField("PERIOD_TITLE", "");
        ClearRemovedPlaceholders(engine);
        for (var weekIndex = 0; weekIndex < 2; weekIndex++)
        {
            engine.SetField($"W{weekIndex + 1}_WEEK_LABEL", "");
            for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            {
                var prefix = $"W{weekIndex + 1}_D{dayIndex + 1}";
                engine.SetField($"{prefix}_DATE", "");
                engine.SetField($"{prefix}_LUNCH_MENU", "");
                engine.SetField($"{prefix}_DINNER_MENU", "");
            }
        }
    }

    private static void ClearRemovedPlaceholders(HwpxTemplateEngine engine)
    {
        foreach (var fieldName in RemovedPlaceholderFields)
        {
            engine.SetField(fieldName, "");
        }
    }

    private static string PeriodTitle(IReadOnlyList<MealPlanWeekPayload> weeks)
    {
        if (weeks.Count == 0)
        {
            return "식단표";
        }

        return WeekLabel.PeriodLabel(weeks[0].Start, weeks[^1].End);
    }

    private static string MenuLines(MealPlanServicePayload? service)
    {
        if (service is null || service.Menus.Count == 0)
        {
            return "";
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(service.ConceptTitle))
        {
            lines.Add($"[{service.ConceptTitle}]");
        }

        lines.AddRange(service.Menus.Select(menu => menu.Trim()).Where(line => line.Length > 0));
        return string.Join("\n", lines);
    }
}

public sealed class CookingInstructionRenderer : IDocumentRenderer
{
    private int _leftParaPrId = 100;
    private int _blueCharPrId = 100;

    public void Render(HwpxTemplateEngine engine, object payload)
    {
        var cooking = (CookingPayload)payload;
        var days = cooking.Days;
        (_leftParaPrId, _blueCharPrId) = engine.EnsureCookingStyles();
        ApplyIngredientsAlignment(engine);

        var repeatConfig = RepeatPageConfigProvider.ByDocumentType.GetValueOrDefault("COOKING_INSTRUCTION");
        if (repeatConfig is not null && engine.ApplyRepeatPages(RepeatPages(days, repeatConfig.ItemsPerPage, cooking.PeriodLabel)))
        {
            return;
        }

        engine.SetField("PERIOD_TITLE", cooking.PeriodLabel);
        engine.Package.EnsureSectionCount(Math.Max(1, days.Count));
        for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
        {
            var sectionName = $"Contents/section{dayIndex}.xml";
            engine.SetField("DATE_LABEL", days[dayIndex].DateLabel, sectionName);
            RenderMeal(engine, days[dayIndex], sectionName, "LUNCH");
            RenderMeal(engine, days[dayIndex], sectionName, "DINNER");
        }

        if (days.Count == 0)
        {
            engine.SetField("DATE_LABEL", "");
            ClearMeal(engine, null, "LUNCH");
            ClearMeal(engine, null, "DINNER");
        }
    }

    private void ApplyIngredientsAlignment(HwpxTemplateEngine engine)
    {
        foreach (var name in engine.Package.SectionNames())
        {
            if (!engine.Package.Files.ContainsKey(name))
            {
                continue;
            }

            var root = engine.Package.ReadXml(name);
            var changed = false;
            foreach (var paragraph in root.Descendants().Where(n => n.Name.LocalName == "p"))
            {
                var texts = OwnTextNodes(paragraph);
                var joined = string.Concat(texts.Select(t => t.Value));
                if (joined.Contains("INGREDIENTS", StringComparison.Ordinal) && joined.Contains("{{", StringComparison.Ordinal))
                {
                    paragraph.SetAttributeValue("paraPrIDRef", _leftParaPrId.ToString());
                    changed = true;
                }
            }

            if (changed)
            {
                engine.Package.WriteXml(name, root);
            }
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> RepeatPages(
        IReadOnlyList<CookingDayPayload> days, int daysPerPage, string periodLabel)
    {
        var chunks = days.Count > 0
            ? Enumerable.Range(0, (days.Count + daysPerPage - 1) / daysPerPage)
                .Select(index => days.Skip(index * daysPerPage).Take(daysPerPage).ToList())
                .ToList()
            : new List<List<CookingDayPayload>> { new() };

        var pages = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var chunk in chunks)
        {
            var day = chunk.Count > 0 ? chunk[0] : null;
            var fields = new Dictionary<string, object?>
            {
                ["DATE_LABEL"] = day?.DateLabel ?? "",
                ["PERIOD_TITLE"] = periodLabel,
            };
            foreach (var (key, value) in MealFields(day, "LUNCH"))
            {
                fields[key] = value;
            }

            foreach (var (key, value) in MealFields(day, "DINNER"))
            {
                fields[key] = value;
            }

            pages.Add(fields);
        }

        return pages;
    }

    private static IReadOnlyDictionary<string, object?> MealFields(CookingDayPayload? day, string mealType)
    {
        var meal = Meal(day, mealType);
        var menus = meal?.Menus ?? [];
        var fields = new Dictionary<string, object?>
        {
            [$"{mealType}_TITLE"] = meal?.MealName ?? "",
        };

        const int slots = 7;
        for (var slot = 1; slot <= slots; slot++)
        {
            var prefix = $"{mealType}_MENU_{slot}";
            var ingredientPrefix = $"{mealType}_INGREDIENTS_{slot}";
            if (slot <= menus.Count)
            {
                var menu = menus[slot - 1];
                if (slot == slots && menus.Count > slots)
                {
                    var remaining = menus.Skip(slot - 1).ToList();
                    fields[prefix] = MenuName(remaining[0]);
                    fields[ingredientPrefix] = remaining.Select(MenuBlockText).ToList();
                    break;
                }

                fields[prefix] = MenuName(menu);
                var lines = IngredientLines(menu);
                var note = NoteLine(menu);
                if (note.Length > 0)
                {
                    lines.Add(note);
                }

                fields[ingredientPrefix] = lines;
            }
            else
            {
                fields[prefix] = "";
                fields[ingredientPrefix] = "";
            }
        }

        return fields;
    }

    private void RenderMeal(HwpxTemplateEngine engine, CookingDayPayload day, string sectionName, string mealType)
    {
        var meal = Meal(day, mealType);
        var menus = meal?.Menus ?? [];
        const int slots = 7;
        for (var slot = 1; slot <= slots; slot++)
        {
            var prefix = $"{mealType}_MENU_{slot}";
            var ingredientPrefix = $"{mealType}_INGREDIENTS_{slot}";
            if (slot <= menus.Count)
            {
                var menu = menus[slot - 1];
                if (slot == slots && menus.Count > slots)
                {
                    var remaining = menus.Skip(slot - 1).ToList();
                    engine.SetField(prefix, MenuName(remaining[0]), sectionName);
                    engine.SetMultilineField(ingredientPrefix, remaining.Select(MenuBlockText).ToList(), sectionName);
                    break;
                }

                engine.SetField(prefix, MenuName(menu), sectionName);
                engine.SetMultilineFieldWithNoteColor(ingredientPrefix, IngredientLines(menu), NoteLine(menu), _blueCharPrId, sectionName);
            }
            else
            {
                engine.SetField(prefix, "", sectionName);
                engine.SetField(ingredientPrefix, "", sectionName);
            }
        }

        var mealTitle = meal?.MealName ?? "";
        if (mealTitle.Length > 0)
        {
            engine.SetField($"{mealType}_TITLE", mealTitle, sectionName);
        }
    }

    private static void ClearMeal(HwpxTemplateEngine engine, string? sectionName, string mealType)
    {
        for (var slot = 1; slot <= 7; slot++)
        {
            engine.SetField($"{mealType}_MENU_{slot}", "", sectionName);
            engine.SetField($"{mealType}_INGREDIENTS_{slot}", "", sectionName);
        }

        engine.SetField($"{mealType}_TITLE", "", sectionName);
    }

    private static CookingMealPayload? Meal(CookingDayPayload? day, string mealType)
    {
        if (day is null)
        {
            return null;
        }

        return day.Services.FirstOrDefault(service => service.MealType == mealType);
    }

    private static string MenuName(CookingMenuPayload menu) => menu.Name.Trim();

    private static List<string> IngredientLines(CookingMenuPayload menu)
    {
        var parts = new List<string>();
        foreach (var ingredient in menu.Ingredients)
        {
            var name = ingredient.Name.Trim();
            var quantity = ingredient.QuantityTotal;
            var unit = ingredient.Unit;
            parts.Add(quantity is not null && unit.Length > 0 ? $"{name} {quantity}{unit}" : name);
        }

        var lines = new List<string>();
        if (parts.Count > 0)
        {
            lines.Add(string.Join(", ", parts));
        }

        if (menu.Instruction.Length > 0)
        {
            lines.Add(menu.Instruction);
        }

        return lines;
    }

    private static string NoteLine(CookingMenuPayload menu) => menu.Note;

    private static string MenuBlockText(CookingMenuPayload menu)
    {
        var lines = new List<string> { MenuName(menu) };
        lines.AddRange(IngredientLines(menu));
        var note = NoteLine(menu);
        if (note.Length > 0)
        {
            lines.Add(note);
        }

        return string.Join("\n", lines.Where(line => line.Length > 0));
    }

    private static List<System.Xml.Linq.XElement> OwnTextNodes(System.Xml.Linq.XElement paragraph)
    {
        var result = new List<System.Xml.Linq.XElement>();
        Walk(paragraph);
        return result;

        void Walk(System.Xml.Linq.XElement element)
        {
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName == "p")
                {
                    continue;
                }

                if (child.Name.LocalName == "t")
                {
                    result.Add(child);
                }

                Walk(child);
            }
        }
    }
}

public sealed class PreservedFoodRenderer : IDocumentRenderer
{
    public void Render(HwpxTemplateEngine engine, object payload)
    {
        var preservation = (PreservationPayload)payload;
        var records = preservation.Records;
        var periodLabel = preservation.PeriodLabel;

        var repeatConfig = RepeatPageConfigProvider.ByDocumentType.GetValueOrDefault("PRESERVATION_RECORD");
        if (repeatConfig is not null && engine.ApplyRepeatPages(RepeatPages(records, repeatConfig.ItemsPerPage, periodLabel)))
        {
            return;
        }

        engine.SetField("PERIOD_TITLE", periodLabel);
        var sectionCount = Math.Max(1, (records.Count + 2) / 3);
        engine.Package.EnsureSectionCount(sectionCount);
        for (var sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
        {
            var sectionName = $"Contents/section{sectionIndex}.xml";
            var chunk = records.Skip(sectionIndex * 3).Take(3).ToList();
            for (var slot = 1; slot <= 3; slot++)
            {
                var record = slot - 1 < chunk.Count ? chunk[slot - 1] : null;
                var prefix = $"B{slot}";
                engine.SetField($"{prefix}_DATE_LABEL", RecordLabel(record), sectionName);
                engine.SetField($"{prefix}_SAMPLE_HOUR", SampleHour(record), sectionName);
                engine.SetField($"{prefix}_SAMPLE_MINUTE", SampleMinute(record), sectionName);
                engine.SetField($"{prefix}_MANAGER", record?.ManagerName ?? "", sectionName);
                engine.SetMultilineField($"{prefix}_MENU_LIST", record?.MenuItems ?? [], sectionName);
                engine.SetField($"{prefix}_FREEZER_TEMP", record?.FreezerTemperature ?? "", sectionName);
                engine.SetField($"{prefix}_DISCARD_DATETIME", record?.DiscardDatetime ?? "", sectionName);
                engine.SetField($"{prefix}_COLLECTOR", record?.CollectorName ?? "", sectionName);
                engine.SetField($"{prefix}_COLLECTION_TIME", record?.CollectionTime ?? "", sectionName);
            }
        }

        if (records.Count == 0)
        {
            engine.SetField("PERIOD_TITLE", "");
            foreach (var slot in new[] { 1, 2, 3 })
            {
                var prefix = $"B{slot}";
                engine.SetField($"{prefix}_DATE_LABEL", "");
                engine.SetField($"{prefix}_SAMPLE_HOUR", "");
                engine.SetField($"{prefix}_SAMPLE_MINUTE", "");
                engine.SetField($"{prefix}_MANAGER", "");
                engine.SetField($"{prefix}_MENU_LIST", "");
                engine.SetField($"{prefix}_FREEZER_TEMP", "");
                engine.SetField($"{prefix}_DISCARD_DATETIME", "");
                engine.SetField($"{prefix}_COLLECTOR", "");
                engine.SetField($"{prefix}_COLLECTION_TIME", "");
            }
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> RepeatPages(
        IReadOnlyList<PreservationRecordPayload> records, int itemsPerPage, string periodLabel)
    {
        var chunks = records.Count > 0
            ? Enumerable.Range(0, (records.Count + itemsPerPage - 1) / itemsPerPage)
                .Select(index => records.Skip(index * itemsPerPage).Take(itemsPerPage).ToList())
                .ToList()
            : new List<List<PreservationRecordPayload>> { new() };

        var pages = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var chunk in chunks)
        {
            var fields = new Dictionary<string, object?> { ["PERIOD_TITLE"] = periodLabel };
            for (var slot = 1; slot <= 3; slot++)
            {
                var record = slot - 1 < chunk.Count ? chunk[slot - 1] : null;
                var prefix = $"B{slot}";
                fields[$"{prefix}_DATE_LABEL"] = RecordLabel(record);
                fields[$"{prefix}_SAMPLE_HOUR"] = SampleHour(record);
                fields[$"{prefix}_SAMPLE_MINUTE"] = SampleMinute(record);
                fields[$"{prefix}_MANAGER"] = record?.ManagerName ?? "";
                fields[$"{prefix}_MENU_LIST"] = record?.MenuItems ?? [];
                fields[$"{prefix}_FREEZER_TEMP"] = record?.FreezerTemperature ?? "";
                fields[$"{prefix}_DISCARD_DATETIME"] = record?.DiscardDatetime ?? "";
                fields[$"{prefix}_COLLECTOR"] = record?.CollectorName ?? "";
                fields[$"{prefix}_COLLECTION_TIME"] = record?.CollectionTime ?? "";
            }

            pages.Add(fields);
        }

        return pages;
    }

    private static string RecordLabel(PreservationRecordPayload? record)
    {
        return record?.DateLabel ?? "";
    }

    private static string SampleHour(PreservationRecordPayload? record)
        => record?.SampleDatetime is { } dt ? dt.Hour.ToString() : "";

    private static string SampleMinute(PreservationRecordPayload? record)
        => record?.SampleDatetime is { } dt ? dt.Minute.ToString() : "";
}
