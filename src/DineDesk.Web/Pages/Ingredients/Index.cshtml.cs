using DineDesk.Web.ApiClient;
using DineDesk.Web.ApiClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages.Ingredients;

[Authorize(Roles = "Manager,Admin")]
public class IndexModel : PageModel
{
    private readonly DineDeskApiClient _apiClient;

    public IndexModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<IngredientResponse> Ingredients { get; private set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostRestockAsync(int ingredientId, decimal quantityToAdd)
    {
        var result = await _apiClient.RestockAsync(ingredientId, quantityToAdd);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var result = await _apiClient.GetIngredientsAsync();
        if (result.Succeeded)
        {
            Ingredients = result.Value!;
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }
}
