using System.Globalization;
using System.Text.Json;
using KpicCafeteria.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KpicCafeteria.Infrastructure.Persistence;

/// <summary>
/// EF Core 값 변환기 모음.
/// DB에는 기존 Python 시스템과 호환되는 문자열/형식을 저장한다.
/// </summary>
public static class ValueConverters
{
    /// <summary>MealType → "LUNCH"/"DINNER" 문자열.</summary>
    public static readonly ValueConverter<MealType, string> MealType = new(
        value => value.ToString(),
        value => Enum.Parse<MealType>(value));

    /// <summary>OrderStatus → "pending"/"ordered"/"skipped" 소문자 문자열.</summary>
    public static readonly ValueConverter<OrderStatus, string> OrderStatusConverter = new(
        value => value.ToString().ToLowerInvariant(),
        value => Enum.Parse<OrderStatus>(value, ignoreCase: true));

    /// <summary>DateTime → UTC ISO-8601 문자열 (SQLite TEXT).</summary>
    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, string>
    {
        public UtcDateTimeConverter()
            : base(
                value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value => DateTime.SpecifyKind(
                    DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    DateTimeKind.Utc))
        {
        }
    }

    /// <summary>DateTime? → UTC ISO-8601 문자열 또는 NULL (SQLite TEXT).</summary>
    public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, string?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                value => value.HasValue ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null,
                value => string.IsNullOrEmpty(value)
                    ? null
                    : DateTime.SpecifyKind(
                        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        DateTimeKind.Utc))
        {
        }
    }

    /// <summary>JSON 객체 → TEXT 문자열 (DocumentTemplate.PlaceholderSummary, ImportJob.Summary, AuditLog.Detail).</summary>
    public sealed class JsonObjectConverter : ValueConverter<Dictionary<string, object?>, string>
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public JsonObjectConverter()
            : base(
                value => JsonSerializer.Serialize(value, Options),
                value => string.IsNullOrEmpty(value)
                    ? new Dictionary<string, object?>()
                    : JsonSerializer.Deserialize<Dictionary<string, object?>>(value, Options) ?? new Dictionary<string, object?>())
        {
        }
    }

    /// <summary>JSON 배열 → TEXT 문자열 (ImportJob.Errors).</summary>
    public sealed class JsonListConverter : ValueConverter<List<Dictionary<string, object?>>, string>
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        public JsonListConverter()
            : base(
                value => JsonSerializer.Serialize(value, Options),
                value => string.IsNullOrEmpty(value)
                    ? new List<Dictionary<string, object?>>()
                    : JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(value, Options) ?? new List<Dictionary<string, object?>>())
        {
        }
    }
}
