using System.Net;
using System.Net.Http.Json;
using DineDesk.Api.Contracts;
using DineDesk.Api.Tests.Infrastructure;

namespace DineDesk.Api.Tests;

public class AuthTests : ApiTestBase
{
    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsJwt()
    {
        var response = await Client.PostAsJsonAsync("api/auth/login", new LoginRequest("admin", "Admin#12345"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("api/auth/login", new LoginRequest("admin", "wrong-password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("api/tables");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
