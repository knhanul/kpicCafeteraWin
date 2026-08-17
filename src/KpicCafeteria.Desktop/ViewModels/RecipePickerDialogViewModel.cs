using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>레시피 선택기 항목.</summary>
public partial class RecipePickerItemViewModel : ObservableObject
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Version { get; init; }

    public bool IsDefault { get; init; }

    public string DisplayName => $"{Name} (v{Version}){(IsDefault ? " · 기본" : "")}";
}

/// <summary>레시피 선택기 대화상자 ViewModel.</summary>
public partial class RecipePickerDialogViewModel : ObservableObject
{
    public RecipePickerDialogViewModel(IReadOnlyList<RecipeListItemDto> recipes)
    {
        foreach (var recipe in recipes)
        {
            Items.Add(new RecipePickerItemViewModel
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Version = recipe.Version,
                IsDefault = recipe.IsDefault,
            });
        }

        ConfirmCommand = new RelayCommand(Confirm);
    }

    public ObservableCollection<RecipePickerItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private RecipePickerItemViewModel? selectedItem;

    public RelayCommand ConfirmCommand { get; }

    /// <summary>확인 시 선택된 레시피 ID.</summary>
    public int? Result { get; private set; }

    public void Confirm()
    {
        Result = SelectedItem?.Id;
    }
}
