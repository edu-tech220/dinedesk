using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DineDesk.Web.ApiClient.Models;

namespace DineDesk.Web.ApiClient;

public class DineDeskApiClient
{
    // The API serializes with ASP.NET Core's camelCase defaults; the plain default
    // JsonSerializerOptions used by System.Net.Http.Json is case-sensitive, so without
    // this every response would deserialize into all-default property values.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public DineDeskApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiResult<LoginResponse>> LoginAsync(string username, string password) =>
        PostAsync<LoginRequest, LoginResponse>("api/auth/login", new LoginRequest(username, password));

    public Task<ApiResult<List<DiningTableResponse>>> GetTablesAsync() =>
        GetAsync<List<DiningTableResponse>>("api/tables");

    public Task<ApiResult<OpenTabResponse>> OpenTabAsync(int tableId, int guestCount) =>
        PostAsync<OpenTabRequest, OpenTabResponse>($"api/tables/{tableId}/open-tab", new OpenTabRequest(guestCount));

    public Task<ApiResult<OpenTabIdResponse>> GetOpenTabIdAsync(int tableId) =>
        GetAsync<OpenTabIdResponse>($"api/tables/{tableId}/open-tab");

    public Task<ApiResult<TabResponse>> GetTabAsync(int tabId) =>
        GetAsync<TabResponse>($"api/tabs/{tabId}");

    public Task<ApiResult<BillResponse>> GetBillAsync(int tabId) =>
        GetAsync<BillResponse>($"api/tabs/{tabId}/bill");

    public Task<ApiResult<List<decimal>>> PreviewSplitAsync(int tabId, int splitCount) =>
        PostAsync<SplitPreviewRequest, List<decimal>>($"api/tabs/{tabId}/split-preview", new SplitPreviewRequest(splitCount));

    public Task<ApiResult<object>> PlaceOrderAsync(int tabId, IReadOnlyList<OrderLineRequest> lines) =>
        PostAsync<PlaceOrderRequest, object>($"api/tabs/{tabId}/orders", new PlaceOrderRequest(lines));

    public Task<ApiResult<bool>> AddPaymentAsync(int tabId, string? guestLabel, decimal amount, string method) =>
        PostNoContentAsync($"api/tabs/{tabId}/payments", new AddPaymentRequest(guestLabel, amount, method));

    public Task<ApiResult<bool>> CloseTabAsync(int tabId) =>
        PostNoContentAsync<object?>($"api/tabs/{tabId}/close", null);

    public Task<ApiResult<PagedResponse<MenuItemResponse>>> GetMenuAsync(int page, int pageSize, string? category = null)
    {
        var query = category is null
            ? $"api/menu?page={page}&pageSize={pageSize}"
            : $"api/menu?page={page}&pageSize={pageSize}&category={Uri.EscapeDataString(category)}";
        return GetAsync<PagedResponse<MenuItemResponse>>(query);
    }

    public Task<ApiResult<object>> CreateMenuItemAsync(CreateMenuItemRequest request) =>
        PostAsync<CreateMenuItemRequest, object>("api/menu", request);

    public Task<ApiResult<bool>> SetMenuAvailabilityAsync(int menuItemId, bool isAvailable, string eTag) =>
        PatchNoContentAsync($"api/menu/{menuItemId}/availability", new SetAvailabilityRequest(isAvailable), eTag);

    public Task<ApiResult<List<IngredientResponse>>> GetIngredientsAsync() =>
        GetAsync<List<IngredientResponse>>("api/ingredients");

    public Task<ApiResult<bool>> RestockAsync(int ingredientId, decimal quantityToAdd) =>
        PostNoContentAsync($"api/ingredients/{ingredientId}/restock", new RestockRequest(quantityToAdd));

    public Task<ApiResult<PagedResponse<KitchenQueueItemResponse>>> GetKitchenQueueAsync(int page, int pageSize, string? status = null)
    {
        var query = status is null
            ? $"api/kitchen/queue?page={page}&pageSize={pageSize}"
            : $"api/kitchen/queue?page={page}&pageSize={pageSize}&status={status}";
        return GetAsync<PagedResponse<KitchenQueueItemResponse>>(query);
    }

    public Task<ApiResult<bool>> UpdateItemStatusAsync(int orderItemId, string newStatus, string eTag) =>
        PatchNoContentAsync($"api/kitchen/items/{orderItemId}/status", new UpdateItemStatusRequest(newStatus), eTag);

    public Task<ApiResult<bool>> VoidItemAsync(int orderItemId, string reason) =>
        PostNoContentAsync($"api/kitchen/items/{orderItemId}/void", new VoidItemRequest(reason));

    private async Task<ApiResult<TResponse>> GetAsync<TResponse>(string url)
    {
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult<TResponse>.Failure(await ReadErrorAsync(response));
        }

        var value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        return ApiResult<TResponse>.Success(value!);
    }

    private async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body)
    {
        var response = await _httpClient.PostAsJsonAsync(url, body, JsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult<TResponse>.Failure(await ReadErrorAsync(response));
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return ApiResult<TResponse>.Success(default!);
        }

        var value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        return ApiResult<TResponse>.Success(value!);
    }

    private async Task<ApiResult<bool>> PostNoContentAsync<TRequest>(string url, TRequest body)
    {
        var response = await _httpClient.PostAsJsonAsync(url, body, JsonOptions);
        return response.IsSuccessStatusCode
            ? ApiResult<bool>.Success(true)
            : ApiResult<bool>.Failure(await ReadErrorAsync(response));
    }

    private async Task<ApiResult<bool>> PatchNoContentAsync<TRequest>(string url, TRequest body, string eTag)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: JsonOptions) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{eTag}\""));

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode
            ? ApiResult<bool>.Success(true)
            : ApiResult<bool>.Failure(await ReadErrorAsync(response));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Response body wasn't a ProblemDetails JSON payload; fall back below.
        }

        return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }
}
