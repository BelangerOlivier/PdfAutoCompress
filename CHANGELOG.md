## [1.0.4] (2026-07-05)

### Features

- Add a compression marker to the compressed pdf to avoid compressing a file that has already been compressed, renamed or copied.
- Reduced the file size of the Core library and the Tray project by half.

### Bug fixes

- Correct the version format display so that it does not include the revision number.
- Correct the initial indentation of certain log messages.
- Fixed process output handling to prevent blocking during Ghostscript execution.


## [1.0.3] (2026-07-04)

### Features

- Install the application using the appropriate Windows Installer (`PdfAutoCompressSetup.exe`) instead of letting it install automatically. Uninstall it via **Add/Remove programs** like any other application; your settings will be preserved.


## [1.0.2] (2026-07-03)

### Features

- Add an uninstall option to the system tray menu to easily uninstall the application from your computer.

### Bug fixes

- Fix an issue that prevented the installed application from running after installation; the downloaded application remained running instead.


## [1.0.1] (2026-07-03)

### Bug fixes

- Fix an issue that prevented the user from being prompted to install the application at launch.


## [1.0.0] (2026-07-02)

### Features

- Automatic PDF compression that watches a folder (Downloads by default) and reduces the size of new PDFs using Ghostscript. It only replaces the original when the result is smaller.
- Windows tray app with a settings window, automatic startup at login, "compressed" notifications, pause/resume, and GitHub update check.
- A Windows background service and a cross-platform command-line watcher that share the same engine.
