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

        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
        PositionMenuNearCursor();

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetForegroundWindow(hwnd);
        Activate();
        Focus();
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

        IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        bool hasMonitorInfo = NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        var (scaleX, scaleY) = NativeMethods.GetMonitorDpiScale(hMonitor);
        if (scaleX <= 0) scaleX = 1.0;
        if (scaleY <= 0) scaleY = 1.0;

        double cursorX = pt.X / scaleX;
        double cursorY = pt.Y / scaleY;

        double menuW = ActualWidth > 0 ? ActualWidth : 236;
        double menuH = ActualHeight > 0 ? ActualHeight : 165;

        double workLeft = SystemParameters.WorkArea.Left;
        double workTop = SystemParameters.WorkArea.Top;
        double workRight = SystemParameters.WorkArea.Right;
        double workBottom = SystemParameters.WorkArea.Bottom;

        if (hasMonitorInfo)
        {
            workLeft = mi.rcWork.Left / scaleX;
            workTop = mi.rcWork.Top / scaleY;
            workRight = mi.rcWork.Right / scaleX;
            workBottom = mi.rcWork.Bottom / scaleY;
        }

        double left = cursorX - menuW + 12;
        double top = cursorY - menuH - 4;

        if (left < workLeft + 4) left = workLeft + 4;
        if (left + menuW > workRight - 4) left = workRight - menuW - 4;

        if (top < workTop + 4) top = cursorY + 8;
        if (top + menuH > workBottom - 4) top = workBottom - menuH - 4;

        Left = left;
        Top = top;
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
