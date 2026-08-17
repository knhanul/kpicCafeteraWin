namespace KpicCafeteria.Application.MasterData;

// ---------------------------------------------------------------------------
// Menu
// ---------------------------------------------------------------------------

public sealed record MenuDto(
    int Id,
    string Name,
    string CanonicalName,
    string Role,
    bool Active,
    string ReviewStatus,
    int RecipeCount,
    int? DefaultRecipeId);

public sealed record MenuInput(
    string Name,
    string? CanonicalName,
    string Role,
    bool Active);

public sealed record MenuSearchResult(
    IReadOnlyList<MenuListItemDto> Items,
    int Total,
    int Offset,
    int Limit,
    bool HasMore);

public sealed record MenuDetailDto(
    MenuDto Menu,
    IReadOnlyList<RecipeListItemDto> Recipes);

public sealed record MenuListItemDto(
    int Id,
    string Name,
    string Role,
    bool Active,
    int RecipeCount,
    int? DefaultRecipeId);

// ---------------------------------------------------------------------------
// Ingredient
// ---------------------------------------------------------------------------

public sealed record IngredientDto(
    int Id,
    string Name,
    string StatGroup,
    string? DefaultUnit,
    double? PurchasePackageQuantity,
    string? PurchasePackageUnit,
    double? KgFactor,
    bool AnalysisExcluded,
    bool Active,
    string ReviewStatus);

public sealed record IngredientDetailDto(
    IngredientDto Ingredient,
    IReadOnlyList<AliasDto> Aliases);

public sealed record IngredientInput(
    string Name,
    string StatGroup,
    string? DefaultUnit,
    double? PurchasePackageQuantity,
    string? PurchasePackageUnit,
    double? KgFactor,
    bool AnalysisExcluded,
    bool Active);

public sealed record IngredientSearchResult(
    IReadOnlyList<IngredientDto> Items,
    int Total,
    int Offset,
    int Limit,
    bool HasMore);

// ---------------------------------------------------------------------------
// Alias
// ---------------------------------------------------------------------------

public sealed record AliasDto(int Id, string Alias);

// ---------------------------------------------------------------------------
// Recipe
// ---------------------------------------------------------------------------

public sealed record RecipeItemInput(
    int? IngredientId,
    string? IngredientName,
    double? QuantityPer100,
    string? Unit,
    bool IsPrimary);

public sealed record RecipeInput(
    string? Name,
    string? Note,
    bool IsDefault,
    bool Active,
    IReadOnlyList<RecipeItemInput> Ingredients);

public sealed record RecipeDto(
    int Id,
    int MenuId,
    string Name,
    int Version,
    string CompositionKey,
    string? Note,
    bool IsDefault,
    bool Active,
    IReadOnlyList<RecipeIngredientDto> Ingredients);

public sealed record RecipeIngredientDto(
    int Id,
    int? IngredientId,
    string IngredientName,
    string StatGroup,
    double? QuantityPer100,
    string? Unit,
    bool IsPrimary,
    int SortOrder);

public sealed record RecipeListItemDto(
    int Id,
    string Name,
    int Version,
    bool IsDefault,
    bool Active,
    int IngredientCount,
    string CompositionKey);

// ---------------------------------------------------------------------------
// MealTypeSetting
// ---------------------------------------------------------------------------

public sealed record MealTypeSettingDto(
    int Id,
    string Code,
    string Name,
    int DefaultPlannedCount,
    string DefaultServiceTime,
    int SortOrder,
    bool IsActive,
    string? Description);

public sealed record MealTypeSettingInput(
    string Code,
    int DefaultPlannedCount,
    string DefaultServiceTime,
    int SortOrder,
    bool IsActive,
    string? Description);
