using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DineDesk.Api.Contracts;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Tests.Infrastructure;

namespace DineDesk.Api.Tests;

public class KitchenTests : ApiTestBase
{
    [Fact]
    public async Task UpdateStatus_ValidTransition_Succeeds()
    {
        var server = await AsServerAsync();
        var kitchen = await AsKitchenAsync();
        var item = await PlaceSingleItemOrderAsync(server, "Cola");

        var response = await SendStatusPatch(kitchen, item.Id, "Preparing", item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_SkippingAStep_ReturnsBadRequest()
    {
        var server = await AsServerAsync();
        var kitchen = await AsKitchenAsync();
        var item = await PlaceSingleItemOrderAsync(server, "Cola");

        var response = await SendStatusPatch(kitchen, item.Id, "Served", item.ETag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithStaleETag_ReturnsConflict()
    {
        var server = await AsServerAsync();
        var kitchen = await AsKitchenAsync();
        var item = await PlaceSingleItemOrderAsync(server, "Cola");
        var staleETag = item.ETag;

        var firstAdvance = await SendStatusPatch(kitchen, item.Id, "Preparing", staleETag);
        Assert.Equal(HttpStatusCode.NoContent, firstAdvance.StatusCode);

        // Item is now "Preparing" in the database, but we still present the ETag from when it
        // was "Received" - a valid next transition (Preparing -> Ready) with stale concurrency data.
        var secondAdvance = await SendStatusPatch(kitchen, item.Id, "Ready", staleETag);
        Assert.Equal(HttpStatusCode.Conflict, secondAdvance.StatusCode);
    }

    [Fact]
    public async Task VoidItem_RestoresIngredientStock()
    {
        var server = await AsServerAsync();
        var manager = await AsManagerAsync();
        var colaSyrupBefore = (await GetIngredientsAsync(manager)).First(i => i.Name == "Cola Syrup").QuantityInStock;

        var item = await PlaceSingleItemOrderAsync(server, "Cola");

        var voidResponse = await manager.PostAsJsonAsync($"api/kitchen/items/{item.Id}/void", new VoidItemRequest("Customer changed their mind"));
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var colaSyrupAfter = (await GetIngredientsAsync(manager)).First(i => i.Name == "Cola Syrup").QuantityInStock;
        Assert.Equal(colaSyrupBefore, colaSyrupAfter);
    }

    [Fact]
    public async Task VoidItem_AsServerRole_IsForbidden()
    {
        var server = await AsServerAsync();
        var item = await PlaceSingleItemOrderAsync(server, "Cola");

        var response = await server.PostAsJsonAsync($"api/kitchen/items/{item.Id}/void", new VoidItemRequest("Not allowed"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendStatusPatch(HttpClient client, int itemId, string newStatus, string eTag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"api/kitchen/items/{itemId}/status")
        {
            Content = JsonContent.Create(new UpdateItemStatusRequest(Enum.Parse<OrderItemStatus>(newStatus)), options: JsonOptions),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{eTag}\""));
        return client.SendAsync(request);
    }

    private async Task<KitchenQueueItemResponse> PlaceSingleItemOrderAsync(HttpClient server, string menuItemName)
    {
        var tablesResponse = await server.GetAsync("api/tables");
        var tables = (await tablesResponse.Content.ReadFromJsonAsync<List<DiningTableResponse>>(JsonOptions))!;
        var freeTable = tables.First(t => t.Status == TableStatus.Free);

        var openResponse = await server.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        var tab = (await openResponse.Content.ReadFromJsonAsync<OpenTabResponse>(JsonOptions))!;

        var menuResponse = await server.GetAsync("api/menu?page=1&pageSize=50");
        var menu = (await menuResponse.Content.ReadFromJsonAsync<PagedResponse<MenuItemResponse>>(JsonOptions))!;
        var menuItem = menu.Items.First(i => i.Name == menuItemName);

        var orderResponse = await server.PostAsJsonAsync($"api/tabs/{tab.TabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(menuItem.Id, 1, null) }));
        var orderBody = await orderResponse.Content.ReadAsStringAsync();

        var kitchen = await AsKitchenAsync();
        var queueResponse = await kitchen.GetAsync("api/kitchen/queue?page=1&pageSize=50");
        var queueBody = await queueResponse.Content.ReadAsStringAsync();
        var queue = System.Text.Json.JsonSerializer.Deserialize<PagedResponse<KitchenQueueItemResponse>>(queueBody, JsonOptions)!;
        return queue.Items.FirstOrDefault(i => i.TableNumber == freeTable.Number && i.MenuItemName == menuItemName)
            ?? throw new InvalidOperationException(
                $"orderStatus={(int)orderResponse.StatusCode} orderBody={orderBody} queueStatus={(int)queueResponse.StatusCode} queueBody={queueBody} freeTable={freeTable.Number}");
    }

    private async Task<List<IngredientResponse>> GetIngredientsAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/ingredients");
        return (await response.Content.ReadFromJsonAsync<List<IngredientResponse>>(JsonOptions))!;
    }
}
