# PDF Auto-Compress

**Automatically shrink PDF files the moment you download them.**

PDF Auto-Compress quietly runs in the background and watches your **Downloads** folder. Whenever a new PDF shows up, it compresses it in place so the big scanned form you just saved becomes a small, e-mail-friendly file without you doing anything.

It works with **any browser** (Edge, Chrome, Firefox, …) and everything happens **on your own
computer**, your files are never uploaded anywhere.

---

## ✨ What it does

- 📉 **Compresses PDFs automatically** right after they finish downloading.
- 🖱️ **Lives in your system tray** with a simple settings window — no command line needed.
- 🔒 **100% local & private** — no accounts, no uploads, no internet required to compress.
- ✅ **Safe** — it only replaces a file when the compressed version is actually smaller.
- 🚀 **Starts with Windows** (optional) so it's always working.
- 🔔 **Tells you** how much space it saved with a small notification.

---

## 🟢 Getting started (Windows)

### Step 1 — Install Ghostscript (one time)

PDF Auto-Compress uses a free tool called **Ghostscript** to do the actual compression.

1. Download it from **[ghostscript.com/releases](https://ghostscript.com/releases/gsdnld.html)**
2. Choose the **64-bit** installer and run it (just click through — the defaults are fine).

### Step 2 — Download PDF Auto-Compress

1. Go to the **[Releases page](https://github.com/BelangerOlivier/PdfAutoCompress/releases)**.
2. Download **`PdfAutoCompress.exe`** from the latest release.
3. Put it anywhere you like (for example, your Documents folder) and **double-click it**.

That's it — a small red **PDF** icon appears in your system tray. It's now watching your Downloads folder.

> 💡 The first time Windows may show a "Windows protected your PC" screen because the app isn't
> code-signed. Click **More info → Run anyway**.

### Step 3 — Try it

Download any PDF larger than 1 MB in your browser. A moment later you'll see a notification like *"PDF compressed — XX% smaller"*, and the file in your Downloads folder will be smaller. Done!

---

## 🛠️ Using it

**Right-click the tray icon** for the menu:

| Menu item | What it does |
| --- | --- |
| **Settings…** | Open the settings window (see below). |
| **Open watched folder** | Open the folder it's watching in File Explorer. |
| **Pause / Resume** | Temporarily stop or restart automatic compression. |
| **Check for updates…** | See if a newer version is available. |
| **Quit** | Close the app. |

### Settings

Open **Settings…** from the tray menu (or double-click the tray icon) to change how it works:

| Setting | What it means |
| --- | --- |
| **Watch folder** | Which folder to watch. Leave empty to use your **Downloads** folder. |
| **Quality** | How hard to compress: **screen** (smallest files) → **ebook** (recommended) → **printer** → **prepress** (best quality, largest). |
| **Min size (MB)** | Ignore PDFs smaller than this. Set to **0** to compress every PDF. |
| **Keep original** | Instead of replacing the file, save a separate copy ending in `-compressed.pdf`. |
| **Show a notification when a PDF is compressed** | Turn the pop-ups on or off. |
| **Start automatically when I log in** | Launch PDF Auto-Compress every time you sign in to Windows. |
| **Check for updates on startup** | Let it check GitHub for a newer version. |
| **Ghostscript** | Leave empty — it finds Ghostscript on its own. Only fill this in if you installed Ghostscript somewhere unusual. |

Changes take effect as soon as you click **Save**.

---

## ❓ Troubleshooting

**The tray icon says "not watching / Ghostscript not found."**
Ghostscript isn't installed (or is in an unusual location). Do Step 1 above, then open
**Settings → Save** (or restart the app). If you installed it somewhere custom, point the
**Ghostscript** box at your `gswin64c.exe`.

**My PDF didn't get smaller.**
Some PDFs are already optimized, or are mostly text, and can't shrink much. PDF Auto-Compress
never makes a file bigger, if it can't save space, it leaves the original untouched. Files
below the **Min size** threshold are skipped too (lower it to test).

**It's not doing anything.**
Make sure the tray icon is present (it may be hidden behind the **^** arrow near the clock),
that it isn't **Paused**, and that you're saving PDFs into the folder it's watching.

**Does it upload my files?**
No. Everything happens on your computer. The only time it uses the internet is the optional
update check.

---

## 💻 Other ways to run it (advanced)

Prefer no desktop app? The same engine comes in two other flavors on the
[Releases page](https://github.com/BelangerOlivier/PdfAutoCompress/releases):

- **Background service** — installs as a Windows Service that runs silently for all sessions.
- **Command-line tool** — a tiny watcher you run in a terminal; works on **Windows, macOS and
  Linux**.

Both read the same settings and compress the same way; they just have no window.

---

## 🔧 Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/BelangerOlivier/PdfAutoCompress.git
cd PdfAutoCompress
dotnet run --project src/PdfAutoCompress.Tray
```

To produce the standalone `PdfAutoCompress.exe`:

```powershell
dotnet publish src/PdfAutoCompress.Tray -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The project is organized as a reusable core library (`PdfAutoCompress.Core`) with three
front-ends: the tray app, a Windows Service, and a cross-platform CLI.

---

*PDF Auto-Compress is a personal project and is not affiliated with Ghostscript or any browser.*
