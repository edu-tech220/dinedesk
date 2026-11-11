using System.Net;
using System.Net.Http.Json;
using DineDesk.Api.Contracts;
using DineDesk.Api.Data;
using DineDesk.Api.Domain.Entities;
using DineDesk.Api.Domain.Enums;
using DineDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DineDesk.Api.Tests;

public class OrderingTests : ApiTestBase
{
    [Fact]
    public async Task PlaceOrder_DecrementsIngredientStockByRecipeQuantity()
    {
        var server = await AsServerAsync();
        var manager = await AsManagerAsync();

        var ingredientsBefore = await GetIngredientsAsync(manager);
        var potatoBefore = ingredientsBefore.First(i => i.Name == "Fries Potato").QuantityInStock;
        var cheddarBefore = ingredientsBefore.First(i => i.Name == "Cheddar Cheese").QuantityInStock;

        var tabId = await OpenTabAsync(server);
        var cheeseFries = (await GetMenuAsync(server)).Items.First(i => i.Name == "Cheese Fries");

        var response = await server.PostAsJsonAsync($"api/tabs/{tabId}/orders",
            new PlaceOrderRequest(new[] { new OrderLineRequest(cheeseFries.Id, 1, null) }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var ingredientsAfter = await GetIngredientsAsync(manager);
        Assert.Equal(potatoBefore - 0.2m, ingredientsAfter.First(i => i.Name == "Fries Potato").QuantityInStock);
        Assert.Equal(cheddarBefore - 0.05m, ingredientsAfter.First(i => i.Name == "Cheddar Cheese").QuantityInStock);
    }

    [Fact]
    public async Task PlaceOrder_WhenRequestExceedsStock_RejectsWithoutDecrementingAnything()
    {
        var server = await AsServerAsync();
        var manager = await AsManagerAsync();
        var tabId = await OpenTabAsync(server);
        var burger = (await GetMenuAsync(server)).Items.First(i => i.Name == "Classic Burger");
        var bunBefore = (await GetIngredientsAsync(manager)).First(i => i.Name == "Burger Bun").QuantityInStock;

        var response = await server.PostAsJsonAsync($"api/tabs/{tabId}/orders",
            new PlaceOrderRequest(new[] { new OrderLineRequest(burger.Id, 1000, null) }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var bunAfter = (await GetIngredientsAsync(manager)).First(i => i.Name == "Burger Bun").QuantityInStock;
        Assert.Equal(bunBefore, bunAfter);
    }

    [Fact]
    public async Task PlaceOrder_WhenStockHitsZero_AutoMarksItemUnavailable()
    {
        var server = await AsServerAsync();
        var (menuItemId, _) = await SeedScarceMenuItemAsync(initialStock: 1);
        var tabId = await OpenTabAsync(server);

        var firstOrder = await server.PostAsJsonAsync($"api/tabs/{tabId}/orders",
            new PlaceOrderRequest(new[] { new OrderLineRequest(menuItemId, 1, null) }));
        Assert.Equal(HttpStatusCode.Created, firstOrder.StatusCode);

        var refreshed = (await GetMenuAsync(server)).Items.First(i => i.Id == menuItemId);
        Assert.False(refreshed.IsAvailable);
    }

    [Fact]
    public async Task PlaceOrder_OnAn86dItem_ReturnsConflictWithoutTouchingStock()
    {
        var server = await AsServerAsync();
        var (menuItemId, ingredientId) = await SeedScarceMenuItemAsync(initialStock: 1);
        var tabId = await OpenTabAsync(server);

        // Deplete it to zero, which auto-86's the item.
        await server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(menuItemId, 1, null) }));

        var response = await server.PostAsJsonAsync($"api/tabs/{tabId}/orders",
            new PlaceOrderRequest(new[] { new OrderLineRequest(menuItemId, 1, null) }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DineDeskDbContext>();
        var stock = db.Ingredients.First(i => i.Id == ingredientId).QuantityInStock;
        Assert.Equal(0m, stock);
    }

    [Fact]
    public async Task PlaceOrder_UnderConcurrency_NeverOversellsStock()
    {
        const int initialStock = 4;
        const int concurrentOrders = 10;

        var (menuItemId, ingredientId) = await SeedScarceMenuItemAsync(initialStock);

        var server = await AsServerAsync();
        var tabId = await OpenTabAsync(server);

        var responses = await Task.WhenAll(Enumerable.Range(0, concurrentOrders).Select(_ =>
            server.PostAsJsonAsync($"api/tabs/{tabId}/orders", new PlaceOrderRequest(new[] { new OrderLineRequest(menuItemId, 1, null) }))));

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicted = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var statusSummary = string.Join(",", responses.Select(r => (int)r.StatusCode));

        Assert.True(succeeded == initialStock, $"succeeded={succeeded} conflicted={conflicted} statuses={statusSummary}");
        Assert.True(conflicted == concurrentOrders - initialStock, $"succeeded={succeeded} conflicted={conflicted} statuses={statusSummary}");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DineDeskDbContext>();
        var finalStock = db.Ingredients.First(i => i.Id == ingredientId).QuantityInStock;
        Assert.Equal(0m, finalStock);
    }

    private async Task<(int MenuItemId, int IngredientId)> SeedScarceMenuItemAsync(decimal initialStock)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DineDeskDbContext>();

        var ingredient = new Ingredient { Name = "Scarce Test Ingredient", UnitOfMeasure = "pc", QuantityInStock = initialStock, ReorderThreshold = 0 };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        var menuItem = new MenuItem { Name = "Scarce Test Item", Category = "Test", Price = 1m };
        menuItem.Recipe.Add(new MenuItemIngredient { IngredientId = ingredient.Id, QuantityRequired = 1 });
        db.MenuItems.Add(menuItem);
        await db.SaveChangesAsync();

        return (menuItem.Id, ingredient.Id);
    }

    private async Task<int> OpenTabAsync(HttpClient client)
    {
        var tablesResponse = await client.GetAsync("api/tables");
        tablesResponse.EnsureSuccessStatusCode();
        var tables = (await tablesResponse.Content.ReadFromJsonAsync<List<DiningTableResponse>>(JsonOptions))!;
        var freeTable = tables.First(t => t.Status == TableStatus.Free);

        var openResponse = await client.PostAsJsonAsync($"api/tables/{freeTable.Id}/open-tab", new OpenTabRequest(2));
        openResponse.EnsureSuccessStatusCode();
        var opened = (await openResponse.Content.ReadFromJsonAsync<OpenTabResponse>(JsonOptions))!;
        return opened.TabId;
    }

    private async Task<PagedResponse<MenuItemResponse>> GetMenuAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/menu?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResponse<MenuItemResponse>>(JsonOptions))!;
    }

    private async Task<List<IngredientResponse>> GetIngredientsAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/ingredients");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<IngredientResponse>>(JsonOptions))!;
    }
}
