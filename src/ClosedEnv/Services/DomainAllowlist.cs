namespace ClosedEnv.Services;

public static class DomainAllowlist
{
    public static bool IsAllowed(string? host, IReadOnlyList<string> allowlist)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var raw in allowlist)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var entry = raw.Trim().ToLowerInvariant();
            if (entry.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = entry[1..];
                var bare = entry[2..];
                if (normalized == bare || normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (normalized == entry || normalized.EndsWith("." + entry, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAllowedUri(Uri uri, IReadOnlyList<string> allowlist)
    {
        if (uri.Scheme is "about" or "data" or "blob" or "ws" or "wss")
        {
            if (uri.Scheme is "ws" or "wss")
            {
                return IsAllowed(uri.Host, allowlist);
            }

            return true;
        }

        if (uri.Scheme is "http" or "https")
        {
            return IsAllowed(uri.Host, allowlist);
        }

        return false;
    }
}
