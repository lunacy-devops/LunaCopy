using System.Runtime.InteropServices;
using System.Text;

namespace LunaCopy.Native;

public static class NativeMethods
{
    // Hooks
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const uint WM_QUIT = 0x0012;

    // Virtual Keys
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const ushort VK_C = 0x43;

    // Window Styles & Messages
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    public const uint WM_NCHITTEST = 0x0084;
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTSYSMENU = 3;
    public const int HTGROWBOX = 4;
    public const int HTMENU = 5;
    public const int HTHSCROLL = 6;
    public const int HTVSCROLL = 7;
    public const int HTMINBUTTON = 8;
    public const int HTMAXBUTTON = 9;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;
    public const int HTBORDER = 18;
    public const int HTCLOSE = 20;

    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // WinEvents for Foreground Tracking
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    // Cursors
    public const int IDC_IBEAM = 32513;
    public const uint CURSOR_SHOWING = 0x00000001;

    // Monitor & DPI
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    public const int MDT_EFFECTIVE_DPI = 0;

    // DWM
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    // Shell User Notification States
    public enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,
        QUNS_BUSY = 2,
        QUNS_RUNNING_D_3D_FULL_SCREEN = 3,
        QUNS_PRESENTATION_MODE = 4,
        QUNS_ACCEPTS_NOTIFICATIONS = 5,
        QUNS_QUIET_TIME = 6,
        QUNS_APP = 7
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf<INPUT>();
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // P/Invoke Declarations
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(out CURSORINFO pci);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("shell32.dll")]
    public static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Cached system I-Beam cursor handle
    private static readonly IntPtr SystemIBeamCursor = LoadCursor(IntPtr.Zero, IDC_IBEAM);

    public static (double ScaleX, double ScaleY) GetMonitorDpiScale(IntPtr hMonitor)
    {
        try
        {
            if (hMonitor != IntPtr.Zero && GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch
        {
            // shcore.dll may not be available on older versions
        }
        return (1.0, 1.0);
    }

    public static bool IsModifierPressed()
    {
        return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
               (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
    }

    public static bool IsCursorIBeam()
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (GetCursorInfo(out ci))
        {
            if ((ci.flags & CURSOR_SHOWING) != 0)
            {
                if (ci.hCursor == SystemIBeamCursor)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool IsSystemTrayOrDesktopWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        var sb = new StringBuilder(256);
        if (GetClassName(hWnd, sb, sb.Capacity) > 0)
        {
            string className = sb.ToString();
            if (className is "Shell_TrayWnd" or
                "NotifyIconOverflowWindow" or
                "Shell_SecondaryTrayWnd" or
                "Progman" or
                "WorkerW")
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsInFullscreenOrBusyState()
    {
        try
        {
            int hr = SHQueryUserNotificationState(out var state);
            if (hr == 0) // S_OK
            {
                return state is QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D_3D_FULL_SCREEN or
                                QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE or
                                QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY;
            }
        }
        catch
        {
            // Ignore
        }
        return false;
    }

    public static bool IsNonClientDragArea(POINT pt)
    {
        IntPtr hWnd = WindowFromPoint(pt);
        if (hWnd == IntPtr.Zero) return false;

        IntPtr lParam = (IntPtr)((pt.Y << 16) | (pt.X & 0xFFFF));
        IntPtr res = SendMessageTimeout(hWnd, WM_NCHITTEST, IntPtr.Zero, lParam, SMTO_ABORTIFHUNG, 30, out IntPtr hitResult);
        if (res == IntPtr.Zero) return false;

        long hit = hitResult.ToInt64();
        return hit is HTCAPTION or
                      HTVSCROLL or
                      HTHSCROLL or
                      HTSYSMENU or
                      HTMENU or
                      HTMINBUTTON or
                      HTMAXBUTTON or
                      HTCLOSE or
                      HTLEFT or
                      HTRIGHT or
                      HTTOP or
                      HTTOPLEFT or
                      HTTOPRIGHT or
                      HTBOTTOM or
                      HTBOTTOMLEFT or
                      HTBOTTOMRIGHT or
                      HTBORDER or
                      HTGROWBOX;
    }

    public static void ApplyWin11WindowEffects(IntPtr hwnd, bool smallCorners = false)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int cornerPref = smallCorners ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
        }
        catch
        {
            // Ignore
        }
    }
}
