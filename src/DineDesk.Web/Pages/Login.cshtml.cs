using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DineDesk.Web.ApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DineDesk.Web.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly DineDeskApiClient _apiClient;

    public LoginModel(DineDeskApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _apiClient.LoginAsync(Username, Password);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        var login = result.Value!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, login.Username),
            new(ClaimTypes.Role, login.Role),
            new("access_token", login.Token),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = login.ExpiresAtUtc,
        });

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }
}
