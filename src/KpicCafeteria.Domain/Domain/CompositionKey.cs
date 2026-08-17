namespace KpicCafeteria.Domain.Domain;

/// <summary>
/// 다중 레시피 구분 키.
/// 기존 Python 구현을 그대로 따른다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py
///   def composition_key(resolved):
///       # 수량과 단위는 레시피 구분 기준이 아니다. 재료 구성만 비교한다.
///       return ",".join(str(value) for value in sorted(ingredient.id for ingredient, _ in resolved)) or "EMPTY"
/// </summary>
public static class CompositionKey
{
    /// <summary>재료가 없는 레시피의 키.</summary>
    public const string Empty = "EMPTY";

    /// <summary>
    /// 재료 ID 목록을 정렬해 ","로 결합한 키를 만든다.
    /// 수량과 단위는 포함하지 않는다.
    /// 재료가 없으면 "EMPTY"를 반환한다.
    ///
    /// 기존 코드와 동일하게 중복 제거는 수행하지 않는다.
    /// (중복 재료는 레시피 항목 해석 단계에서 400 오류로 차단된다.)
    /// </summary>
    public static string Create(IEnumerable<int> ingredientIds)
    {
        var ids = ingredientIds.OrderBy(id => id).ToList();
        return ids.Count == 0 ? Empty : string.Join(",", ids);
    }
}
