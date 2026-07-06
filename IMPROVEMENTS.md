# PDF Auto-Compress — Improvement Roadmap

A prioritized list of hardening, feature, tooling, and testing ideas. Each item lists **what** it does, **how** it works, and **how** to implement it. Nothing here is required for the app to function — these raise robustness, trust, and contributor-readiness. Item numbers are stable references.

## 🔴 Correctness / bugs (small, high-value — do first)

### 1. Centralize and fix the app version

- **What it does:** Makes every build report its true version, so the in-app "Check for updates" and the CLI/tray version display are accurate.
- **How it works:** `UpdateChecker.CurrentVersion()` reads the entry assembly's `AssemblyVersion`, which comes from `<Version>` in the csproj. Today `Tray.csproj:15` and `Cli.csproj:10` hardcode `1.0.3`, the Service csproj has none (→ 1.0.0), and the CHANGELOG is at 1.0.4 — so local builds misreport and the update check compares against a stale number. CI overrides it with `-p:Version` from the git tag, hiding the bug in releases only.
- **How to implement:** Delete the per-project `<Version>` lines and add one `<Version>1.0.4</Version>` (or `<VersionPrefix>`) to a repo-root `Directory.Build.props` (see #12) so all projects inherit it. Keep the CI tag override as-is; it still wins at release time.

### 2. Bound concurrent Ghostscript processes

- **What it does:** Prevents a burst of downloads from spawning dozens of `gswin64c.exe` processes at once (CPU/RAM spike), which matters most for the always-on Service.
- **How it works:** `PdfWatcher.OnCreatedOrRenamed` fires `_ = HandleAsync(...)` per file event with no limit ([PdfWatcher.cs:79](src/PdfAutoCompress.Core/PdfWatcher.cs#L79)); each call eventually runs one Ghostscript process. Thirty PDFs dropped together → thirty parallel processes.
- **How to implement:** Add a `SemaphoreSlim(maxConcurrent)` field to `PdfWatcher`, `await _gate.WaitAsync()` around the `ProcessAsync` call and `Release()` in a `finally`. Make `maxConcurrent` a new `AppConfig` field (default 1–2). Add a test asserting the in-flight count never exceeds the bound (needs #18).

### 3. Fix the stale doc comment (or build what it claims)

- **What it does:** Removes a misleading comment so the code documents reality.
- **How it works:** [PdfCompressor.cs:8](src/PdfAutoCompress.Core/PdfCompressor.cs#L8) says the compressor is used by "the Explorer context menu" and a "Compress a file now" tray item — neither exists anywhere in the codebase (confirmed by search).
- **How to implement:** Either edit the comment to list only the real callers (`PdfWatcher`, tests), or implement those features (see Feature #F1) and keep the comment. Cheapest fix: correct the comment now, track the features separately.

### 4. Log when a file never becomes readable

- **What it does:** Tells the user when a download was seen but could never be opened, instead of silently dropping it.
- **How it works:** `WaitUntilReadyAsync` polls the file (opening with `FileShare.None`) for up to 5 minutes and returns `false` on timeout with no log line ([PdfWatcher.cs:163](src/PdfAutoCompress.Core/PdfWatcher.cs#L163)); a file locked by antivirus or a stalled browser just vanishes from the activity log.
- **How to implement:** In `ProcessAsync`, when `WaitUntilReadyAsync` returns `false`, call `Logger.Emit($"Gave up waiting for {name} (still locked after 5 min).")` before returning.

### 5. Pick the newest Ghostscript, and find more installs

- **What it does:** Uses the latest installed Ghostscript and detects installs from winget/scoop/choco.
- **How it works:** `GhostscriptChecker.ResolveGhostscript` iterates `C:\Program Files\gs\*\bin` in filesystem order and returns the *first* match ([GhostscriptChecker.cs:25](src/PdfAutoCompress.Core/GhostscriptChecker.cs#L25)) — which can be an older version — and only checks two Program Files roots plus PATH.
- **How to implement:** Sort the `Directory.GetDirectories(root)` results descending (parse the trailing version number) before scanning, and add common package-manager locations (e.g. `%LOCALAPPDATA%\Microsoft\WinGet\Packages`, `%USERPROFILE%\scoop\apps\ghostscript`). Existing `GhostscriptCheckerTests` gives a place to add cases.

## 🟢 Robustness / safety (this app overwrites user files)

### 6. Send the original to the Recycle Bin instead of hard-overwriting

- **What it does:** Makes a bad compression recoverable — the user can restore the original from the Recycle Bin.
- **How it works:** In-place mode does `File.Move(tmp, dest, overwrite: true)` ([PdfCompressor.cs:68](src/PdfAutoCompress.Core/PdfCompressor.cs#L68)), permanently discarding the original bytes.
- **How to implement:** Before the move, when not `KeepOriginal`, send the source to the Recycle Bin via `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(src, ..., RecycleOption.SendToRecycleBin)` (Windows only — guard with an OS check), then move the temp into place. Gate behind a config toggle if you want it optional.

### 7. Validate the compressed PDF before replacing

- **What it does:** Guards against Ghostscript occasionally producing a smaller but broken PDF.
- **How it works:** Currently any smaller output is accepted. You already reference PdfPig, which can open a PDF and count pages.
- **How to implement:** After Ghostscript writes the temp file and before the move, open both source and temp with `PdfDocument.Open` and compare `NumberOfPages`; if the temp fails to open or has fewer pages, discard it and log. Reuse the pattern already in `IsAlreadyCompressed`.

### 8. Harden the folder watcher against dropped/missed files

- **What it does:** Stops losing PDFs during bursts and catches files that arrived while the app was closed.
- **How it works:** `FileSystemWatcher` has a default 8 KB internal buffer that overflows under rapid events (the `Error` handler only logs it), and it only sees files created *after* `Start()` — anything downloaded while the app was off is never processed.
- **How to implement:** Set `_watcher.InternalBufferSize = 64 * 1024;` in `StartWatcher`. Then add a one-time reconciliation sweep in `Start()`: enumerate existing `*.pdf` in the folder and feed each through the same `HandleAsync` path (the dedup dictionary and marker check already prevent double-processing).

### 9. Detect and skip encrypted PDFs cleanly

- **What it does:** Turns a cryptic Ghostscript failure on password-protected PDFs into a clear "skipped: encrypted" message.
- **How it works:** PdfPig throws when opening an encrypted PDF; Ghostscript also fails, currently surfacing only a raw stderr snippet.
- **How to implement:** In `ProcessAsync`, wrap a quick `PdfDocument.Open` probe (or catch PdfPig's encryption exception) and, if encrypted, `Logger.Emit("Skipped … : encrypted")` and return before invoking Ghostscript.

### 10. Note: temp file lives next to the source

- **What it does:** Documents a minor edge case rather than changing behavior.
- **How it works:** `PdfCompressor` writes `<src>.gstmp` in the source folder ([PdfCompressor.cs:55](src/PdfAutoCompress.Core/PdfCompressor.cs#L55)); this fails on read-only source folders and could theoretically collide.
- **How to implement:** Low priority. If needed, write to `Path.GetTempPath()` with a GUID name and move cross-volume; the current `.gstmp`-next-to-source choice is deliberate (won't retrigger the `*.pdf` watcher), so only change if read-only folders become a real scenario.

## ✨ Feature ideas

### F1. One-shot compression + Explorer right-click *(highest UX payoff)*

- **What it does:** Lets the user compress a specific PDF on demand — from the terminal (`pdfautocompress file.pdf`) and from a Windows right-click "Compress with PDF Auto-Compress" menu.
- **How it works:** The engine already has a single-file entry point (`PdfCompressor.CompressFileAsync`); it just isn't wired to any on-demand front-end. The CLI currently ignores its `args` and only watches.
- **How to implement:** In `Cli/Program.cs`, if `args` contains a file path, load config, resolve Ghostscript, call `CompressFileAsync` once, print the result, and exit (skip the watcher loop). For the shell menu, add a `[Registry]` entry in the Inno Setup script under `HKCU\Software\Classes\SystemFileAssociations\.pdf\shell\...` pointing at the tray exe (or CLI) with `"%1"`, and have the target handle a single-file argument.

### F2. Lifetime "space saved" stats

- **What it does:** Shows cumulative value ("Saved 1.3 GB across 428 files") in the Settings window.
- **How it works:** Every successful compression already produces a `CompressResult` with original/new bytes; nothing persists the totals.
- **How to implement:** Add `TotalFilesCompressed` and `TotalBytesSaved` to `AppConfig` (or a small separate stats JSON in `%APPDATA%`), increment them in the `Compressed` event handler, and render a read-only line in `SettingsForm.LoadFromConfig`.

### F3. Persistent rolling log file

- **What it does:** Keeps a durable on-disk log so users can attach it to bug reports.
- **How it works:** Tray and CLI only keep the last 200 lines in memory (`CompressionLog`); nothing is written to disk. The Service logs via `ILogger` but with no file sink.
- **How to implement:** In `CompressionLog.Emit`, also append the stamped line to `%APPDATA%\PdfAutoCompress\log.txt`, rolling it when it exceeds e.g. 1 MB (rename to `log.1.txt`). Keep it best-effort (swallow IO errors) so logging never breaks compression.

### F4. Watch multiple folders / include subfolders

- **What it does:** Lets users monitor more than just Downloads, optionally including subfolders.
- **How it works:** `PdfWatcher` creates one `FileSystemWatcher` with `IncludeSubdirectories = false` ([PdfWatcher.cs:57](src/PdfAutoCompress.Core/PdfWatcher.cs#L57)) over a single folder string.
- **How to implement:** Change `AppConfig.WatchFolder` to a list (keep back-compat parsing of the single string), have `Start` spin up one watcher per folder into a list, and add an `IncludeSubdirectories` bool. Update `SettingsForm` to edit a folder list.

### F5. "Compress this folder now" batch action

- **What it does:** Compresses every eligible PDF already sitting in a chosen folder, on demand.
- **How it works:** Reuses `CompressFileAsync` in a loop; independent of the live watcher.
- **How to implement:** Add a tray menu item that opens a folder picker, enumerates `*.pdf`, and runs them through the compressor with the concurrency gate from #2, reporting progress via the existing log/notification path.

### F6. Dark mode + localization (polish)

- **What it does:** Makes the Settings window respect Windows dark mode and prepares strings for translation.
- **How it works:** `SettingsForm` uses default WinForms colors and hardcoded English strings; `InvariantGlobalization=true` is set in `Directory.Build.props`.
- **How to implement:** For dark mode, apply system colors / the Win11 immersive dark-mode API to the form. For localization, move user-facing strings into `.resx` resources. Both are optional polish — do only if you want them.

## 🛠️ Developer experience / project solidity

### 11. Verify the custom PdfPig package resolves for everyone *(check first)*

- **What it does:** Ensures contributors and CI can restore the project at all.
- **How it works:** Core depends on `UglyToad.PdfPig` **`1.7.0-custom-5`** ([Core.csproj:14](src/PdfAutoCompress.Core/PdfAutoCompress.Core.csproj#L14)) — a non-standard "custom" build. If it only lives on your machine's local feed, a fresh clone's `dotnet restore` fails.
- **How to implement:** Run `dotnet restore` in a clean clone (or check on the CI runner). If it fails, either switch to a public PdfPig release (e.g. `0.1.x`/`1.7.x` from nuget.org, adjusting any API differences) or commit a `NuGet.config` that points at the feed hosting the custom build.

### 12. Add quality gates in a repo-root `Directory.Build.props`

- **What it does:** Turns warnings into build failures and enables analyzers across *all* projects including tests.
- **How it works:** The current props file lives under `src/` so it never applies to `tests/`, and it only sets globalization/packaging properties — no analysis or warnings-as-errors.
- **How to implement:** Move `Directory.Build.props` to the repo root, keep the existing properties, and add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<EnableNETAnalyzers>true</EnableNETAnalyzers>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<Deterministic>true</Deterministic>`, and the shared `<TargetFramework>`/`<Nullable>`/`<ImplicitUsings>`/`<Version>` (from #1). Remove the now-duplicated per-project copies. Fix any warnings this surfaces.

### 13. Add an `.editorconfig`

- **What it does:** Enforces one formatting/style standard so diffs stay clean.
- **How it works:** No `.editorconfig` exists today, so `dotnet format` has no shared ruleset.
- **How to implement:** Add a root `.editorconfig` (start from the .NET default and tune naming/`var`/using rules), then add a CI step `dotnet format --verify-no-changes` so unformatted code fails the build.

### 14. Central Package Management

- **What it does:** Versions all NuGet packages in one place.
- **How it works:** Package versions are currently scattered across csproj `<PackageReference Version="…">` lines.
- **How to implement:** Add `Directory.Packages.props` at the root with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and `<PackageVersion>` entries, then strip the `Version=` attributes from the csproj references.

### 15. Broaden CI

- **What it does:** Builds the cross-platform CLI on Linux/macOS and collects coverage.
- **How it works:** `ci.yml` runs Windows-only, build + test, with no coverage or format checks — yet the CLI ships for linux-x64/osx-arm64 that CI never compiles.
- **How to implement:** Add a matrix (`windows-latest`, `ubuntu-latest`, `macos-latest`) that builds/tests `Core` + `Cli` on all three (keep the WinForms tray Windows-only). Add `--collect:"XPlat Code Coverage"` to the test step and upload the result; optionally enforce a coverage threshold.

### 16. Harden the release workflow

- **What it does:** Makes releases verifiable and the installer build reproducible.
- **How it works:** `release.yml` publishes exes but no checksums, and calls Inno Setup at a hardcoded path assuming the runner ships it.
- **How to implement:** Add a step generating `SHA256SUMS.txt` (`Get-FileHash`) over the assets and attach it to the release. Explicitly install/pin Inno Setup (e.g. via `choco install innosetup`) instead of relying on the image. Optionally document code-signing to cut the SmartScreen warning users see (README already mentions it).

### 17. Add `CONTRIBUTING.md`

- **What it does:** Tells contributors how to build, test, and cut a release.
- **How it works:** Issue templates exist, but there's no contributor guide.
- **How to implement:** Write a short `CONTRIBUTING.md` covering prerequisites (.NET 10 SDK, Ghostscript), `dotnet build`/`dotnet test`, the project layout (Core + three front-ends), and the tag-to-release flow.

## 🧪 Testing (widen beyond Core helpers)

### 18. Make `PdfWatcher` runner-injectable

- **What it does:** Lets you test the watcher's real async pipeline end-to-end without invoking Ghostscript.
- **How it works:** `PdfCompressor.CompressFileAsync` accepts a `GhostscriptRunner` delegate (the test seam), but `PdfWatcher.ProcessAsync` calls it without passing one ([PdfWatcher.cs:143](src/PdfAutoCompress.Core/PdfWatcher.cs#L143)), so `HandleAsync`/dedup/`WaitUntilReadyAsync` are untested.
- **How to implement:** Add an optional `PdfCompressor.GhostscriptRunner? runner` to `PdfWatcher` (settable for tests) and forward it into `CompressFileAsync`. Then write a test that starts a watcher on a temp folder, drops a file, and asserts the `Compressed` event fires with expected sizes.

### 19. Make `UpdateChecker.CheckAsync` testable

- **What it does:** Covers the real HTTP + JSON-parsing path, not just version-string parsing.
- **How it works:** `UpdateChecker` uses a `static readonly HttpClient` ([UpdateChecker.cs:14](src/PdfAutoCompress.Core/UpdateChecker.cs#L14)), so tests can't intercept the network call; only `TryParseVersion` is tested.
- **How to implement:** Allow injecting an `HttpMessageHandler` (or an `HttpClient`) into `CheckAsync`/the class, then write tests with a fake handler returning canned GitHub JSON to verify "newer → UpdateInfo", "same → null", and "error → null".

### 20. One real-Ghostscript integration test

- **What it does:** Catches regressions in the actual Ghostscript argument string and the marker round-trip.
- **How it works:** Every current test uses a fake runner, so the real command line in `GhostscriptRunner` and the `IsAlreadyCompressed` marker are never exercised together.
- **How to implement:** Add a test gated on Ghostscript being present (skip via `Assert.Skip`/a fact condition when `ResolveGhostscript` returns empty) that compresses a tiny generated PDF and asserts (a) the output is smaller and (b) `IsAlreadyCompressed` returns true afterward.

### 21. Concurrency-limit test

- **What it does:** Proves the throttle from #2 actually caps parallelism.
- **How it works:** Depends on the injectable runner (#18) and the semaphore (#2).
- **How to implement:** Inject a fake runner that increments a shared counter on entry, records the max seen, and delays; drop N files and assert the observed max never exceeds the configured bound.

## Suggested first slice

If you act on any of this, a tight first PR: **#1** (version) + **#12** (root props/analyzers), **#2/#4/#5** (throttle + the two watcher/Ghostscript fixes), **#3** (comment), **#11** (confirm package source), and **#18 + #20** (watcher seam + one integration test). Verify with `dotnet build -c Release` (warnings-as-errors on) and `dotnet test`, plus a manual run of the tray app dropping several large PDFs at once.
