namespace KpicCafeteria.Application.Orders;

/// <summary>
/// 발주 업무 오류의 기본 타입.
/// UI는 이 예외의 Message를 사용자에게 그대로 표시한다.
/// </summary>
public class OrderException : Exception
{
    public OrderException(string message)
        : base(message)
    {
    }
}

/// <summary>발주 상태 값이 올바르지 않을 때.</summary>
public sealed class InvalidOrderStatusException : OrderException
{
    public InvalidOrderStatusException()
        : base("발주 상태 값이 올바르지 않습니다.")
    {
    }
}

/// <summary>일괄 변경 시 변경할 항목이 없을 때.</summary>
public sealed class NoChangesToApplyException : OrderException
{
    public NoChangesToApplyException()
        : base("변경할 항목이 없습니다.")
    {
    }
}

/// <summary>선택한 발주 항목이 없을 때.</summary>
public sealed class EmptyOrderSelectionException : OrderException
{
    public EmptyOrderSelectionException()
        : base("선택한 발주 항목이 없습니다.")
    {
    }
}

/// <summary>서로 다른 식재료를 하나의 묶음 발주로 묶을 때.</summary>
public sealed class MixedIngredientGroupException : OrderException
{
    public MixedIngredientGroupException()
        : base("서로 다른 식재료를 하나의 묶음 발주로 묶을 수 없습니다.")
    {
    }
}
