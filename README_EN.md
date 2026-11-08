<div align="center">

# 🌙 LunaCopy

**Lightweight Windows 10/11 system utility that automatically copies any mouse-selected text to the clipboard across all applications.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![UI Style](https://img.shields.io/badge/Style-Windows%2011%20Dark-202020)](https://learn.microsoft.com/en-us/windows/apps/design/)
[![License: MIT](https://img.shields.io/badge/License-MIT-22C55E.svg)](LICENSE)

*Читать на [Русском](README.md).*

</div>

---

## ✨ Features

- **Instant Auto-Copy on Selection:**
  - Copies text immediately when you drag-select (>5px) or double-click words.
  - Strict I-Beam cursor checking (`IDC_IBEAM`): only triggers in text areas, preventing accidental copies when dragging files in Explorer, Desktop icons, or window splitters.
  - Ignores single clicks, modifier keys (`Ctrl`, `Shift`, `Alt`, `Win`), and non-client drags (window titles, scrollbars, borders via `WM_NCHITTEST`).
- **Two-Tier Safe Text Extraction:**
  1. **UI Automation (`TextPattern`):** Reads text selection directly from `AutomationElement.FocusedElement` without keyboard emulation.
  2. **Smart Clipboard Fallback:** If the target control does not support UIA, LunaCopy checks `GetClipboardSequenceNumber()` and sends synthetic `Ctrl+C`. If no text was copied, **previous clipboard content (images, files, formatted text) is automatically restored**.
- **🔒 Password Security & Privacy:**
  - Password inputs are ignored automatically using the UIA `IsPassword` attribute.
  - Terminal protection: `Ctrl+C` is never sent to console processes (`cmd.exe`, `powershell.exe`, `pwsh.exe`, `wsl.exe`, etc.) to prevent terminating active tasks.
- **Gaming & Fullscreen Detection:**
  - Automatically suspends copying during full-screen games and presentations via `SHQueryUserNotificationState`.
- **Minimalistic Feedback (Toast + Soft Pop):**
  - Smooth floating dark toast ("Скопировано!") appears right above the cursor without stealing focus (`WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`).
  - Procedural soft pop sound (WAV ~800Hz with trailing silence for Bluetooth audio latency).
- **Windows 11 Dark Aesthetic:**
  - Built with **C# (.NET 8, WPF)** styled after the Windows 11 Fluent dark design language: `#202020` / `#2B2B2B` surfaces, toggle switches, DWM rounded window corners, and `PerMonitorV2` High-DPI support.

---

## 💻 System Requirements

- **OS:** Windows 10 (1809+) or Windows 11 (x64)
- **Runtime:** [**.NET 8.0 Desktop Runtime (x64)**](https://dotnet.microsoft.com/download/dotnet/8.0) (*Windows Desktop Runtime 8.0.x* package).

---

## 🚀 Quick Start

1. Download `LunaCopy.exe` from [Releases](https://github.com/DenisGetman/LunaCopy/releases).
2. Run `LunaCopy.exe` — the blue clipboard icon will appear in your system tray.
3. Select any text in your browser, code editor, or chat app — it's instantly copied to your clipboard!

### Tray Controls
- **Left click tray icon:** Toggle auto-copy on/off.
- **Right click tray icon:** Open menu (toggle status, 1-click exclude active app, settings, exit).

---

## ⚠️ Notes & Limitations

1. **Elevated Windows (UIPI):**
   Windows User Interface Privilege Isolation prevents standard user applications from sending input or inspecting windows running with Administrator privileges. To enable auto-copying inside elevated apps (e.g., Task Manager or Registry Editor), run `LunaCopy.exe` as Administrator.
2. **Diagnostics Log:**
   Diagnostics are logged locally to `%AppData%\LunaCopy\LunaCopy.log` (or next to the executable).

---

## 🛠 Building from Source

```powershell
git clone https://github.com/DenisGetman/LunaCopy.git
cd LunaCopy
dotnet publish LunaCopy.csproj -c Release -r win-x64 -o publish
```

---

## 📄 License

Licensed under the [MIT License](LICENSE).  
Author: **Denis Getman** ([tokia20139@gmail.com](mailto:tokia20139@gmail.com)).
