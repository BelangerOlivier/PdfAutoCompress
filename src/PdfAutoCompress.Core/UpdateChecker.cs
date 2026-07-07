using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace PdfAutoCompress.Core;

public readonly record struct UpdateInfo(
    Version Latest, string Tag, string HtmlUrl, string InstallerUrl, long InstallerSize);

public static class UpdateChecker
{
    private static readonly HttpClient s_http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PdfAutoCompress", CurrentVersion().ToString()));
        c.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    public static Version CurrentVersion() =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version ?? new Version(0, 0, 0);

    public static async Task<UpdateInfo?> CheckAsync(string repo)
    {
        repo = repo.Trim().Trim('/');
        if (repo.Length == 0 || !repo.Contains('/'))
            return null;

        try
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var resp = await s_http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            JsonElement root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            if (tag.Length == 0)
                return null;

            if (!TryParseVersion(tag, out Version latest))
                return null;

            (string installerUrl, long installerSize) = FindInstallerAsset(root);

            return latest > CurrentVersion()
                ? new UpdateInfo(latest, tag, htmlUrl, installerUrl, installerSize)
                : null;
        }
        catch
        {
            return null; // fail quietly
        }
    }

    internal static (string Url, long Size) FindInstallerAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return ("", 0);

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (!string.Equals(name, UpdateInstaller.InstallerAssetName, StringComparison.OrdinalIgnoreCase))
                continue;

            string url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            long size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out long sz) ? sz : 0;
            return (url, size);
        }

        return ("", 0);
    }

    internal static bool TryParseVersion(string tag, out Version version)
    {
        string s = tag.TrimStart('v', 'V').Trim();
        int cut = s.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0) s = s[..cut];
        return Version.TryParse(s, out version!);
    }
}
