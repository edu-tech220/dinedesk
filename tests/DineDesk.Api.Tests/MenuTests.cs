using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DineDesk.Api.Contracts;
using DineDesk.Api.Tests.Infrastructure;

namespace DineDesk.Api.Tests;

public class MenuTests : ApiTestBase
{
    [Fact]
    public async Task GetMenu_IsPublic_AndReturnsSeededItems()
    {
        var response = await Client.GetAsync("api/menu?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<MenuItemResponse>>(JsonOptions);
        Assert.True(page!.TotalCount > 0);
        Assert.Contains(page.Items, i => i.Name == "Classic Burger");
    }

    [Fact]
    public async Task CreateMenuItem_AsManager_Succeeds()
    {
        var manager = await AsManagerAsync();
        var ingredients = await GetIngredientsAsync(manager);
        var bun = ingredients.First(i => i.Name == "Burger Bun");

        var request = new CreateMenuItemRequest("Test Wrap", "Test", 6.5m, false, new[] { new RecipeLineRequest(bun.Id, 1) });
        var response = await manager.PostAsJsonAsync("api/menu", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateMenuItem_AsServer_IsForbidden()
    {
        var server = await AsServerAsync();

        var request = new CreateMenuItemRequest("Unauthorized Item", "Test", 5m, false, new[] { new RecipeLineRequest(1, 1) });
        var response = await server.PostAsJsonAsync("api/menu", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetAvailability_WithStaleETag_ReturnsConflict()
    {
        var manager = await AsManagerAsync();
        var menu = await GetMenuAsync(manager);
        var item = menu.Items.First(i => i.Name == "Cola");
        var staleETag = item.ETag;

        var firstToggle = SendAvailabilityPatch(manager, item.Id, false, staleETag);
        var firstResponse = await firstToggle;
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        var secondResponse = await SendAvailabilityPatch(manager, item.Id, true, staleETag);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAvailabilityPatch(HttpClient client, int menuItemId, bool isAvailable, string eTag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"api/menu/{menuItemId}/availability")
        {
            Content = JsonContent.Create(new SetAvailabilityRequest(isAvailable), options: JsonOptions),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{eTag}\""));
        return client.SendAsync(request);
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
