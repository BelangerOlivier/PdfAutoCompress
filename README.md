# PDF Auto-Compress (.NET watch-folder + Ghostscript)

Watches a folder (your **Downloads** by default) and automatically compresses any new PDF with
**Ghostscript**. Everything runs locally — no files leave your PC, no browser extension, and
**no Python**. It works with **any browser** (Edge, Chrome, Firefox…) because it just watches
the folder where downloads land.

The code is split so you can use it however you like: a reusable engine plus three front-ends.

```none
PdfAutoCompress.slnx
├─ src/
│  ├─ PdfAutoCompress.Core/     <- the engine (folder watcher + Ghostscript + update check).
│  │                               Reusable class library. Cross-platform, no UI.
│  ├─ PdfAutoCompress.Tray/     <- Windows tray app: icon, settings window, autostart, updates.
│  ├─ PdfAutoCompress.Service/  <- Windows Service / background worker (also runs as a console).
│  └─ PdfAutoCompress.Cli/      <- tiny cross-platform console watcher for a terminal.
└─ legacy/                      <- the old Python + Edge-extension version (no longer used)
```

**Which one should I run?**

| You want… | Use | Notes |
| --- | --- | --- |
| A friendly desktop app with a tray icon + settings UI | **Tray** | Windows only. Autostart, notifications, update checks. |
| A silent background service that's always on | **Service** | Install as a Windows Service, or just run the exe as a console. |
| To run it in a terminal on Windows/macOS/Linux | **Cli** | Simplest and fully cross-platform. |

All three share **Core**, so compression behaviour and settings are identical.

## Prerequisites

1. **Ghostscript** — install once from https://ghostscript.com/releases/gsdnld.html
   (64-bit). Auto-detected under `C:\Program Files\gs\...\bin\gswin64c.exe` (or `gs` on
   macOS/Linux).
2. **.NET SDK 10** — only needed to *build*. Publish a self-contained exe (below) and it runs
   with no .NET installed.

## Settings

Each front-end has its own `appsettings.json` next to its executable (the Tray app also edits
it through its Settings window):

| Setting | Meaning |
| --- | --- |
| `WatchFolder` | Folder to watch. Empty = your Downloads folder. |
| `GhostscriptPath` | Full path to `gswin64c.exe`. Empty = auto-detect. |
| `PdfSettings` | `/screen` (smallest) · `/ebook` (default) · `/printer` · `/prepress` (largest). |
| `MinSizeBytes` | Skip PDFs smaller than this. `1048576` = 1 MB. `0` = compress every PDF. |
| `KeepOriginal` | `false` = overwrite in place (only if smaller). `true` = write `<name>-compressed.pdf`. |
| `ShowNotifications` | Tray only: balloon when a PDF is compressed. |
| `CheckForUpdates` | Check GitHub for a newer release on startup. |
| `UpdateRepo` | Your GitHub repo as `owner/name` for the update check. |
| `StartupDelaySeconds` | Tray only: delay before watching when launched at login (lazy start). |

## Run (development)

```powershell
dotnet run --project src/PdfAutoCompress.Tray      # desktop tray app
dotnet run --project src/PdfAutoCompress.Service   # background worker (console)
dotnet run --project src/PdfAutoCompress.Cli       # cross-platform terminal watcher
```

Each prints the detected Ghostscript path and `Watching …`. Then, in **any browser**, download
a PDF larger than 1 MB and watch it shrink. To compress *every* PDF while testing, set
`MinSizeBytes` to `0`.

## The tray app

Right-click the tray icon for **Settings…**, **Open watched folder**, **Pause/Resume**,
**Check for updates…**, and **Quit**. In Settings you can change the folder/quality/threshold,
toggle **Start automatically when I log in** (an HKCU `Run` entry — no admin needed), and set
your GitHub repo for update checks.

## Build standalone executables (no .NET needed to run)

```powershell
# Tray app — single self-contained .exe
dotnet publish src/PdfAutoCompress.Tray -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# CLI — cross-platform (swap the RID: win-x64 | linux-x64 | osx-arm64)
dotnet publish src/PdfAutoCompress.Cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output lands in each project's `bin\Release\...\publish\` folder (with `appsettings.json`
beside the exe).

## Install the Windows Service

Publish it, then register it (run the shell **as Administrator**):

```powershell
dotnet publish src/PdfAutoCompress.Service -c Release -r win-x64 --self-contained true
$exe = "C:\path\to\publish\PdfAutoCompress.Service.exe"
sc.exe create PdfAutoCompress binPath= "$exe" start= auto
sc.exe start PdfAutoCompress
# later: sc.exe stop PdfAutoCompress ; sc.exe delete PdfAutoCompress
```

The service reads `appsettings.json` from its install folder. Because it runs in the background
with no desktop, notifications and update pop-ups are off by default for the service.

## Notes

- The original file is overwritten **only if** the compressed version is actually smaller;
  otherwise it's left untouched.
- Already-optimized or text-only PDFs may not shrink much.
- The engine waits until a download has fully finished writing before compressing, and never
  reprocesses its own output.
