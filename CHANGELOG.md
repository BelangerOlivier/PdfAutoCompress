## [1.0.3] (2026-07-04)

### Features

- Install with a proper Windows installer (`PdfAutoCompressSetup.exe`) instead of the app installing itself. Updating is now as simple as running the newer installer. It closes the running app, upgrades it in place, and restarts it, so you no longer have to quit or uninstall the old version first. Uninstall from **Add or remove programs** like any other app; your settings are kept.


## [1.0.2] (2026-07-03)

### Features

- Add uninstall option to tray menu to easily uninstall the application from your computer.

### Bug fixes

- Fix an issue that prevented the installed application from running after installation; the downloaded application was the one that remained running.


## [1.0.1] (2026-07-03)

### Bug fixes

- Fix an issue that prevented the user from being prompted to install the application upon launch.


## [1.0.0] (2026-07-02)

### Features

- Automatic PDF compression that watches a folder (Downloads by default) and shrinks new PDFs
  with Ghostscript, replacing the original only when the result is smaller.
- Windows tray app with a settings window, autostart at login, "compressed" notifications,
  pause/resume, and a GitHub update check.
- Windows background service and a cross-platform command-line watcher that share the same engine.
