using PdfAutoCompress.Core;

namespace PdfAutoCompress.Tray;

internal static class UpdateFlow
{
    public static async Task PromptDownloadInstallAsync(IWin32Window? owner, UpdateInfo u, Action quit)
    {
        if (MessageBox.Show(owner,
                $"A new version is available: {u.Tag}\n" +
                $"You have {UpdateChecker.CurrentVersion().ToString(3)}.\n\nDownload and install now?",
                "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
            != DialogResult.Yes)
        {
            return;
        }

        // Older releases (or a misnamed asset) have no installer to download — open the page.
        if (string.IsNullOrWhiteSpace(u.InstallerUrl))
        {
            SettingsForm.OpenUrl(u.HtmlUrl);
            return;
        }

        using var dlg = new DownloadProgressForm(u);
        dlg.ShowDialog(owner);

        if (dlg.DownloadedPath is { } path)
        {
            UpdateInstaller.RunAndExit(path);
            quit();
            return;
        }

        // Download failed or was cancelled — offer the manual page.
        string detail = dlg.Error is { } ex ? $"\n\n{ex.Message}" : "";
        if (MessageBox.Show(owner,
                $"The update couldn't be downloaded automatically.{detail}\n\nOpen the download page instead?",
                "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            == DialogResult.Yes)
        {
            SettingsForm.OpenUrl(u.HtmlUrl);
        }
    }
}

internal sealed class DownloadProgressForm : Form
{
    private readonly UpdateInfo _info;
    private readonly ProgressBar _bar = new()
    { Width = 320, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
    private readonly Label _label = new() { AutoSize = true, Text = "Downloading update…" };
    private readonly CancellationTokenSource _cts = new();

    public string? DownloadedPath { get; private set; }

    public Exception? Error { get; private set; }

    public DownloadProgressForm(UpdateInfo info)
    {
        _info = info;

        Text = "Updating PDF Auto-Compress";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(360, 96);

        var cancel = new Button { Text = "Cancel", AutoSize = true, Anchor = AnchorStyles.None };
        cancel.Click += (_, _) => { _cts.Cancel(); cancel.Enabled = false; };
        CancelButton = cancel;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        layout.Controls.Add(_label, 0, 0);
        layout.Controls.Add(_bar, 0, 1);
        layout.Controls.Add(cancel, 0, 2);
        Controls.Add(layout);

        Shown += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<double>(p =>
        {
            if (_bar.Style != ProgressBarStyle.Blocks)
            {
                _bar.Style = ProgressBarStyle.Blocks;
                _bar.MarqueeAnimationSpeed = 0;
            }
            _bar.Value = (int)Math.Round(Math.Clamp(p, 0, 1) * 100);
        });

        try
        {
            DownloadedPath = await UpdateInstaller.DownloadAsync(_info, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // User cancelled: leave DownloadedPath null, no error.
        }
        catch (Exception ex)
        {
            Error = ex;
        }

        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cts.Dispose();
        base.Dispose(disposing);
    }
}
