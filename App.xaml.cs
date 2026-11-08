using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using LunaCopy.Services;
using LunaCopy.Windows;
using Application = System.Windows.Application;

namespace LunaCopy;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private NotifyIcon? _notifyIcon;
    private Icon? _activeIcon;
    private Icon? _inactiveIcon;

    private MouseHookService? _mouseHook;
    private ToastWindow? _toastWindow;
    private TrayMenuWindow? _trayMenuWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SetupExceptionHandling();

        _singleInstanceMutex = new Mutex(true, @"Local\LunaCopy_SingleInstance_Mutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            Logger.Warn("Another instance of LunaCopy is already running. Exiting.");
            Shutdown();
            return;
        }

        Logger.Info("LunaCopy starting up...");

        ConfigService.Load();

        _activeIcon = IconGenerator.CreateTrayIcon(true);
        _inactiveIcon = IconGenerator.CreateTrayIcon(false);

        _toastWindow = new ToastWindow();

        _mouseHook = new MouseHookService();
        _mouseHook.TextCopied += OnTextCopied;
        _mouseHook.Start();

        _trayMenuWindow = new TrayMenuWindow(
            onToggleEnabled: ToggleActiveState,
            onOpenSettings: OpenSettingsWindow,
            onExit: ExitApplication,
            getLastActiveProcess: () => _mouseHook?.LastActiveProcessName ?? string.Empty,
            onExclusionChanged: () => _settingsWindow?.RefreshExclusionList());

        _notifyIcon = new NotifyIcon
        {
            Visible = true
        };
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        UpdateTrayVisuals();

        Logger.Info("LunaCopy initialized successfully.");
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error("AppDomain UnhandledException", ex);
            }
        };

        DispatcherUnhandledException += (s, e) =>
        {
            Logger.Error("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // Prevent application crash when recoverable
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Logger.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleActiveState();
        }
        else if (e.Button == MouseButtons.Right)
        {
            _trayMenuWindow?.ShowAtCursor();
        }
    }

    private void ToggleActiveState()
    {
        ConfigService.Current.IsEnabled = !ConfigService.Current.IsEnabled;
        ConfigService.Save();
        UpdateTrayVisuals();
    }

    private void UpdateTrayVisuals()
    {
        if (_notifyIcon == null) return;

        bool active = ConfigService.Current.IsEnabled;
        _notifyIcon.Icon = active ? _activeIcon : _inactiveIcon;
        _notifyIcon.Text = active ? "LunaCopy — Активен" : "LunaCopy — Отключен";
    }

    private void OnTextCopied()
    {
        SoundService.PlayPop();

        Dispatcher.BeginInvoke(() =>
        {
            _toastWindow?.ShowNearCursor();
        });
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.RefreshAllControls();
        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        Logger.Info("LunaCopy exiting by user request.");
        Cleanup();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private void Cleanup()
    {
        if (_mouseHook != null)
        {
            _mouseHook.TextCopied -= OnTextCopied;
            _mouseHook.Dispose();
            _mouseHook = null;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _activeIcon?.Dispose();
        _activeIcon = null;

        _inactiveIcon?.Dispose();
        _inactiveIcon = null;

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
    }
}
