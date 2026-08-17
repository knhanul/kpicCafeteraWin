namespace KpicCafeteria.Application.DataManagement;

/// <summary>XLSX 이관 Preview 결과.</summary>
public sealed class ImportPreview
{
    public string Filename { get; set; } = string.Empty;

    public bool Ready { get; set; }

    public Dictionary<string, int> SheetCounts { get; set; } = new();

    public int MealTypeCount { get; set; }

    public int MenuCount { get; set; }

    public int IngredientCount { get; set; }

    public int AliasCount { get; set; }

    public int RecipeRowCount { get; set; }

    public int MealHistoryRowCount { get; set; }

    public int MealIngredientRowCount { get; set; }

    public List<ImportIssue> Errors { get; set; } = [];

    public List<ImportIssue> Warnings { get; set; } = [];
}
