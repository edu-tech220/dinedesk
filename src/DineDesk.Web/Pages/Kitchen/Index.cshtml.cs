using DineDesk.Web.ApiClient;
using DineDesk.Web.ApiClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages.Kitchen;

[Authorize(Roles = "Kitchen,Manager,Admin")]
public class IndexModel : PageModel
{
    private static readonly Dictionary<string, string> NextStatusByCurrent = new()
    {
        ["Received"] = "Preparing",
        ["Preparing"] = "Ready",
        ["Ready"] = "Served",
    };

    private readonly DineDeskApiClient _apiClient;

    public IndexModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<KitchenQueueItemResponse> Queue { get; private set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? GetNextStatus(string currentStatus) =>
        NextStatusByCurrent.TryGetValue(currentStatus, out var next) ? next : null;

    public async Task OnGetAsync()
    {
        await LoadQueueAsync();
    }

    public async Task<IActionResult> OnPostAdvanceAsync(int itemId, string newStatus, string eTag)
    {
        var result = await _apiClient.UpdateItemStatusAsync(itemId, newStatus, eTag);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVoidAsync(int itemId, string reason)
    {
        var result = await _apiClient.VoidItemAsync(itemId, reason);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    private async Task LoadQueueAsync()
    {
        var result = await _apiClient.GetKitchenQueueAsync(1, 50);
        if (result.Succeeded)
        {
            Queue = result.Value!.Items.ToList();
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }
}
