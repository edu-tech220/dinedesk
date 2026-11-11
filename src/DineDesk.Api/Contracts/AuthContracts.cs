namespace DineDesk.Api.Contracts;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Username, string Role);
