using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using LunaCopy.Native;
using LunaCopy.Services;

namespace LunaCopy.Windows;

public partial class ToastWindow : Window
{
    private CancellationTokenSource? _cts;

    public ToastWindow()
    {
        InitializeComponent();
        Opacity = 0;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    public async void ShowNearCursor()
    {
        if (!ConfigService.Current.ToastEnabled)
            return;

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
        PositionAboveCursor();

        BeginAnimation(OpacityProperty, null);
        Opacity = 0.0;

        // Fade-in 100 ms
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        try
        {
            await Task.Delay(800, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        // Fade-out 200 ms
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            if (!cts.Token.IsCancellationRequested)
            {
                Hide();
            }
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void PositionAboveCursor()
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

        double widthDips = ActualWidth > 0 ? ActualWidth : 120;
        double heightDips = ActualHeight > 0 ? ActualHeight : 36;

        int widthPx = (int)Math.Round(widthDips * scaleX);
        int heightPx = (int)Math.Round(heightDips * scaleY);

        int targetX = pt.X - (widthPx / 2);
        int targetY = pt.Y - heightPx - (int)Math.Round(18 * scaleY);

        if (hasMonitorInfo)
        {
            if (targetY < mi.rcWork.Top + 4)
            {
                targetY = pt.Y + (int)Math.Round(22 * scaleY);
            }

            if (targetX < mi.rcWork.Left + 4) targetX = mi.rcWork.Left + 4;
            if (targetX + widthPx > mi.rcWork.Right - 4) targetX = mi.rcWork.Right - widthPx - 4;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            targetX,
            targetY,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }
}
