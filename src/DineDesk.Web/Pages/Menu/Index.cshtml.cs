using System.Globalization;
using DineDesk.Web.ApiClient;
using DineDesk.Web.ApiClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages.Menu;

[Authorize(Roles = "Manager,Admin")]
public class IndexModel : PageModel
{
    private const string RecipeFieldPrefix = "RecipeQuantity_";

    private readonly DineDeskApiClient _apiClient;

    public IndexModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<MenuItemResponse> MenuItems { get; private set; } = new();

    public List<IngredientResponse> Ingredients { get; private set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostToggleAvailabilityAsync(int menuItemId, string eTag, bool isAvailable)
    {
        var result = await _apiClient.SetMenuAvailabilityAsync(menuItemId, isAvailable, eTag);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, string category, decimal price, bool isHappyHourEligible)
    {
        var recipe = new List<RecipeLineRequest>();
        foreach (var key in Request.Form.Keys.Where(k => k.StartsWith(RecipeFieldPrefix, StringComparison.Ordinal)))
        {
            var ingredientId = int.Parse(key[RecipeFieldPrefix.Length..], CultureInfo.InvariantCulture);
            var quantity = decimal.Parse(Request.Form[key].ToString(), CultureInfo.InvariantCulture);
            if (quantity > 0)
            {
                recipe.Add(new RecipeLineRequest(ingredientId, quantity));
            }
        }

        var result = await _apiClient.CreateMenuItemAsync(new CreateMenuItemRequest(name, category, price, isHappyHourEligible, recipe));
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var menuResult = await _apiClient.GetMenuAsync(1, 100);
        if (menuResult.Succeeded)
        {
            MenuItems = menuResult.Value!.Items.ToList();
        }
        else
        {
            ErrorMessage = menuResult.Error;
        }

        var ingredientsResult = await _apiClient.GetIngredientsAsync();
        Ingredients = ingredientsResult.Succeeded ? ingredientsResult.Value! : new List<IngredientResponse>();
    }
}
