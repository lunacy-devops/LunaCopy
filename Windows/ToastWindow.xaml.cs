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
            // Hold 700 ms after fade-in
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

        IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        bool hasMonitorInfo = NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0;
        double dpiY = 1.0;

        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformFromDevice.M11;
            dpiY = source.CompositionTarget.TransformFromDevice.M22;
        }

        double cursorDipX = pt.X * dpiX;
        double cursorDipY = pt.Y * dpiY;

        double width = ActualWidth > 0 ? ActualWidth : 120;
        double height = ActualHeight > 0 ? ActualHeight : 36;

        double targetLeft = cursorDipX - (width / 2.0);
        double targetTop = cursorDipY - height - 18.0;

        if (hasMonitorInfo)
        {
            double workLeft = mi.rcWork.Left * dpiX;
            double workTop = mi.rcWork.Top * dpiY;
            double workRight = mi.rcWork.Right * dpiX;
            double workBottom = mi.rcWork.Bottom * dpiY;

            if (targetTop < workTop + 4)
            {
                targetTop = cursorDipY + 22.0;
            }

            if (targetLeft < workLeft + 4) targetLeft = workLeft + 4;
            if (targetLeft + width > workRight - 4) targetLeft = workRight - width - 4;
        }

        Left = targetLeft;
        Top = targetTop;
    }
}
