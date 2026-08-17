using System.Text.Json;

namespace KpicCafeteria.Documents.Hwpx;

/// <summary>반복 페이지 설정 (template-page-config.json).</summary>
public sealed record RepeatPageConfig(
    string TemplateType,
    string CapacityToken,
    int ItemsPerPage,
    IReadOnlyList<string> LocalSlots,
    string PageRule);

/// <summary>
/// 문서 유형별 반복 페이지 설정.
/// 기존 hwpx_engine.py REPEAT_PAGE_CONFIG_BY_DOCUMENT_TYPE에 대응.
/// </summary>
public static class RepeatPageConfigProvider
{
    private static readonly Dictionary<string, int> CapacityToCount = new()
    {
        ["2-weeks"] = 2,
        ["1-day-2-meals"] = 1,
        ["3-meals"] = 3,
    };

    private static readonly Dictionary<string, string> DocumentTypeToTemplateType = new()
    {
        ["MEAL_PLAN"] = "meal-plan",
        ["COOKING_INSTRUCTION"] = "cooking-instruction",
        ["PRESERVATION_RECORD"] = "preserved-food",
    };

    private static readonly Dictionary<string, RepeatPageConfig> DefaultConfig = new()
    {
        ["meal-plan"] = new RepeatPageConfig("meal-plan", "2-weeks", 2, ["W1", "W2"], "ceil(week_count/2)"),
        ["cooking-instruction"] = new RepeatPageConfig("cooking-instruction", "1-day-2-meals", 1, ["LUNCH", "DINNER"], "selected_day_count"),
        ["preserved-food"] = new RepeatPageConfig("preserved-food", "3-meals", 3, ["B1", "B2", "B3"], "ceil(meal_count/3)"),
    };

    private static readonly Lazy<IReadOnlyDictionary<string, RepeatPageConfig>> _byDocumentType = new(Build);

    public static IReadOnlyDictionary<string, RepeatPageConfig> ByDocumentType => _byDocumentType.Value;

    private static Dictionary<string, RepeatPageConfig> Build()
    {
        var byTemplateType = new Dictionary<string, RepeatPageConfig>();
        foreach (var (_, entry) in LoadRawConfig())
        {
            if (entry is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var templateType = GetString(element, "type");
            var capacityToken = GetString(element, "capacity");
            if (string.IsNullOrWhiteSpace(templateType) || !CapacityToCount.TryGetValue(capacityToken, out var itemsPerPage))
            {
                continue;
            }

            var localSlots = GetString(element, "local_slots")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            byTemplateType[templateType] = new RepeatPageConfig(
                templateType, capacityToken, itemsPerPage, localSlots, GetString(element, "page_rule"));
        }

        var result = new Dictionary<string, RepeatPageConfig>();
        foreach (var (documentType, templateType) in DocumentTypeToTemplateType)
        {
            if (byTemplateType.TryGetValue(templateType, out var config))
            {
                result[documentType] = config;
            }
        }

        return result;
    }

    private static Dictionary<string, object> LoadRawConfig()
    {
        try
        {
            var stream = typeof(RepeatPageConfigProvider).Assembly.GetManifestResourceStream("KpicCafeteria.Documents.Templates.template-page-config.json");
            if (stream is null)
            {
                return DefaultConfig.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            }

            using var reader = new StreamReader(stream);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            return loaded ?? DefaultConfig.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        }
        catch (Exception)
        {
            return DefaultConfig.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        }
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}
