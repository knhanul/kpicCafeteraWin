namespace KpicCafeteria.Application.MasterData;

/// <summary>
/// 기준정보 업무 오류의 기본 타입.
/// UI는 이 예외의 Message를 사용자에게 그대로 표시한다 (기술적 Stack Trace 노출 금지).
/// </summary>
public class MasterDataException : Exception
{
    public MasterDataException(string message)
        : base(message)
    {
    }
}

/// <summary>같은 이름의 메뉴가 이미 존재할 때.</summary>
public sealed class DuplicateMenuNameException : MasterDataException
{
    public DuplicateMenuNameException()
        : base("같은 이름의 메뉴가 있습니다.")
    {
    }
}

/// <summary>같은 이름의 재료가 이미 존재할 때.</summary>
public sealed class DuplicateIngredientNameException : MasterDataException
{
    public DuplicateIngredientNameException()
        : base("같은 이름의 재료가 있습니다.")
    {
    }
}

/// <summary>같은 메뉴에 같은 재료 구성의 레시피가 이미 존재할 때.</summary>
public sealed class DuplicateRecipeCompositionException : MasterDataException
{
    public DuplicateRecipeCompositionException(string duplicateRecipeName)
        : base($"같은 재료 구성의 레시피가 이미 있습니다: {duplicateRecipeName}")
    {
    }
}

/// <summary>한 레시피 안에 같은 재료가 중복 입력되었을 때.</summary>
public sealed class DuplicateRecipeIngredientException : MasterDataException
{
    public DuplicateRecipeIngredientException(string ingredientName)
        : base($"레시피에 같은 재료가 중복되었습니다: {ingredientName}")
    {
    }
}

/// <summary>대상 엔티티를 찾을 수 없을 때.</summary>
public sealed class MasterDataNotFoundException : MasterDataException
{
    public MasterDataNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>레시피 재료를 해석할 수 없을 때 (재료 ID/이름 모두 불일치).</summary>
public sealed class RecipeIngredientNotFoundException : MasterDataException
{
    public RecipeIngredientNotFoundException()
        : base("재료를 찾을 수 없습니다.")
    {
    }
}

/// <summary>배식유형 코드를 찾을 수 없을 때.</summary>
public sealed class MealTypeNotFoundException : MasterDataException
{
    public MealTypeNotFoundException(string code)
        : base($"배식유형을 찾을 수 없습니다: {code}")
    {
    }
}

/// <summary>배식시간 형식이 HH:MM이 아닐 때.</summary>
public sealed class InvalidTimeFormatException : MasterDataException
{
    public InvalidTimeFormatException()
        : base("배식시간 형식은 HH:MM이어야 합니다.")
    {
    }
}

/// <summary>계획식수가 음수일 때.</summary>
public sealed class InvalidPlannedCountException : MasterDataException
{
    public InvalidPlannedCountException()
        : base("기본 계획식수는 0 이상이어야 합니다.")
    {
    }
}
