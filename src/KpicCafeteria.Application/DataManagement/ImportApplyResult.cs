namespace KpicCafeteria.Application.DataManagement;

/// <summary>이관 Apply 결과.</summary>
public sealed class ImportApplyResult
{
    public string Filename { get; set; } = string.Empty;

    public ImportMode Mode { get; set; }

    public int MealTypes { get; set; }

    public int Menus { get; set; }

    public int Ingredients { get; set; }

    public int Aliases { get; set; }

    public int Recipes { get; set; }

    public int Services { get; set; }

    public int MealHistoryRows { get; set; }

    public int MealIngredientRows { get; set; }
}
