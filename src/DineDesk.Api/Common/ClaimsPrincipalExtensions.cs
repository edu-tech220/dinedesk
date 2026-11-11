using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DineDesk.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.Parse(sub ?? throw new InvalidOperationException("Token is missing a subject claim."));
    }
}
