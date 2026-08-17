using KpicCafeteria.Domain.Domain;

namespace KpicCafeteria.Tests;

/// <summary>
/// 다중 레시피 구분 키 규칙 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\tests\test_multi_recipe.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py (composition_key)
/// </summary>
public class CompositionKeyTests
{
    [Fact]
    public void Create_SortsIngredientIdsAndJoinsWithComma()
    {
        // [8,1,4] → "1,4,8"
        Assert.Equal("1,4,8", CompositionKey.Create([8, 1, 4]));
    }

    [Fact]
    public void Create_EmptyIngredients_ReturnsEmptyConstant()
    {
        // [] → "EMPTY"
        Assert.Equal("EMPTY", CompositionKey.Create([]));
    }

    [Fact]
    public void Create_AlreadySortedInput_KeepsOrder()
    {
        Assert.Equal("1,2,3", CompositionKey.Create([1, 2, 3]));
    }

    [Fact]
    public void Create_QuantityAndUnitDoNotAffectKey()
    {
        // 수량/단위는 레시피 구분 기준이 아니다. 같은 재료 구성이면 같은 키다.
        var keyA = CompositionKey.Create([3, 1]);
        var keyB = CompositionKey.Create([1, 3]);
        Assert.Equal(keyA, keyB);
        Assert.Equal("1,3", keyA);
    }

    [Fact]
    public void Create_SingleIngredient_ReturnsSingleId()
    {
        Assert.Equal("7", CompositionKey.Create([7]));
    }
}
