using CommunityToolkit.Mvvm.ComponentModel;
using KpicCafeteria.Desktop.Services;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>
/// 문서 출력 기간/형식 입력 대화상자 ViewModel.
/// </summary>
public partial class DocumentOutputDialogViewModel : ObservableObject
{
    public DocumentOutputDialogViewModel(
        string documentType,
        DateOnly defaultStartDate,
        DateOnly defaultEndDate)
    {
        DocumentType = documentType;
        StartDate = defaultStartDate.ToDateTime(TimeOnly.MinValue);
        EndDate = defaultEndDate.ToDateTime(TimeOnly.MinValue);
    }

    public string DocumentType { get; }

    [ObservableProperty]
    private DateTime startDate;

    [ObservableProperty]
    private DateTime endDate;

    [ObservableProperty]
    private bool generatePdf;

    public DocumentOutputSelection? Result { get; private set; }

    public void Confirm()
    {
        Result = new DocumentOutputSelection(
            DateOnly.FromDateTime(StartDate),
            DateOnly.FromDateTime(EndDate),
            GeneratePdf);
    }
}
