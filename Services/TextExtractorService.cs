using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using LunaCopy.Native;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;
using DataObject = System.Windows.DataObject;

namespace LunaCopy.Services;

public enum UiaExtractionStatus
{
    NotSupported,
    PasswordIgnored,
    EmptySelection,
    TimedOut,
    Success
}

public sealed class ClipboardBackup
{
    private readonly DataObject? _data;
    private readonly bool _hadContent;

    private ClipboardBackup(DataObject? data, bool hadContent)
    {
        _data = data;
        _hadContent = hadContent;
    }

    public static ClipboardBackup Capture()
    {
        try
        {
            IDataObject? current = Clipboard.GetDataObject();
            if (current == null)
            {
                return new ClipboardBackup(null, false);
            }

            var clone = new DataObject();
            string[] formats = current.GetFormats(false);
            bool hasAny = false;

            foreach (string format in formats)
            {
                try
                {
                    object? obj = current.GetData(format, false);
                    if (obj != null)
                    {
                        clone.SetData(format, obj);
                        hasAny = true;
                    }
                }
                catch
                {
                    // Skip unsupported custom OLE formats
                }
            }

            return new ClipboardBackup(clone, hasAny);
        }
        catch
        {
            return new ClipboardBackup(null, false);
        }
    }

    public void Restore()
    {
        if (!_hadContent || _data == null) return;
        try
        {
            Clipboard.SetDataObject(_data, true);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to restore clipboard backup: {ex.Message}");
        }
    }
}

public static class TextExtractorService
{
    private static string _lastCopiedText = string.Empty;
    private static DateTime _lastCopyTimestamp = DateTime.MinValue;

