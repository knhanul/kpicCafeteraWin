namespace KpicCafeteria.Domain.Domain;

/// <summary>
/// 100인 기준 수량(quantity_per_100)과 계획식수 기준 총량(quantity_total) 사이의 환산 규칙.
/// 기존 Python 구현을 그대로 따른다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py
///   _copy_recipe_to_service_menu():
///       total = recipe_item.quantity_per_100 * item.service.planned_count / 100
///               if recipe_item.quantity_per_100 is not None else None
///   update_service_menu_ingredients():
///       if per_100 is None and total is not None and item.service.planned_count:
///           per_100 = total * 100 / item.service.planned_count
///       if total is None and per_100 is not None:
///           total = per_100 * item.service.planned_count / 100
/// </summary>
public static class QuantityCalculator
{
    /// <summary>
    /// 100인 기준 수량 → 계획식수 기준 총량.
    /// quantity_total = quantity_per_100 × planned_count / 100
    /// per_100이 null이면 null을 반환한다.
    /// </summary>
    public static double? CalculateTotal(double? quantityPer100, int plannedCount)
        => quantityPer100 is null ? null : quantityPer100.Value * plannedCount / 100.0;

    /// <summary>
    /// 계획식수 기준 총량 → 100인 기준 수량.
    /// quantity_per_100 = quantity_total × 100 / planned_count
    /// total이 null이거나 planned_count가 0이면 null을 반환한다.
    /// (기존 코드: planned_count가 0이면 역산하지 않고 per_100을 null로 유지)
    /// </summary>
    public static double? CalculatePer100(double? quantityTotal, int plannedCount)
        => quantityTotal is null || plannedCount == 0 ? null : quantityTotal.Value * 100.0 / plannedCount;
}
