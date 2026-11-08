using System.Diagnostics;
using System.Runtime.InteropServices;
using LunaCopy.Native;

namespace LunaCopy.Services;

public sealed class MouseHookService : IDisposable
{
    private Thread? _hookThread;
    private uint _hookThreadId;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private IntPtr _winEventHookId = IntPtr.Zero;

    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly NativeMethods.WinEventDelegate _winEventProc;

    private readonly ManualResetEventSlim _initEvent = new(false);
    private readonly object _stateLock = new();

    private bool _isLeftButtonDown;
    private NativeMethods.POINT _downPoint;
    private bool _startedWithModifier;
    private bool _isDoubleClickSequence;

    private uint _lastClickUpTime;
    private NativeMethods.POINT _lastClickUpPoint;

    private CancellationTokenSource? _copyCts;
    private readonly System.Threading.Timer _watchdogTimer;
    private DateTime _lastHookHeartbeat = DateTime.UtcNow;

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
        lock (_stateLock)
        {
            if (_hookThread != null && _hookThread.IsAlive) return;

            _initEvent.Reset();
            _hookThread = new Thread(HookThreadProc)
            {
                IsBackground = true,
                Name = "LunaCopy_MouseHookThread"
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            // Wait up to 2 seconds for hook thread to initialize
            _initEvent.Wait(2000);

            InstallWinEventHook();
            UpdateForegroundProcessFromHandle(NativeMethods.GetForegroundWindow());

            _watchdogTimer.Change(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }
    }

    private void HookThreadProc()
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();

        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule? curModule = curProcess.MainModule)
        {
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
            _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);

            if (_mouseHookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Logger.Error($"Failed to install SetWindowsHookEx on dedicated thread. Win32Error: {err}");
            }
            else
            {
                Logger.Info("Low-level mouse hook installed on dedicated background thread.");
            }
        }

        _initEvent.Set();

        // Dedicated message loop for WH_MOUSE_LL
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
            Logger.Info("Mouse hook uninstalled on dedicated thread exit.");
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
            // Process may have terminated or access denied
        }
    }

    private void WatchdogCheck(object? state)
    {
        try
        {
            lock (_stateLock)
            {
                // If hook thread died or hook handle was invalidated, restart hook thread
                if (_hookThread == null || !_hookThread.IsAlive || _mouseHookId == IntPtr.Zero)
                {
                    Logger.Warn("Watchdog detected inactive hook thread. Restarting...");
                    RestartHookThread();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Watchdog check error", ex);
        }
    }

    private void RestartHookThread()
    {
        StopHookThread();
        _initEvent.Reset();
        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "LunaCopy_MouseHookThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        _initEvent.Wait(2000);
    }

    private void StopHookThread()
    {
        if (_hookThreadId != 0)
        {
            NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThreadId = 0;
        }

        if (_hookThread != null && _hookThread.IsAlive)
        {
            _hookThread.Join(1000);
            _hookThread = null;
        }
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            _watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);

            StopHookThread();

            if (_winEventHookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_winEventHookId);
                _winEventHookId = IntPtr.Zero;
            }
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            _lastHookHeartbeat = DateTime.UtcNow;

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
                if (NativeMethods.IsNonClientDragArea(downPoint) || NativeMethods.IsNonClientDragArea(upPoint))
                    return;

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
        _initEvent.Dispose();
    }
}
