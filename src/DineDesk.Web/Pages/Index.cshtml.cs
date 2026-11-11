using DineDesk.Web.ApiClient;
using DineDesk.Web.ApiClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly DineDeskApiClient _apiClient;

    public IndexModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<DiningTableResponse> Tables { get; private set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _apiClient.GetTablesAsync();
        if (result.Succeeded)
        {
            Tables = result.Value!;
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    public async Task<IActionResult> OnPostOpenTabAsync(int tableId, int guestCount)
    {
        var result = await _apiClient.OpenTabAsync(tableId, guestCount);
        if (!result.Succeeded)
        {
            await OnGetAsync();
            ErrorMessage = result.Error;
            return Page();
        }

        return RedirectToPage("/Tabs/Details", new { id = result.Value!.TabId });
    }

    public async Task<IActionResult> OnPostGoToTabAsync(int tableId)
    {
        var result = await _apiClient.GetOpenTabIdAsync(tableId);
        if (!result.Succeeded)
        {
            await OnGetAsync();
            ErrorMessage = result.Error;
            return Page();
        }

        return RedirectToPage("/Tabs/Details", new { id = result.Value!.TabId });
    }
}
