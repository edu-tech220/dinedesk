using System.Net;
using System.Net.Http.Json;
using DineDesk.Api.Contracts;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Tests.Infrastructure;

namespace DineDesk.Api.Tests;

public class TabsTests : ApiTestBase
{
    [Fact]
    public async Task OpenTab_OnAlreadyOccupiedTable_ReturnsConflict()
    {
        var server = await AsServerAsync();
        var freeTable = await GetFreeTableAsync(server);

        var first = await server.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await server.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetBill_ComputesTaxAndTotalFromOrderedItems()
    {
        var server = await AsServerAsync();
        var tabId = await OpenTabAsync(server);
        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");

        await server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1, null) }));

        var billResponse = await server.GetAsync($"api/tabs/{tabId}/bill");
        var bill = (await billResponse.Content.ReadFromJsonAsync<BillResponse>(JsonOptions))!;

        Assert.Equal(burger.Price, bill.Subtotal);
        Assert.Equal(Math.Round(burger.Price * bill.TaxRate, 2, MidpointRounding.AwayFromZero), bill.Tax);
        Assert.Equal(bill.Subtotal + bill.Tax, bill.Total);
        Assert.Equal(bill.Total, bill.BalanceDue);
    }

    [Fact]
    public async Task AddPayment_ExceedingBalance_ReturnsBadRequest()
    {
        var server = await AsServerAsync();
        var tabId = await OpenTabAsync(server);
        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");
        await server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1, null) }));

        var response = await server.PostAsJsonAsync($"api/tabs/{tabId}/payments", new AddPaymentRequest(null, 1000m, PaymentMethod.Cash));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CloseTab_WhenNotFullyPaid_ReturnsConflict()
    {
        var server = await AsServerAsync();
        var tabId = await OpenTabAsync(server);
        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");
        await server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1, null) }));

        var response = await server.PostAsync($"api/tabs/{tabId}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CloseTab_WhenFullyPaid_SucceedsAndFreesTheTable()
    {
        var server = await AsServerAsync();
        var freeTable = await GetFreeTableAsync(server);
        var openResponse = await server.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        var tab = (await openResponse.Content.ReadFromJsonAsync<OpenTabResponse>(JsonOptions))!;

        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");
        await server.PostAsJsonAsync($"api/tabs/{tab.TabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1, null) }));

        var bill = (await (await server.GetAsync($"api/tabs/{tab.TabId}/bill")).Content.ReadFromJsonAsync<BillResponse>(JsonOptions))!;
        await server.PostAsJsonAsync($"api/tabs/{tab.TabId}/payments", new AddPaymentRequest(null, bill.Total, PaymentMethod.Card));

        var closeResponse = await server.PostAsync($"api/tabs/{tab.TabId}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);

        var tables = (await (await server.GetAsync("api/tables")).Content.ReadFromJsonAsync<List<DiningTableResponse>>(JsonOptions))!;
        var reloadedTable = tables.First(t => t.Id == freeTable.Id);
        Assert.Equal(TableStatus.Free, reloadedTable.Status);
    }

    [Fact]
    public async Task SplitPreview_DistributesRoundingRemainderInCents()
    {
        var server = await AsServerAsync();
        var tabId = await OpenTabAsync(server);
        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");
        await server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1, null) }));

        var bill = (await (await server.GetAsync($"api/tabs/{tabId}/bill")).Content.ReadFromJsonAsync<BillResponse>(JsonOptions))!;

        var response = await server.PostAsJsonAsync($"api/tabs/{tabId}/split-preview", new SplitPreviewRequest(3));
        var shares = (await response.Content.ReadFromJsonAsync<List<decimal>>(JsonOptions))!;

        Assert.Equal(3, shares.Count);
        Assert.Equal(bill.Total, shares.Sum());
        Assert.True(shares.Max() - shares.Min() <= 0.01m);
    }

    private async Task<DiningTableResponse> GetFreeTableAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/tables");
        var tables = (await response.Content.ReadFromJsonAsync<List<DiningTableResponse>>(JsonOptions))!;
        return tables.First(t => t.Status == TableStatus.Free);
    }

    private async Task<int> OpenTabAsync(HttpClient client)
    {
        var freeTable = await GetFreeTableAsync(client);
        var response = await client.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        var opened = (await response.Content.ReadFromJsonAsync<OpenTabResponse>(JsonOptions))!;
        return opened.TabId;
    }

    private async Task<PagedResponse<MenuItemResponse>> GetMenuAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/menu?page=1&pageSize=50");
        return (await response.Content.ReadFromJsonAsync<PagedResponse<MenuItemResponse>>(JsonOptions))!;
    }
}
