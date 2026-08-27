using System.Linq;

using Eto.Drawing;
using Eto.Forms;

using PdfAutoCompress.Core;

namespace PdfAutoCompress.Tray;

public class SettingsForm : Form
{
    private readonly AppConfig _config;

    private readonly ListBox _watchedFolders = new();
    private readonly TextBox _ghostscriptPath = new();
    private readonly DropDown _quality = new();
    private readonly NumericStepper _minSizeMb = new();
    private readonly CheckBox _keepOriginal = new();
    private readonly CheckBox _showNotification = new();
    private readonly CheckBox _launchOnStartup = new();
    private readonly CheckBox _checkForUpdatesOnStartup = new();
    private readonly ListBox _recentActivity = new() { Enabled = false };

    public SettingsForm()
    {
        Title = "PDF Auto Compress - Settings";
        ClientSize = new Size(480, 600);
        Resizable = false;

        _config = AppConfig.Load();
        PopulateFromConfig();

        var addFolderButton = new Button { Text = "Add..." };
        addFolderButton.Click += (_, _) => AddWatchedFolder();

        var removeFolderButton = new Button { Text = "Remove" };
        removeFolderButton.Click += (_, _) => RemoveSelectedWatchedFolder();

        var browseGhostscriptButton = new Button { Text = "Browse..." };
        browseGhostscriptButton.Click += (_, _) => BrowseForGhostscript();

        var saveButton = new Button { Text = "Save" };
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Click += (_, _) => Close();

        var layout = new DynamicLayout { Padding = 10, DefaultSpacing = new Size(6, 6) };

        layout.AddRow(new Label { Text = "Watched folders" });
        layout.AddRow(_watchedFolders);
        layout.AddRow(TableLayout.HorizontalScaled(6, addFolderButton, removeFolderButton));

        layout.AddRow(new Label { Text = "Ghostscript executable" });
        layout.AddRow(TableLayout.Horizontal(6, new TableCell(_ghostscriptPath, scaleWidth: true), browseGhostscriptButton));

        layout.AddRow(new Label { Text = "Quality" });
        layout.AddRow(_quality);

        layout.AddRow(new Label { Text = "Min size (MB)" });
        layout.AddRow(_minSizeMb);

        layout.AddRow(_keepOriginal);
        layout.AddRow(_showNotification);
        layout.AddRow(_launchOnStartup);
        layout.AddRow(_checkForUpdatesOnStartup);

        layout.AddRow(new Label { Text = "Recent activity" });
        layout.AddRow(new TableRow(_recentActivity) { ScaleHeight = true });

        layout.AddRow(TableLayout.HorizontalScaled(6, saveButton, cancelButton));

        Content = layout;
    }

    private void PopulateFromConfig()
    {
        foreach (string folder in _config.WatchedFolders)
        {
            _watchedFolders.Items.Add(folder);
        }

        _ghostscriptPath.Text = _config.GhostscriptLocation;

        _quality.Items.Add(new ListItem { Key = "/screen", Text = "/screen - Low resolution output, smallest file size" });
        _quality.Items.Add(new ListItem { Key = "/ebook", Text = "/ebook - Medium resolution output, good balance (recommended)" });
        _quality.Items.Add(new ListItem { Key = "/printer", Text = "/printer - High resolution output, larger file size" });
        _quality.Items.Add(new ListItem { Key = "/prepress", Text = "/prepress - High-quality output for printing, largest file size" });
        _quality.SelectedKey = _config.PdfSettings;

        _minSizeMb.Increment = 0.1;
        _minSizeMb.DecimalPlaces = 1;
        _minSizeMb.MinValue = 0;
        _minSizeMb.Value = _config.MinSizeBytes / (1024.0 * 1024.0);

        _keepOriginal.Text = "Keep original (write \"-compressed.pdf\" instead of overwriting)";
        _keepOriginal.Checked = _config.KeepOriginal;

        _showNotification.Text = "Show a notification when a PDF is compressed";
        _showNotification.Checked = _config.ShowNotification;

        _launchOnStartup.Text = "Launch automatically on startup";
        _launchOnStartup.Checked = StartupManager.IsEnabled();

        _checkForUpdatesOnStartup.Text = "Check for updates on startup";
        _checkForUpdatesOnStartup.Checked = _config.CheckForUpdatesOnStartup;
    }

    private void AddWatchedFolder()
    {
        var dialog = new SelectFolderDialog();
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;

        string folder = dialog.Directory;
        if (string.IsNullOrEmpty(folder)) return;

        bool alreadyWatched = _watchedFolders.Items.Any(item => item.Text == folder);
        if (alreadyWatched) return;

        _watchedFolders.Items.Add(folder);
    }

    private void RemoveSelectedWatchedFolder()
    {
        if (_watchedFolders.SelectedIndex < 0) return;

        _watchedFolders.Items.RemoveAt(_watchedFolders.SelectedIndex);
    }

    private void BrowseForGhostscript()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select the Ghostscript executable",
            Filters = { new FileFilter("Ghostscript executable", ".exe") }
        };
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;

        _ghostscriptPath.Text = dialog.FileName;
    }

    private void SaveAndClose()
    {
        _config.WatchedFolders = _watchedFolders.Items.Select(item => item.Text).ToList();
        _config.GhostscriptLocation = _ghostscriptPath.Text;
        _config.PdfSettings = _quality.SelectedKey;
        _config.MinSizeBytes = (long)(_minSizeMb.Value * 1024 * 1024);
        _config.KeepOriginal = _keepOriginal.Checked ?? false;
        _config.ShowNotification = _showNotification.Checked ?? false;
        _config.LaunchOnStartup = _launchOnStartup.Checked ?? false;
        _config.CheckForUpdatesOnStartup = _checkForUpdatesOnStartup.Checked ?? false;

        _config.Save();
        StartupManager.SetEnabled(_config.LaunchOnStartup);

        Close();
    }
}
