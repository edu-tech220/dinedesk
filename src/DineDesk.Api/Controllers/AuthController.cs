using DineDesk.Api.Contracts;
using DineDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DineDesk.Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password);
        return FromResult(result, r => Ok(new LoginResponse(r.Token, r.ExpiresAtUtc, r.Username, r.Role)));
    }
}
