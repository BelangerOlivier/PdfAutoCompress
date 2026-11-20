# PDF Auto-Compress

## Automatically compress PDF files at any time

PDF Auto-Compress quietly runs in the background and watches your folders. Whenever a new PDF shows up, 
it compresses it in place without you doing anything.

Your files are never uploaded anywhere.

## What it does

- [X] Compresses PDFs automatically as soon as they land in a watched folder (your Downloads folder by default).
- [X] Lives in your system tray with a simple settings window and no command line needed.
- [X] 100% local: no accounts, no uploads, no internet required to compress.
- [X] Safe: a file is only replaced when the compressed version is smaller **and** has the same number of pages.
- [X] Never compresses the same file twice: compressed PDFs are tagged so they are skipped next time.
- [X] Tells you how much space it saved with a small notification.
- [X] Can keep the original and save the result next to is as `name-compressed.pdf`.

## How it works

1. The app watches each configured folder for new or renamed `.pdf` files.
2. It waits until the file is fully written (for example, until a download finishes).
3. It skips files that are too small, already compressed, or named `*-compressed.pdf`.
4. [Ghostscript](https://www.ghostscript.com/) compresses the file into a temporary copy.
5. If the copy is smaller and has the same page count, it replaces the original. Otherwise, the original is left untouched.

## Getting started

** Requirements **

- [Ghostscript](https://www.ghostscript.com/releases/gsdnld.html) installed (`gswin64c.exe` on Windows, `gs` on Linux). It is not bundled with the app.
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source.

** Build and run from source**
```bash
git clone https://github.com/BelangerOlivier/PdfAutoCompress.git
cd PdfAutoCompress
dotnet run --project src/PdfAutoCompress.Tray
```

The app opens in the system tray. Right-click the icon and choose **Settings** to pick your folders.

> A Windows installer (per-user, no admin rights needed) is on the way and will be published under [Releases](https://github.com/BelangerOlivier/PdfAutoCompress/releases).

## Settings
 
Settings are saved to `%APPDATA%\PdfAutoCompress\appsettings.json`.
 
| Setting | Default | Description |
|---|---|---|
| Watched folders | Downloads | Folders to watch for new PDFs (not recursive). |
| Ghostscript location | auto-detect | Path to the Ghostscript executable, if it isn't found automatically. |
| Keep original | Off | Save the result as `name-compressed.pdf` instead of replacing the file. |
| Minimum size | 1 MB | Smaller files are ignored. |
| Quality | `/ebook` | Ghostscript preset: `/screen` (smallest), `/ebook` (recommended), `/printer`, `/prepress` (best quality). |
| Show notification | On | Show how much space was saved after each file. |
| Launch on startup | Off | Start the app when you sign in (Windows). |

Logs are kept for 7 days in `%APPDATA%\PdfAutoCompress\logs\` and are also visible live in the Settings window.

## Platform support
 
Built with [Eto.Forms](https://github.com/picoe/Eto), so the app runs on **Windows** and **Linux** (GTK: build with `-p:BuildPlatform=Gtk`).
Notifications and launch on startup are currently Windows-only.

## Development
 
```bash
dotnet build PdfAutoCompress.slnx
dotnet test src/PdfAutoCompress.Core.Tests
```
 
- `PdfAutoCompress.Core`: folder watching, Ghostscript, PDF inspection (PdfPig), settings and logging (Serilog). No UI.
- `PdfAutoCompress.Tray`: the tray app and settings window.
- `PdfAutoCompress.Core.Tests`: xUnit tests. Ghostscript is faked, so it doesn't need to be installed to run them.

## License
 
[MIT](LICENSE)

---

*PDF Auto-Compress is a personal project and is not affiliated with Ghostscript.*
