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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
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
                "Не удалось изменить параметр автозапуска в реестре Windows. Возможно, требуются права администратора.",
                "LunaCopy — Ошибка автозапуска",
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
