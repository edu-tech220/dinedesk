namespace DineDesk.Api.Options;

public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 60;

    public int WindowSeconds { get; set; } = 10;
}
