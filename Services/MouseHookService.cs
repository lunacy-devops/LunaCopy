using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LunaCopy.Native;

namespace LunaCopy.Services;

public sealed class MouseHookService : IDisposable
{
    private IntPtr _mouseHookId = IntPtr.Zero;
    private IntPtr _winEventHookId = IntPtr.Zero;

    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly NativeMethods.WinEventDelegate _winEventProc;

    private bool _isLeftButtonDown;
    private NativeMethods.POINT _downPoint;
    private bool _startedWithModifier;
    private bool _isDoubleClickSequence;

    private uint _lastClickUpTime;
    private NativeMethods.POINT _lastClickUpPoint;

    private CancellationTokenSource? _copyCts;
    private readonly System.Threading.Timer _watchdogTimer;

    private string _cachedForegroundProcess = string.Empty;
    private readonly object _processLock = new();

    public string LastActiveProcessName
    {
        get
        {
            lock (_processLock)
            {
                return _cachedForegroundProcess;
            }
        }
        private set
        {
            lock (_processLock)
            {
                _cachedForegroundProcess = value;
            }
        }
    }

    public event Action? TextCopied;

    public MouseHookService()
    {
        _mouseProc = MouseHookCallback;
        _winEventProc = WinEventCallback;
        _watchdogTimer = new System.Threading.Timer(WatchdogCheck, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        InstallMouseHook();
        InstallWinEventHook();
        UpdateForegroundProcessFromHandle(NativeMethods.GetForegroundWindow());

        // Start watchdog every 10 seconds
        _watchdogTimer.Change(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private void InstallMouseHook()
    {
        if (_mouseHookId != IntPtr.Zero) return;

        using Process curProcess = Process.GetCurrentProcess();
        using ProcessModule? curModule = curProcess.MainModule;
        IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);

        _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        if (_mouseHookId == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Logger.Error($"Failed to install SetWindowsHookEx (WH_MOUSE_LL). Win32Error: {err}");
        }
        else
        {
            Logger.Info("Mouse hook installed successfully.");
        }
    }

    private void InstallWinEventHook()
    {
        if (_winEventHookId != IntPtr.Zero) return;

        _winEventHookId = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_winEventHookId == IntPtr.Zero)
        {
            Logger.Warn("Failed to install SetWinEventHook for EVENT_SYSTEM_FOREGROUND.");
        }
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero)
        {
            UpdateForegroundProcessFromHandle(hwnd);
        }
    }

    private void UpdateForegroundProcessFromHandle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        try
        {
            // Ignore system tray, start menu, desktop, and notifications
            if (NativeMethods.IsSystemTrayOrDesktopWindow(hWnd))
                return;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return;

            int currentPid = Environment.ProcessId;
            if (pid == (uint)currentPid) return;

            using var proc = Process.GetProcessById((int)pid);
            string procName = ConfigService.NormalizeProcessName(proc.ProcessName);

            if (string.Equals(procName, "LunaCopy.exe", StringComparison.OrdinalIgnoreCase))
                return;

            LastActiveProcessName = procName;
        }
        catch
        {
            // Process might have exited or permission denied
        }
    }

    private void WatchdogCheck(object? state)
    {
        try
        {
            if (_mouseHookId == IntPtr.Zero)
            {
                Logger.Warn("Watchdog: Mouse hook lost, attempting reinstall...");
                InstallMouseHook();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Watchdog check error", ex);
        }
    }

    public void Stop()
    {
        _watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
            Logger.Info("Mouse hook removed.");
        }

        if (_winEventHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHookId);
            _winEventHookId = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_LBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                OnLeftButtonDown(hookStruct);
            }
            else if (msg == NativeMethods.WM_LBUTTONUP)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                OnLeftButtonUp(hookStruct);
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private void OnLeftButtonDown(NativeMethods.MSLLHOOKSTRUCT hookStruct)
    {
        _isLeftButtonDown = true;
        _downPoint = hookStruct.pt;
        _startedWithModifier = NativeMethods.IsModifierPressed();

        uint doubleClickTime = NativeMethods.GetDoubleClickTime();
        if (doubleClickTime == 0) doubleClickTime = 500;

        uint timeDiff = unchecked(hookStruct.time - _lastClickUpTime);
        double distFromLastClick = GetDistance(hookStruct.pt, _lastClickUpPoint);

        _isDoubleClickSequence = timeDiff <= doubleClickTime && distFromLastClick <= 5.0;
    }

    private void OnLeftButtonUp(NativeMethods.MSLLHOOKSTRUCT hookStruct)
    {
        if (!_isLeftButtonDown) return;
        _isLeftButtonDown = false;

        var upPoint = hookStruct.pt;
        var downPoint = _downPoint;
        double dragDistance = GetDistance(downPoint, upPoint);
        bool isDragSelect = dragDistance > 5.0;
        bool isDoubleClick = _isDoubleClickSequence && dragDistance <= 5.0;

        _lastClickUpTime = hookStruct.time;
        _lastClickUpPoint = upPoint;

        if (!ConfigService.Current.IsEnabled)
            return;

        if (_startedWithModifier || NativeMethods.IsModifierPressed())
            return;

        if (!isDragSelect && !isDoubleClick)
            return;

        // Cancel previous pending copy
        _copyCts?.Cancel();
        var cts = new CancellationTokenSource();
        _copyCts = cts;

        Task.Run(async () =>
        {
            try
            {
                // Check if dragging window caption, borders, or scrollbars
                if (NativeMethods.IsNonClientDragArea(downPoint) || NativeMethods.IsNonClientDragArea(upPoint))
                    return;

                // Check fullscreen games / presentation
                if (NativeMethods.IsInFullscreenOrBusyState())
                    return;

                IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
                if (foregroundHwnd == IntPtr.Zero || NativeMethods.IsSystemTrayOrDesktopWindow(foregroundHwnd))
                    return;

                string currentProcess = LastActiveProcessName;
                if (string.IsNullOrEmpty(currentProcess))
                {
                    UpdateForegroundProcessFromHandle(foregroundHwnd);
                    currentProcess = LastActiveProcessName;
                }

                if (string.IsNullOrEmpty(currentProcess) || ConfigService.IsExcluded(currentProcess))
                    return;

                if (cts.Token.IsCancellationRequested)
                    return;

                bool copied = await TextExtractorService.TryCopySelectedTextAsync(currentProcess).ConfigureAwait(false);
                if (copied && !cts.Token.IsCancellationRequested)
                {
                    TextCopied?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during copy workflow", ex);
            }
        });
    }

    private static double GetDistance(NativeMethods.POINT a, NativeMethods.POINT b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose()
    {
        _copyCts?.Cancel();
        Stop();
        _watchdogTimer.Dispose();
    }
}
