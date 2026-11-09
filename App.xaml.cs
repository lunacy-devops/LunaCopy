using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Threading;
using LunaCopy.Services;
using LunaCopy.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

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
            Logger.Warn("Another instance of LunaCopy is already running.");
            MessageBox.Show(
                Loc.Get("App.AlreadyRunning"),
                "LunaCopy",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Check if legacy AutoCopy process is still running without claiming ownership of its mutex
        if (Mutex.TryOpenExisting(@"Local\AutoCopy_SingleInstance_Mutex", out var legacyMutex))
        {
            legacyMutex.Dispose();
            Logger.Warn("A running instance of legacy AutoCopy was detected.");
            MessageBox.Show(
                Loc.Get("App.LegacyRunning"),
                Loc.Get("App.LegacyTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        Logger.Info("LunaCopy starting up...");

        // Background warmup of UI Automation subsystem and client focus for instant cold-start response
        Task.Run(() =>
        {
            try
            {
                _ = AutomationElement.RootElement;
                _ = AutomationElement.FocusedElement;
                Logger.Info("UI Automation subsystem and focus warmed up successfully.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"UIA warmup notice: {ex.Message}");
            }
        });

        ConfigService.Load();

        _activeIcon = IconGenerator.CreateTrayIcon(true);
        _inactiveIcon = IconGenerator.CreateTrayIcon(false);

        _toastWindow = new ToastWindow();

        _mouseHook = new MouseHookService();
        _mouseHook.TextCopied += OnTextCopied;
        _mouseHook.Start();

        _trayMenuWindow = CreateTrayMenu();

        _notifyIcon = new NotifyIcon
        {
            Visible = true
        };
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        UpdateTrayVisuals();

        Logger.Info("LunaCopy initialized successfully.");
    }

    private TrayMenuWindow CreateTrayMenu() => new(
        onToggleEnabled: ToggleActiveState,
        onOpenSettings: OpenSettingsWindow,
        onExit: ExitApplication,
        getLastActiveProcess: () => _mouseHook?.LastActiveProcessName ?? string.Empty,
        onExclusionChanged: () => _settingsWindow?.RefreshExclusionList());

    /// <summary>Applies a new UI language ("auto", "ru" or "en") and rebuilds all localized windows.</summary>
    public void ChangeLanguage(string setting)
    {
        ConfigService.Current.Language = setting;
        ConfigService.Save();

        _trayMenuWindow?.Close();
        _trayMenuWindow = CreateTrayMenu();

        _toastWindow?.Close();
        _toastWindow = new ToastWindow();

        UpdateTrayVisuals();

        if (_settingsWindow != null)
        {
            var old = _settingsWindow;
            double left = old.Left, top = old.Top;
            _settingsWindow = null;
            old.Close();

            _settingsWindow = new SettingsWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = left,
                Top = top
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
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
            e.Handled = true;
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
        _notifyIcon.Text = Loc.Get(active ? "App.TooltipActive" : "App.TooltipDisabled");
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
