using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DineDesk.Api.Data;
using DineDesk.Api.Options;
using DineDesk.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DineDesk.Api.Services;

public record LoginResult(string Token, DateTime ExpiresAtUtc, string Username, string Role);

public interface IAuthService
{
    Task<ServiceResult<LoginResult>> LoginAsync(string username, string password);
}

public class AuthService : IAuthService
{
    private readonly DineDeskDbContext _db;
    private readonly JwtOptions _jwtOptions;

    public AuthService(DineDeskDbContext db, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<ServiceResult<LoginResult>> LoginAsync(string username, string password)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == username);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return ServiceResult<LoginResult>.Failure("Invalid username or password.", ServiceErrorType.Validation);
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return ServiceResult<LoginResult>.Success(new LoginResult(tokenString, expiresAtUtc, user.Username, user.Role.ToString()));
    }
}
