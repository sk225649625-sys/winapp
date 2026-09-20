namespace ReelForge.Web;

internal static class QueryString
{
    public static string? Get(Uri uri, string key)
    {
        var q = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in q)
        {
            var pair = item.Split('=', 2);
            if (pair.Length == 2 && Uri.UnescapeDataString(pair[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1].Replace("+", " "));
        }
        return null;
    }
}
