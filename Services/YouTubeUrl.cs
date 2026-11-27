namespace RealityCheckDesktop.Services;

public static class YouTubeUrl
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(value)) return false;
        var address = value.Trim();
        if (!address.Contains("://", StringComparison.Ordinal)) address = $"https://{address}";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        if (host is "youtu.be")
        {
            var shortId = uri.AbsolutePath.Trim('/');
            if (!ValidId(shortId)) return false;
            normalized = $"https://www.youtube.com/watch?v={shortId}";
            return true;
        }
        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")) return false;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = uri.AbsolutePath == "/watch" ? ReadQuery(uri.Query, "v") :
            segments.Length == 2 && segments[0] is "shorts" or "live" or "embed" ? segments[1] : "";
        if (!ValidId(id)) return false;
        normalized = $"https://www.youtube.com/watch?v={id}";
        return true;
    }
    private static bool ValidId(string? id) => id is { Length: 11 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    private static string? ReadQuery(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name) return Uri.UnescapeDataString(parts.ElementAtOrDefault(1) ?? "");
        }
        return null;
    }
}
