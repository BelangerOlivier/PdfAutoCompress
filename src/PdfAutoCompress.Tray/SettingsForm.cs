using System.Diagnostics;
using PdfAutoCompress.Core;

namespace PdfAutoCompress.Tray;

/// <summary>
/// Small settings window: edit config, toggle autostart, view recent activity, check updates.
/// Edits a clone; on OK the caller reads <see cref="Result"/> and restarts the watcher.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly PdfWatcher _watcher;

    public AppConfig Result => _config;

    private readonly TextBox _watchFolder = new() { Width = 300 };
    private readonly TextBox _ghostscript = new() { Width = 300 };
    private readonly ComboBox _pdfSettings = new()
    { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _minMb = new()
    { DecimalPlaces = 1, Minimum = 0, Maximum = 4096, Increment = 0.1M, Width = 90 };
    private readonly CheckBox _keepOriginal = new() { Text = "Keep original (write “-compressed.pdf” instead of overwriting)", AutoSize = true };
    private readonly CheckBox _notify = new() { Text = "Show a notification when a PDF is compressed", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Launch automatically on startup", AutoSize = true };
    private readonly CheckBox _checkUpdates = new() { Text = "Check for updates on startup", AutoSize = true };
    private readonly TextBox _log = new()
    { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 525, Height = 120, Font = new Font("Consolas", 8.5f) };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(525, 0) };

    public SettingsForm(AppConfig config, PdfWatcher watcher, Icon icon)
    {
        _config = config;
        _watcher = watcher;

        Text = "PDF Auto-Compress — Settings";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        BuildUi();
        LoadFromConfig();

        _watcher.Logger.Log += OnWatcherLog;
        FormClosed += (_, _) => _watcher.Logger.Log -= OnWatcherLog;
    }

    private void BuildUi()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;
        void AddRow(string label, Control field, Control? third = null)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 6) }, 0, row);
            grid.Controls.Add(field, 1, row);
            if (third != null) grid.Controls.Add(third, 2, row);
            row++;
        }
        void AddSpan(Control field)
        {
            grid.Controls.Add(field, 0, row);
            grid.SetColumnSpan(field, 3);
            row++;
        }

        _pdfSettings.Items.AddRange(["/screen", "/ebook", "/printer", "/prepress"]);

        var browseFolder = new Button { Text = "Browse…", AutoSize = true };
        browseFolder.Click += (_, _) => BrowseFolder();
        var browseGs = new Button { Text = "Browse…", AutoSize = true };
        browseGs.Click += (_, _) => BrowseGhostscript();

        AddRow("Watch folder:", _watchFolder, browseFolder);
        AddRow("(empty = Downloads folder)", new Label { AutoSize = true }, null);
        AddRow("Ghostscript:", _ghostscript, browseGs);
        AddRow("(empty = auto-detect)", new Label { AutoSize = true }, null);
        AddRow("Quality:", _pdfSettings);
        AddRow("Min size (MB):", _minMb);
        AddSpan(_keepOriginal);
        AddSpan(_notify);
        AddSpan(new Label { Height = 6 });
        AddSpan(_startup);
        AddSpan(_checkUpdates);

        var checkNow = new Button { Text = "Check for updates now", AutoSize = true };
        checkNow.Click += async (_, _) => await CheckNow();

        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
        save.Click += (_, _) => SaveToConfig();
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(checkNow);

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
        };
        root.Controls.Add(grid);
        root.Controls.Add(new Label { Text = "Recent activity:", AutoSize = true, Margin = new Padding(3, 8, 0, 2) });
        root.Controls.Add(_log);
        root.Controls.Add(_status);
        root.Controls.Add(buttons);
        Controls.Add(root);
    }

    private void LoadFromConfig()
    {
        _watchFolder.Text = _config.WatchFolder;
        _ghostscript.Text = _config.GhostscriptPath;
        _pdfSettings.SelectedItem = _config.PdfSettings;
        if (_pdfSettings.SelectedIndex < 0) _pdfSettings.SelectedItem = "/ebook";
        _minMb.Value = (decimal)Math.Round(_config.MinSizeBytes / (1024.0 * 1024.0), 1);
        _keepOriginal.Checked = _config.KeepOriginal;
        _notify.Checked = _config.ShowNotifications;
        _checkUpdates.Checked = _config.CheckForUpdates;
        _startup.Checked = StartupManager.IsEnabled();

        foreach (string line in _watcher.Logger.RecentLog)
            _log.AppendText(line + Environment.NewLine);

        string gs = GhostscriptChecker.ResolveGhostscript(_config.GhostscriptPath);
        _status.Text = $"Version {UpdateChecker.CurrentVersion()}   •   " +
            (gs.Length > 0 ? $"Ghostscript: {gs}" : "Ghostscript: NOT FOUND — install it or set the path above.");
    }

    private void SaveToConfig()
    {
        _config.WatchFolder = _watchFolder.Text.Trim();
        _config.GhostscriptPath = _ghostscript.Text.Trim();
        _config.PdfSettings = _pdfSettings.SelectedItem as string ?? "/ebook";
        _config.MinSizeBytes = (long)Math.Round((double)_minMb.Value * 1024 * 1024);
        _config.KeepOriginal = _keepOriginal.Checked;
        _config.ShowNotifications = _notify.Checked;
        _config.CheckForUpdates = _checkUpdates.Checked;

        try { StartupManager.SetEnabled(_startup.Checked); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not update the startup setting:\n" + ex.Message,
                "PDF Auto-Compress", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        try { _config.Save(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save settings:\n" + ex.Message,
                "PDF Auto-Compress", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder to watch" };
        if (_watchFolder.Text.Length > 0 && Directory.Exists(_watchFolder.Text))
            dlg.SelectedPath = _watchFolder.Text;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _watchFolder.Text = dlg.SelectedPath;
    }

    private void BrowseGhostscript()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select gswin64c.exe",
            Filter = "Ghostscript console (gswin*c.exe)|gswin*c.exe|Executables (*.exe)|*.exe",
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _ghostscript.Text = dlg.FileName;
    }

    private async Task CheckNow()
    {
        string repo = AppConfig.UpdateRepo.Trim();
        if (repo.Length == 0)
        {
            MessageBox.Show(this, "Enter your GitHub repo (owner/name) first.",
                "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        UpdateInfo? info = await UpdateChecker.CheckAsync(repo);
        if (info is { } u)
        {
            if (MessageBox.Show(this,
                    $"A new version is available: {u.Tag}\n" +
                    $"You have {UpdateChecker.CurrentVersion()}.\n\nOpen the download page?",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                == DialogResult.Yes)
            {
                OpenUrl(u.HtmlUrl);
            }
        }
        else
        {
            MessageBox.Show(this, "You're up to date (or the repo/release couldn't be reached).",
                "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnWatcherLog(string line)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() => _log.AppendText(line + Environment.NewLine));
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }
}
