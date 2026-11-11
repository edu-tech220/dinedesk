using DineDesk.Api.Common;
using DineDesk.Api.Contracts;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

public class MenuController : ApiControllerBase
{
    private readonly IMenuService _menuService;

    public MenuController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetMenu([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? category = null)
    {
        var result = await _menuService.GetMenuAsync(page, pageSize, category);
        var items = result.Items
            .Select(i => new MenuItemResponse(i.Id, i.Name, i.Category, i.Price, i.IsAvailable, i.IsHappyHourEligible, ConcurrencyHeader.ToETag(i.RowVersion)))
            .ToList();
        return Ok(new PagedResponse<MenuItemResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> CreateMenuItem([FromBody] CreateMenuItemRequest request)
    {
        var recipe = request.Recipe.Select(r => new MenuItemRecipeLine(r.IngredientId, r.QuantityRequired)).ToList();
        var result = await _menuService.CreateMenuItemAsync(request.Name, request.Category, request.Price, request.IsHappyHourEligible, recipe);
        return FromResult(result, m => CreatedAtAction(nameof(GetMenu), new { }, new MenuItemResponse(m.Id, m.Name, m.Category, m.Price, m.IsAvailable, m.IsHappyHourEligible, ConcurrencyHeader.ToETag(m.RowVersion))));
    }

    [HttpPatch("{id:int}/availability")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> SetAvailability(int id, [FromBody] SetAvailabilityRequest request, [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        if (!ConcurrencyHeader.TryParse(ifMatch, out var rowVersion))
        {
            return BadRequest(new ProblemDetails { Title = "Validation", Detail = "An If-Match header with the item's current ETag is required." });
        }

        var result = await _menuService.SetAvailabilityAsync(id, request.IsAvailable, rowVersion);
        return FromResult(result, _ => NoContent());
    }
}
