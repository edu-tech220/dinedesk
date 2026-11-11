using DineDesk.Web.ApiClient;
using DineDesk.Web.ApiClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages.Tabs;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly DineDeskApiClient _apiClient;

    public DetailsModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public TabResponse? Tab { get; private set; }

    public BillResponse? Bill { get; private set; }

    public List<MenuItemResponse> AvailableMenu { get; private set; } = new();

    public List<decimal>? SplitShares { get; private set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadTabAsync(id))
        {
            return Page();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddItemAsync(int tabId, int menuItemId, int quantity, string? guestLabel)
    {
        var result = await _apiClient.PlaceOrderAsync(tabId, new[] { new OrderLineRequest(menuItemId, quantity, guestLabel) });
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage(new { id = tabId });
    }

    public async Task<IActionResult> OnPostAddPaymentAsync(int tabId, string? guestLabel, decimal amount, string method)
    {
        var result = await _apiClient.AddPaymentAsync(tabId, guestLabel, amount, method);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage(new { id = tabId });
    }

    public async Task<IActionResult> OnPostPreviewSplitAsync(int tabId, int splitCount)
    {
        if (!await LoadTabAsync(tabId))
        {
            return Page();
        }

        var result = await _apiClient.PreviewSplitAsync(tabId, splitCount);
        if (result.Succeeded)
        {
            SplitShares = result.Value;
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCloseTabAsync(int tabId)
    {
        var result = await _apiClient.CloseTabAsync(tabId);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage(new { id = tabId });
    }

    private async Task<bool> LoadTabAsync(int id)
    {
        var tabResult = await _apiClient.GetTabAsync(id);
        if (!tabResult.Succeeded)
        {
            ErrorMessage = tabResult.Error;
            return false;
        }

        Tab = tabResult.Value;

        var billResult = await _apiClient.GetBillAsync(id);
        Bill = billResult.Succeeded ? billResult.Value : null;

        var menuResult = await _apiClient.GetMenuAsync(1, 100);
        AvailableMenu = menuResult.Succeeded
            ? menuResult.Value!.Items.Where(i => i.IsAvailable).ToList()
            : new List<MenuItemResponse>();

        return true;
    }
}
