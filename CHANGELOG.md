# Changelog

All notable changes to the **LunaCopy** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-11-08

### Added
- **I-Beam Cursor Detection**: Fallback copying is strictly restricted to text editing zones (`IDC_IBEAM`), eliminating accidental copies when dragging icons, files, or sliders.
- **Password Protection**: Automated selection reader explicitly ignores password fields via UI Automation (`IsPasswordProperty`).
- **Clipboard Preservation**: Automatic capture and restoration of previous clipboard contents (images, files, rich text) if a synthetic copy attempt yields non-text or duplicate data.
- **Console Process Filter**: Prevents sending `Ctrl+C` to console windows (`cmd.exe`, `powershell.exe`, `pwsh.exe`, etc.) to protect running terminal tasks from termination.
- **PerMonitorV2 High-DPI Manifest**: Embedded `app.manifest` for precise scaling and positioning across multi-monitor setups.
- **DWM Native Rounded Corners & Mica Dark Theme**: Windows 11 aesthetics for Settings and Dialog windows.
- **Foreground WinEvent Hook**: Efficient window tracking via `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` without querying process lists on every click.
- **Fullscreen Detection via `SHQueryUserNotificationState`**: Automatically mutes copying during full-screen games and presentations.
- **Thread-safe & Atomic Configuration**: Saves configuration via atomic temp-file swaps with automatic corrupted file backups.
- **Rotated File Logging**: Automatic diagnostics logging to `LunaCopy.log` with unhandled exception hooks.
- **Audio Silence Tail**: Added 120 ms buffer tail to prevent pop audio clipping on Bluetooth audio devices.
- **Unit Test Suite**: xUnit tests covering process name normalization, configuration parsing, and safety filters.

### Changed
- Rebranded project from AutoCopy to **LunaCopy**.
- Windows 11 Settings window updated with modern Fluent dark styling and custom ToggleSwitches.
- Real-time background process scanning in "Add Exclusion" dialog.
