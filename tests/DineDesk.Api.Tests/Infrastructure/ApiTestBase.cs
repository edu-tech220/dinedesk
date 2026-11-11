using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineDesk.Api.Contracts;

namespace DineDesk.Api.Tests.Infrastructure;

public abstract class ApiTestBase : IDisposable
{
    // Mirrors the API's JsonStringEnumConverter so enum-valued contracts (TableStatus, etc.)
    // round-trip the same way a real client would see them, instead of as raw integers.
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected readonly DineDeskApiFactory Factory;
    protected readonly HttpClient Client;

    protected ApiTestBase()
    {
        Factory = new DineDeskApiFactory();
        Client = Factory.CreateClient();
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    protected Task<HttpClient> AsAdminAsync() => CreateAuthenticatedClientAsync("admin", "Admin#12345");

    protected Task<HttpClient> AsManagerAsync() => CreateAuthenticatedClientAsync("manager", "Manager#12345");

    protected Task<HttpClient> AsServerAsync() => CreateAuthenticatedClientAsync("server1", "Server#12345");

    protected Task<HttpClient> AsKitchenAsync() => CreateAuthenticatedClientAsync("kitchen1", "Kitchen#12345");

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
