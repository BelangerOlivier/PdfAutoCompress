# PDF Auto-Compress

**Automatically compress PDF files the moment you download them.**

PDF Auto-Compress quietly runs in the background and watches the folders you tell it to watch. Whenever a new PDF shows up, it compresses it in place to become a small, e-mail-friendly file without you doing anything.

It currently works only on Windows.
Your files are never uploaded anywhere.

## What it does

- **Compresses PDFs automatically** right after they finish downloading.
- **Lives in your system tray** with a simple settings window and no command line needed.
- **100% local** — no accounts, no uploads, no internet required to compress.
- **Safe** — it only replaces a file when the compressed version is actually smaller.
- **Tells you** how much space it saved with a small notification.

## Getting started (Windows)

### Step 1 — Install Ghostscript

PDF Auto-Compress uses a free tool called **Ghostscript** to do the actual compression.

1. Download it from **[ghostscript.com/releases](https://ghostscript.com/releases/gsdnld.html)**
2. Choose the **64-bit** installer and run it (just click through, the defaults are fine).

### Step 2 — Download PDF Auto-Compress

1. Go to the **[Releases page](https://github.com/BelangerOlivier/PdfAutoCompress/releases)**.
2. Download **`PdfAutoCompressSetup.exe`** from the latest release.
3. **Run the installer**, it sets everything up for you, no admin rights needed. It installs to a per-user folder, adds a Start-menu entry, and (optional) sets the app to start with Windows.
4. A small red **PDF** icon appears in your system tray. It's now watching your Downloads folder.

### Step 3 — Try it

Download any PDF larger than the selected limit. After a few seconds you'll see a notification like *"PDF compressed — XX% smaller"*, and the file in your folder will be smaller.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/BelangerOlivier/PdfAutoCompress.git
cd PdfAutoCompress
dotnet run --project src/PdfAutoCompress.Tray
```

---

*PDF Auto-Compress is a personal project and is not affiliated with Ghostscript.*