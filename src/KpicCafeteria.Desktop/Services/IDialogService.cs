namespace KpicCafeteria.Desktop.Services;

/// <summary>선택된 메뉴/레시피 결과.</summary>
public sealed record MenuPickerSelection(int MenuId, int? SelectedRecipeId);

/// <summary>묶음 발주 입력 결과.</summary>
public sealed record GroupOrderSelection(
    double? OrderQuantity,
    string? OrderUnit,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate);

/// <summary>일괄 변경 입력 결과 (null이 아닌 항목만 적용).</summary>
public sealed record BulkUpdateSelection(
    DateOnly? OrderDate,
    DateOnly? DeliveryDate,
    string? Status);

/// <summary>문서 출력 입력 결과.</summary>
public sealed record DocumentOutputSelection(
    DateOnly StartDate,
    DateOnly EndDate,
    bool GeneratePdf);

/// <summary>
/// 대화상자 표시 서비스 (ViewModel에서 Window 직접 생성 금지).
/// </summary>
public interface IDialogService
{
    /// <summary>문서 출력 기간/형식 입력 대화상자. 취소 시 null.</summary>
    DocumentOutputSelection? ShowDocumentOutputDialog(
        string documentType,
        DateOnly defaultStartDate,
        DateOnly defaultEndDate);

    /// <summary>저장 파일 선택. 취소 시 null.</summary>
    string? ShowSaveFileDialog(string suggestedFilename, string filter);

    /// <summary>열기 파일 선택. 취소 시 null.</summary>
    string? ShowOpenFileDialog(string filter, string title);

    /// <summary>메뉴 선택기. 선택한 항목 목록을 반환하며, 취소 시 null.</summary>
    List<MenuPickerSelection>? ShowMenuPicker(int serviceId);

    /// <summary>레시피 선택기. 선택한 레시피 ID를 반환하며, 취소 시 null.</summary>
    int? ShowRecipePicker(int menuId);

    /// <summary>묶음 발주 입력 대화상자. 취소 시 null.</summary>
    GroupOrderSelection? ShowGroupOrderDialog(
        string ingredientName,
        double? totalRequired,
        string? requiredUnit,
        double? suggestedQuantity,
        string? suggestedUnit,
        DateOnly defaultOrderDate,
        DateOnly defaultDeliveryDate);

    /// <summary>일괄 변경 입력 대화상자. 취소 시 null.</summary>
    BulkUpdateSelection? ShowBulkUpdateDialog();
}
