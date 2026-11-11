using DineDesk.Api.Common;
using DineDesk.Api.Contracts;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

[Authorize]
public class TabsController : ApiControllerBase
{
    private readonly ITabService _tabService;
    private readonly IOrderService _orderService;

    public TabsController(ITabService tabService, IOrderService orderService)
    {
        _tabService = tabService;
        _orderService = orderService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTab(int id)
    {
        var result = await _tabService.GetTabAsync(id);
        return FromResult(result, tab => Ok(ToResponse(tab)));
    }

    [HttpGet("{id:int}/bill")]
    public async Task<IActionResult> GetBill(int id)
    {
        var result = await _tabService.GetBillAsync(id);
        return FromResult(result, b => Ok(new BillResponse(b.Subtotal, b.TaxRate, b.Tax, b.Tip, b.Total, b.AmountPaid, b.BalanceDue)));
    }

    [HttpPost("{id:int}/split-preview")]
    public async Task<IActionResult> PreviewSplit(int id, [FromBody] SplitPreviewRequest request)
    {
        var result = await _tabService.PreviewEvenSplitAsync(id, request.SplitCount);
        return FromResult(result, shares => Ok(shares));
    }

    [HttpPost("{id:int}/orders")]
    [Authorize(Roles = "Server,Manager,Admin")]
    public async Task<IActionResult> PlaceOrder(int id, [FromBody] PlaceOrderRequest request)
    {
        var lines = request.Items.Select(i => new PlaceOrderLine(i.MenuItemId, i.Quantity, i.GuestLabel)).ToList();
        var result = await _orderService.PlaceOrderAsync(id, lines);
        return FromResult(result, order => CreatedAtAction(nameof(GetTab), new { id }, order));
    }

    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = "Server,Manager,Admin")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] AddPaymentRequest request)
    {
        var result = await _tabService.AddPaymentAsync(id, request.GuestLabel, request.Amount, request.Method);
        return FromResult(result, _ => NoContent());
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Roles = "Server,Manager,Admin")]
    public async Task<IActionResult> CloseTab(int id)
    {
        var result = await _tabService.CloseTabAsync(id);
        return FromResult(result, _ => NoContent());
    }

    private static TabResponse ToResponse(TabDetail tab)
    {
        var orders = tab.Orders
            .Select(o => new TabOrderResponse(
                o.OrderId,
                o.PlacedAtUtc,
                o.Items.Select(i => new TabItemResponse(i.Id, i.MenuItemName, i.Quantity, i.UnitPrice, i.Status, i.GuestLabel, ConcurrencyHeader.ToETag(i.RowVersion))).ToList()))
            .ToList();

        return new TabResponse(tab.Id, tab.DiningTableId, tab.TableNumber, tab.GuestCount, tab.Status, tab.OpenedAtUtc, orders);
    }
}
