using System.Windows;
using System.Windows.Controls;

namespace KpicCafeteria.Desktop.Controls;

/// <summary>KPI 요약 카드 (라벨 + 값 + 보조 텍스트).</summary>
public partial class KpiCard : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubProperty = DependencyProperty.Register(
        nameof(Sub), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public KpiCard()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Sub
    {
        get => (string)GetValue(SubProperty);
        set => SetValue(SubProperty, value);
    }
}
