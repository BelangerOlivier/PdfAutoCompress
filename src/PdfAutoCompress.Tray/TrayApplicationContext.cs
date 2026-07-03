using PdfAutoCompress.Core;

namespace PdfAutoCompress.Tray;

/// <summary>
/// Owns the tray icon, menu, background engine, notifications and update checks.
/// This is the app: there is no main window, just the notification-area icon.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;
    private readonly IntPtr _iconHandle;
    private readonly PdfWatcher _watcher = new();
    private readonly SynchronizationContext _ui = SynchronizationContext.Current ?? new();

    private AppConfig _config;
    private SettingsForm? _settingsForm;
    private ToolStripMenuItem _pauseItem = null!;
    private bool _paused;

    public TrayApplicationContext(string[] args)
    {
        _config = AppConfig.Load();
        _icon = IconFactory.Create(out _iconHandle);

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "PDF Auto-Compress",
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

        _watcher.Compressed += OnCompressed;

        bool startedAtLogin = args.Contains(StartupManager.StartupArg, StringComparer.OrdinalIgnoreCase);
        _ = StartAsync(startedAtLogin);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var settings = new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings())
        { Font = new Font(menu.Font, FontStyle.Bold) };
        var openFolder = new ToolStripMenuItem("Open watched folder", null, (_, _) =>
            SettingsForm.OpenUrl(_watcher.WatchFolder));
        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => TogglePause());
        var checkUpdates = new ToolStripMenuItem("Check for updates…", null,
            async (_, _) => await CheckForUpdatesAsync(interactive: true));
        var quit = new ToolStripMenuItem("Quit", null, (_, _) => ExitThread());

        menu.Items.Add(settings);
        menu.Items.Add(openFolder);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(checkUpdates);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);

        menu.Opening += (_, _) => _pauseItem.Text = _paused ? "Resume" : "Pause";
        return menu;
    }

    private async Task StartAsync(bool startedAtLogin)
    {
        if (startedAtLogin && _config.StartupDelaySeconds > 0)
            await Task.Delay(TimeSpan.FromSeconds(_config.StartupDelaySeconds));

        StartWatcher();

        if (!startedAtLogin)
            MaybeOfferInstall();

        if (_config.CheckForUpdates)
            await CheckForUpdatesAsync(interactive: false);
    }

    private void MaybeOfferInstall()
    {
        if (Installer.RunningFromInstall || _config.SetupPromptShown)
            return;

        _config.SetupPromptShown = true;
        try { _config.Save(); } catch { /* non-fatal */ }

        OnInstallClicked();
    }

    private void OnInstallClicked()
    {
        if (Installer.RunningFromInstall)
            return;

        if (MessageBox.Show(
                "Install PDF Auto-Compress on this PC?\n\n" +
                $"It will be copied to:\n{Installer.InstallDir}\n\n" +
                "It is also added to the Start menu, and set to launch automatically on startup",
                "Install", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            Installer.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Install failed:\n" + ex.Message, "PDF Auto-Compress",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _tray.Visible = false;
        ExitThread(); // the installed copy relaunches once this process exits
    }

    private void StartWatcher()
    {
        if (_paused) return;
        string? error = _watcher.Start(_config);
        if (error != null)
            _tray.ShowBalloonTip(8000, "PDF Auto-Compress — not watching", error, ToolTipIcon.Warning);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_paused) _watcher.Stop();
        else StartWatcher();
        _tray.Text = _paused ? "PDF Auto-Compress (paused)" : "PDF Auto-Compress";
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_config.Clone(), _watcher, _icon);
        _settingsForm = form;
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.Result;
            if (!_paused) StartWatcher();
        }
        form.Dispose();
        _settingsForm = null;
    }

    private void OnCompressed(CompressResult r)
    {
        if (!_config.ShowNotifications) return;
        _ui.Post(_ =>
        {
            try
            {
                _tray.ShowBalloonTip(4000, "PDF compressed",
                    $"{Path.GetFileName(r.File)} — {r.SavedPercent:F0}% smaller", ToolTipIcon.Info);
            }
            catch (ObjectDisposedException) { }
        }, null);
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        UpdateInfo? info = await UpdateChecker.CheckAsync(AppConfig.UpdateRepo.Trim());
        if (info is { } u)
        {
            if (interactive)
            {
                if (MessageBox.Show(
                        $"A new version is available: {u.Tag}\n" +
                        $"You have {UpdateChecker.CurrentVersion()}.\n\nOpen the download page?",
                        "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                    == DialogResult.Yes)
                {
                    SettingsForm.OpenUrl(u.HtmlUrl);
                }
            }
            else
            {
                _tray.ShowBalloonTip(8000, "Update available",
                    $"Version {u.Tag} is available (you have {UpdateChecker.CurrentVersion()}). " +
                    "Open Settings ▸ Check for updates to download.", ToolTipIcon.Info);
            }
        }
        else if (interactive)
        {
            MessageBox.Show("You're up to date (or the repo/release couldn't be reached).",
                "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher.Compressed -= OnCompressed;
            _watcher.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _icon.Dispose();
            IconFactory.Destroy(_iconHandle);
            _settingsForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
