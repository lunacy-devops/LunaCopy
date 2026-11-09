using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LunaCopy.Native;
using LunaCopy.Services;

namespace LunaCopy.Windows;

public partial class TrayMenuWindow : Window
{
    private readonly Action _onToggleEnabled;
    private readonly Action _onOpenSettings;
    private readonly Action _onExit;
    private readonly Func<string> _getLastActiveProcess;
    private readonly Action _onExclusionChanged;

    private string _currentProcessName = string.Empty;

    public TrayMenuWindow(
        Action onToggleEnabled,
        Action onOpenSettings,
        Action onExit,
        Func<string> getLastActiveProcess,
        Action onExclusionChanged)
    {
        InitializeComponent();
        _onToggleEnabled = onToggleEnabled;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;
        _getLastActiveProcess = getLastActiveProcess;
        _onExclusionChanged = onExclusionChanged;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
    }

    public void ShowAtCursor()
    {
        RefreshMenuState();

        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        UpdateLayout();
        PositionMenuNearCursor();

        if (!IsVisible)
        {
            Show();
        }

        NativeMethods.SetForegroundWindow(hwnd);
        Activate();
        Focus();

        // If Windows refused to give us focus, Deactivated would never fire and the menu
        // would stay on screen forever. Close it in that case.
        var guard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        guard.Tick += (_, _) =>
        {
            guard.Stop();
            if (IsVisible && !IsActive)
            {
                Hide();
            }
        };
        guard.Start();
    }

    private void RefreshMenuState()
    {
        bool isEnabled = ConfigService.Current.IsEnabled;
        EnabledCheckText.Visibility = isEnabled ? Visibility.Visible : Visibility.Hidden;

        _currentProcessName = _getLastActiveProcess();
        if (string.IsNullOrWhiteSpace(_currentProcessName))
        {
            ProcessNameText.Text = "Нет активного процесса";
            ProcessCheckText.Visibility = Visibility.Hidden;
            LastProcessButton.IsEnabled = false;
        }
        else
        {
            LastProcessButton.IsEnabled = true;
            ProcessNameText.Text = _currentProcessName;
            bool isExcluded = ConfigService.IsExcluded(_currentProcessName);
            ProcessCheckText.Visibility = isExcluded ? Visibility.Hidden : Visibility.Visible;
        }
    }

    private void PositionMenuNearCursor()
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        bool hasMonitorInfo = NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        var (scaleX, scaleY) = NativeMethods.GetMonitorDpiScale(hMonitor);
        if (scaleX <= 0) scaleX = 1.0;
        if (scaleY <= 0) scaleY = 1.0;

        double menuWDips = ActualWidth > 0 ? ActualWidth : 236;
        double menuHDips = ActualHeight > 0 ? ActualHeight : 165;

        int menuWPx = (int)Math.Round(menuWDips * scaleX);
        int menuHPx = (int)Math.Round(menuHDips * scaleY);

        int targetX = pt.X - menuWPx + (int)Math.Round(12 * scaleX);
        int targetY = pt.Y - menuHPx - (int)Math.Round(4 * scaleY);

        if (hasMonitorInfo)
        {
            if (targetX < mi.rcWork.Left + 4) targetX = mi.rcWork.Left + 4;
            if (targetX + menuWPx > mi.rcWork.Right - 4) targetX = mi.rcWork.Right - menuWPx - 4;

            if (targetY < mi.rcWork.Top + 4) targetY = pt.Y + (int)Math.Round(8 * scaleY);
            if (targetY + menuHPx > mi.rcWork.Bottom - 4) targetY = mi.rcWork.Bottom - menuHPx - 4;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            targetX,
            targetY,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW);
    }

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        _onToggleEnabled();
        Hide();
    }

    private void ToggleProcessExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentProcessName))
        {
            ConfigService.ToggleExclusion(_currentProcessName);
            _onExclusionChanged();
        }
        Hide();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _onOpenSettings();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _onExit();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Hide();
    }
}
