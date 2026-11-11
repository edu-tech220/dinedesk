using DineDesk.Api.Common;
using DineDesk.Api.Contracts;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

[Authorize]
public class TablesController : ApiControllerBase
{
    private readonly ITabService _tabService;

    public TablesController(ITabService tabService)
    {
        _tabService = tabService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _tabService.GetTablesAsync();
        return Ok(tables.Select(t => new DiningTableResponse(t.Id, t.Number, t.Capacity, t.Status)));
    }

    [HttpPost("{id:int}/open-tab")]
    [Authorize(Roles = "Server,Manager,Admin")]
    public async Task<IActionResult> OpenTab(int id, [FromBody] OpenTabRequest request)
    {
        var result = await _tabService.OpenTabAsync(id, request.GuestCount, User.GetUserId());
        return FromResult(result, tab => CreatedAtAction(
            nameof(TabsController.GetTab), "Tabs", new { id = tab.Id }, new OpenTabResponse(tab.Id, tab.DiningTableId, tab.OpenedAtUtc)));
    }
}
