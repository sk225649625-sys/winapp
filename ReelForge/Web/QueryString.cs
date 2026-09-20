namespace ReelForge.Web;

internal static class QueryString
{
    public static string? Get(Uri uri, string key)
    {
        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length != 2) continue;
            var k = Uri.UnescapeDataString(pair[0].Replace("+", " "));
            if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            return Uri.UnescapeDataString(pair[1].Replace("+", " "));
        }
        return null;
    }
}
