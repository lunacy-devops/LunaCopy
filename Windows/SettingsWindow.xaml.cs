using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using LunaCopy.Native;
using LunaCopy.Services;

namespace LunaCopy.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        HeaderIconImage.Source = IconGenerator.CreateClipboardCheckDrawingImage(true);
        VersionText.Text = "v" + (typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "dev");
        RefreshAllControls();

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
        NativeMethods.ApplyWin11WindowEffects(hwnd);
    }

    public void RefreshAllControls()
    {
        string languageSetting = ConfigService.Current.Language;
        LanguageButton.Content = languageSetting switch
        {
            Loc.Russian => "RU",
            Loc.English => "EN",
            _ => "AUTO"
        };
        LanguageButton.ToolTip = Loc.Get("Settings.Language.Title") + ": " + Loc.DescribeSetting(languageSetting)
            + "\n" + Loc.Get("Settings.Language.Desc");

        StartupToggle.IsChecked = ConfigService.Current.RunOnStartup;
        SoundToggle.IsChecked = ConfigService.Current.SoundEnabled;
        ToastToggle.IsChecked = ConfigService.Current.ToastEnabled;
        RefreshExclusionList();
    }

    public void RefreshExclusionList()
    {
        var items = ConfigService.GetExcludedProcessesSnapshot()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ExcludedListBox.ItemsSource = items;
        RemoveExclusionButton.IsEnabled = ExcludedListBox.SelectedItem != null;
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        string next = Loc.NextSetting(ConfigService.Current.Language);
        ((App)System.Windows.Application.Current).ChangeLanguage(next);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        bool desired = StartupToggle.IsChecked == true;
        bool ok = ConfigService.SetStartupRegistry(desired);
        if (!ok)
        {
            StartupToggle.IsChecked = !desired;
            System.Windows.MessageBox.Show(this,
                Loc.Get("Settings.StartupError"),
                Loc.Get("Settings.StartupErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SoundToggle_Click(object sender, RoutedEventArgs e)
    {
        ConfigService.Current.SoundEnabled = SoundToggle.IsChecked == true;
        ConfigService.Save();
        if (ConfigService.Current.SoundEnabled)
        {
            SoundService.PlayPop();
        }
    }

    private void ToastToggle_Click(object sender, RoutedEventArgs e)
    {
        ConfigService.Current.ToastEnabled = ToastToggle.IsChecked == true;
        ConfigService.Save();
    }

    private void ExcludedListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RemoveExclusionButton.IsEnabled = ExcludedListBox.SelectedItem != null;
    }

    private void AddExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        var modal = new AddExclusionWindow
        {
            Owner = this
        };

        if (modal.ShowDialog() == true && !string.IsNullOrWhiteSpace(modal.SelectedProcessName))
        {
            ConfigService.AddExclusion(modal.SelectedProcessName);
            RefreshExclusionList();
        }
    }

    private void RemoveExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        if (ExcludedListBox.SelectedItem is string selected)
        {
            ConfigService.RemoveExclusion(selected);
            RefreshExclusionList();
        }
    }
}
