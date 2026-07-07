using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;

namespace PdfAutoCompress.Core;

public static class UpdateInstaller
{
    public const string InstallerAssetName = "PdfAutoCompressSetup.exe";

    private static readonly HttpClient s_http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        string version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version?.ToString() ?? "0.0.0";
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PdfAutoCompress", version));
        return c;
    }

    public static string DownloadFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PdfAutoCompress", "updates");

    public static async Task<string> DownloadAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.InstallerUrl))
            throw new InvalidOperationException("This release has no installer asset to download.");

        Directory.CreateDirectory(DownloadFolder);
        string safeTag = MakeSafeFileName(info.Tag);
        string dest = Path.Combine(DownloadFolder, $"PdfAutoCompressSetup-{safeTag}.exe");
        string temp = dest + ".part";

        using (var resp = await s_http.GetAsync(
            info.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength
                ?? (info.InstallerSize > 0 ? info.InstallerSize : -1);

            await using var http = await resp.Content.ReadAsStreamAsync(ct);
            await using var file = new FileStream(
                temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);

            var buffer = new byte[1 << 16];
            long read = 0;
            int n;
            while ((n = await http.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0)
                    progress?.Report(Math.Min(1.0, (double)read / total));
            }

            // Cheap integrity check: the byte count must match the advertised size.
            if (info.InstallerSize > 0 && read != info.InstallerSize)
                throw new IOException(
                    $"Downloaded {read} bytes but expected {info.InstallerSize}; aborting.");
        }

        await VerifyChecksumAsync(info, temp, ct);

        File.Move(temp, dest, overwrite: true);
        progress?.Report(1.0);
        return dest;
    }

    private static async Task VerifyChecksumAsync(UpdateInfo info, string file, CancellationToken ct)
    {
        string checksumUrl = info.InstallerUrl + ".sha256";

        string expected;
        using (var resp = await s_http.GetAsync(checksumUrl, ct))
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return; // no checksum published for this release
            resp.EnsureSuccessStatusCode();
            string body = (await resp.Content.ReadAsStringAsync(ct)).Trim();
            // Format: "<hex>  filename"; take the leading hex token.
            expected = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
                ? parts[0].ToLowerInvariant()
                : "";
        }

        if (expected.Length == 0)
            return;

        await using var fs = File.OpenRead(file);
        byte[] hash = await System.Security.Cryptography.SHA256.HashDataAsync(fs, ct);
        string actual = Convert.ToHexStringLower(hash);

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new IOException("The downloaded installer failed its checksum verification; aborting.");
    }

    public static void RunAndExit(string installerPath)
    {
        var psi = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = "/SILENT /RELAUNCH=1",
        };
        Process.Start(psi);
    }

    private static string MakeSafeFileName(string tag)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            tag = tag.Replace(c, '_');
        return tag.Length == 0 ? "latest" : tag;
    }
}
