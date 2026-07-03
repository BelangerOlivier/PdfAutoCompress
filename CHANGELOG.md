# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-07-03
### Bug fix
- Fix an issue that prevented the user from being prompted to install the application upon launch.

## [1.0.0] - 2026-07-02
### Added
- Automatic PDF compression that watches a folder (Downloads by default) and shrinks new PDFs
  with Ghostscript, replacing the original only when the result is smaller.
- Windows tray app with a settings window, autostart at login, "compressed" notifications,
  pause/resume, and a GitHub update check.
- Windows background service and a cross-platform command-line watcher that share the same engine.
