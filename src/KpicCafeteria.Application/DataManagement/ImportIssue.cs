namespace KpicCafeteria.Application.DataManagement;

/// <summary>이관 검증 Issue (오류 또는 경고).</summary>
public sealed record ImportIssue(
    string Type,
    string Message,
    string? Sheet = null,
    int? Row = null);
