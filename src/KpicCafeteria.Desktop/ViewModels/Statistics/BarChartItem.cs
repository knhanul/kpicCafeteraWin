namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>가로 막대 차트 항목.</summary>
public sealed record BarChartItem(string Label, double Value, string ValueText, string? Color = null);
