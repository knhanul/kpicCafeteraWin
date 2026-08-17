using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KpicCafeteria.Desktop.ViewModels.Statistics;

namespace KpicCafeteria.Desktop.Controls;

/// <summary>
/// 가로 막대 차트 컨트롤.
/// 기존 Web 구현의 CSS 막대와 동일한 시각 표현을 ItemsControl로 구현한다.
/// (외부 차트 라이브러리 의존성 없음)
/// </summary>
public partial class BarChart : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IReadOnlyList<BarChartItem>),
        typeof(BarChart),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public BarChart()
    {
        InitializeComponent();
    }

    public IReadOnlyList<BarChartItem>? ItemsSource
    {
        get => (IReadOnlyList<BarChartItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>최대값 대비 백분율로 변환된 표시 행.</summary>
    public IReadOnlyList<BarChartRow> Rows { get; private set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (BarChart)d;
        var items = e.NewValue as IReadOnlyList<BarChartItem>;
        if (items is null || items.Count == 0)
        {
            chart.Rows = [];
        }
        else
        {
            var max = items.Max(i => i.Value);
            chart.Rows = items
                .Select(i => new BarChartRow(
                    i.Label,
                    max > 0 ? Math.Round(i.Value / max * 100, 1) : 0,
                    i.ValueText,
                    i.Color ?? "#2563EB"))
                .ToList();
        }

        chart.OnPropertyChanged(nameof(Rows));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>막대 차트 표시 행 (백분율 기반).</summary>
public sealed record BarChartRow(string Label, double Percent, string ValueText, string Color)
{
    public double Remainder => Math.Max(0, 100 - Percent);
}

/// <summary>백분율(double) → GridLength(Star) 변환.</summary>
public sealed class PercentToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new GridLength(System.Convert.ToDouble(value, CultureInfo.InvariantCulture), GridUnitType.Star);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