    private static readonly HashSet<string> ConsoleProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "conhost.exe",
        "windowsterminal.exe",
        "wt.exe",
        "alacritty.exe",
        "wezterm.exe",
        "wezterm-gui.exe",
        "conemu.exe",
        "conemu64.exe",
        "putty.exe",
        "kitty.exe",
        "tabby.exe",
        "hyper.exe",
        "terminus.exe",
        "xshell.exe",
        "mobaxterm.exe",
        "wsl.exe",
        "bash.exe",
        "mintty.exe",
        "openconsole.exe"
    };

    public static bool IsConsoleProcess(string processName)
    {
        string norm = ConfigService.NormalizeProcessName(processName);
        return ConsoleProcesses.Contains(norm);
    }

    public static async Task<bool> TryCopySelectedTextAsync(string currentProcessName)
    {
        // 1. Brief pause to allow target application to finalize its selection
        await Task.Delay(65).ConfigureAwait(false);

        if (NativeMethods.IsModifierPressed()) return false;

        // 2. Query UI Automation with a hard timeout of 200ms
        using var uiaCts = new CancellationTokenSource();
        var uiaTask = Task.Run(() => TryGetSelectedTextViaUia(uiaCts.Token), uiaCts.Token);
        var timeoutTask = Task.Delay(200);

        var completed = await Task.WhenAny(uiaTask, timeoutTask).ConfigureAwait(false);
        UiaExtractionStatus status;
        string? uiaText = null;

        if (completed == uiaTask)
        {
            (status, uiaText) = await uiaTask.ConfigureAwait(false);
        }
        else
        {
            uiaCts.Cancel();
            status = UiaExtractionStatus.TimedOut;
            Logger.Warn("UIA query exceeded 200ms deadline. Aborting UIA extraction.");
        }

        if (status == UiaExtractionStatus.PasswordIgnored)
        {
            Logger.Info("Selection skipped: Password field detected via UI Automation.");
            return false;
        }

        if (status == UiaExtractionStatus.Success && !string.IsNullOrWhiteSpace(uiaText))
        {
            if (IsDuplicateRecentCopy(uiaText))
                return false;

            bool setOk = await SetClipboardTextOnStaAsync(uiaText).ConfigureAwait(false);
            if (setOk)
            {
                RecordSuccessfulCopy(uiaText);
                return true;
            }
        }

        // If target supports UIA TextPattern and definitively reported empty selection,
        // do NOT fall back to blind Ctrl+C.
        // If UIA timed out (e.g. cold start in heavy Chromium/Electron app), allow fallback
        // ONLY if the cursor is firmly verified to be an I-Beam in a non-console window.
        if (status == UiaExtractionStatus.EmptySelection)
        {
            return false;
        }

        // 3. Fallback (Ctrl+C):
        // Allowed ONLY if:
        // - Control does NOT support UIA TextPattern
        // - Process is NOT a command line / console terminal
        // - Cursor is currently an I-Beam (indicating active text area)
        if (IsConsoleProcess(currentProcessName))
        {
            Logger.Info($"Fallback skipped for console/terminal process: {currentProcessName}");
            return false;
        }

        if (!NativeMethods.IsCursorIBeam())
        {
            return false;
        }

        // Capture previous clipboard content to restore if fallback fails or yields non-text
        ClipboardBackup backup = await CaptureClipboardOnStaAsync().ConfigureAwait(false);

        uint seqBefore = NativeMethods.GetClipboardSequenceNumber();
        SendSyntheticCtrlC();

        bool seqChanged = false;
        const int pollStepMs = 15;
        int elapsed = 0;

        while (elapsed < 120)
        {
            await Task.Delay(pollStepMs).ConfigureAwait(false);
            elapsed += pollStepMs;

            uint seqNow = NativeMethods.GetClipboardSequenceNumber();
            if (seqNow != seqBefore)
            {
                seqChanged = true;
                break;
            }
        }

        if (!seqChanged)
        {
            return false;
        }

        string? clipboardText = await GetClipboardTextOnStaAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(clipboardText) || IsDuplicateRecentCopy(clipboardText))
        {
            await RestoreClipboardOnStaAsync(backup).ConfigureAwait(false);
            return false;
        }

        RecordSuccessfulCopy(clipboardText);
        return true;
    }

    private static bool IsDuplicateRecentCopy(string text)
    {
        var now = DateTime.UtcNow;
        if (string.Equals(text, _lastCopiedText, StringComparison.Ordinal) &&
            (now - _lastCopyTimestamp).TotalMilliseconds < 1500)
        {
            return true;
        }
        return false;
    }

    private static void RecordSuccessfulCopy(string text)
    {
        _lastCopiedText = text;
        _lastCopyTimestamp = DateTime.UtcNow;
    }

    private static (UiaExtractionStatus Status, string? Text) TryGetSelectedTextViaUia(CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested)
                return (UiaExtractionStatus.NotSupported, null);

            AutomationElement? focused = AutomationElement.FocusedElement;
            if (focused == null)
                return (UiaExtractionStatus.NotSupported, null);

            // Password field protection
            try
            {
                object isPasswordVal = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
                if (isPasswordVal is bool isPass && isPass)
                {
                    return (UiaExtractionStatus.PasswordIgnored, null);
                }
            }
            catch
            {
                // Property unsupported or element invalidated
            }

            if (ct.IsCancellationRequested)
                return (UiaExtractionStatus.NotSupported, null);

            if (focused.TryGetCurrentPattern(TextPattern.Pattern, out object? patternObj) &&
                patternObj is TextPattern textPattern)
            {
                TextPatternRange[]? ranges = textPattern.GetSelection();
                if (ranges == null || ranges.Length == 0)
                {
                    return (UiaExtractionStatus.EmptySelection, null);
                }

                var sb = new StringBuilder();
                foreach (var range in ranges)
                {
                    if (ct.IsCancellationRequested) break;

                    string? part = range.GetText(65536);
                    if (!string.IsNullOrEmpty(part))
                    {
                        sb.Append(part);
                    }
                }

                string result = sb.ToString();
                if (string.IsNullOrWhiteSpace(result))
                {
                    return (UiaExtractionStatus.EmptySelection, null);
                }

                return (UiaExtractionStatus.Success, result);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"UIA query exception: {ex.Message}");
        }

        return (UiaExtractionStatus.NotSupported, null);
    }

    private static void SendSyntheticCtrlC()
    {
        if (NativeMethods.IsModifierPressed()) return;

        var inputs = new NativeMethods.INPUT[4];

        // Ctrl Down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = NativeMethods.VK_CONTROL;

        // 'C' Down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = NativeMethods.VK_C;

        // 'C' Up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].U.ki.wVk = NativeMethods.VK_C;
        inputs[2].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl Up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].U.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
    }

    private static async Task<ClipboardBackup> CaptureClipboardOnStaAsync()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return ClipboardBackup.Capture();

        try
        {
            return await app.Dispatcher.InvokeAsync(ClipboardBackup.Capture);
        }
        catch
        {
            return ClipboardBackup.Capture();
        }
    }

    private static async Task RestoreClipboardOnStaAsync(ClipboardBackup backup)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        try
        {
            await app.Dispatcher.InvokeAsync(backup.Restore);
        }
        catch
        {
            // Ignore
        }
    }

    private static async Task<string?> GetClipboardTextOnStaAsync()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return null;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                string? text = await app.Dispatcher.InvokeAsync(() =>
                {
                    if (Clipboard.ContainsText())
                    {
                        return Clipboard.GetText();
                    }
                    return null;
                });

                if (text != null)
                    return text;
            }
            catch
            {
                // Clipboard may be temporarily locked
            }
            await Task.Delay(15).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<bool> SetClipboardTextOnStaAsync(string text)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return false;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                bool ok = await app.Dispatcher.InvokeAsync(() =>
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                });
                if (ok) return true;
            }
            catch
            {
                // Clipboard temporarily busy
            }
            await Task.Delay(15).ConfigureAwait(false);
        }

        return false;
    }
}
