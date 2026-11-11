using DineDesk.Api.Common;
using DineDesk.Api.Contracts;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

[Authorize(Roles = "Kitchen,Manager,Admin")]
public class KitchenController : ApiControllerBase
{
    private readonly IOrderService _orderService;

    public KitchenController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] OrderItemStatus? status = null)
    {
        var result = await _orderService.GetKitchenQueueAsync(page, pageSize, status);
        var items = result.Items
            .Select(i => new KitchenQueueItemResponse(i.Id, i.MenuItemName, i.Quantity, i.Status, i.TableNumber, i.PlacedAtUtc, ConcurrencyHeader.ToETag(i.RowVersion)))
            .ToList();
        return Ok(new PagedResponse<KitchenQueueItemResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    [HttpPatch("items/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateItemStatusRequest request, [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        if (!ConcurrencyHeader.TryParse(ifMatch, out var rowVersion))
        {
            return BadRequest(new ProblemDetails { Title = "Validation", Detail = "An If-Match header with the item's current ETag is required." });
        }

        var result = await _orderService.UpdateItemStatusAsync(id, request.NewStatus, rowVersion);
        return FromResult(result, _ => NoContent());
    }

    [HttpPost("items/{id:int}/void")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> VoidItem(int id, [FromBody] VoidItemRequest request)
    {
        var result = await _orderService.VoidItemAsync(id, request.Reason);
        return FromResult(result, _ => NoContent());
    }
}
