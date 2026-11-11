namespace DineDesk.Api.Common;

/// <summary>Reads/writes optimistic-concurrency tokens as ETag-style base64 strings over If-Match headers.</summary>
public static class ConcurrencyHeader
{
    public static string ToETag(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryParse(string? ifMatchHeader, out byte[] rowVersion)
    {
        if (string.IsNullOrWhiteSpace(ifMatchHeader))
        {
            rowVersion = Array.Empty<byte>();
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(ifMatchHeader.Trim('"'));
            return true;
        }
        catch (FormatException)
        {
            rowVersion = Array.Empty<byte>();
            return false;
        }
    }
}
