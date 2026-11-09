using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using LunaCopy.Native;
using LunaCopy.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LunaCopy.Windows;

public partial class AddExclusionWindow : Window
{
    public string? SelectedProcessName { get; private set; }
    private readonly DispatcherTimer _refreshTimer;
    private bool _isLoading;

    public AddExclusionWindow()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshProcessesAsync();

        Loaded += async (_, _) =>
        {
            ProcessNameTextBox.Focus();
            await RefreshProcessesAsync();
            _refreshTimer.Start();
        };

        Unloaded += (_, _) => _refreshTimer.Stop();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ApplyWin11WindowEffects(hwnd, smallCorners: true);
    }

    private async Task RefreshProcessesAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            var running = await Task.Run(() =>
            {
                int currentPid = Environment.ProcessId;
                var uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (proc.Id == currentPid) continue;
                        if (proc.MainWindowHandle == IntPtr.Zero) continue;
                        if (string.IsNullOrWhiteSpace(proc.MainWindowTitle)) continue;

                        string name = ConfigService.NormalizeProcessName(proc.ProcessName);
                        if (!string.Equals(name, "LunaCopy.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            uniqueNames.Add(name);
                        }
                    }
                    catch
                    {
                        // Inaccessible process
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                return uniqueNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            });

            string? selected = RunningProcessesListBox.SelectedItem as string;

            // Only update ItemsSource if list actually changed
            var currentItems = RunningProcessesListBox.ItemsSource as List<string>;
            bool changed = currentItems == null || !currentItems.SequenceEqual(running, StringComparer.OrdinalIgnoreCase);

            if (changed)
            {
                RunningProcessesListBox.ItemsSource = running;
                if (!string.IsNullOrEmpty(selected) && running.Contains(selected, StringComparer.OrdinalIgnoreCase))
                {
                    RunningProcessesListBox.SelectedItem = selected;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to refresh running processes: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.Get("Add.BrowseTitle"),
            Filter = Loc.Get("Add.BrowseFilter"),
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == true)
        {
            string fileName = Path.GetFileName(dlg.FileName);
            ProcessNameTextBox.Text = fileName;
        }
    }

    private void RunningProcessesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunningProcessesListBox.SelectedItem is string selected)
        {
            ProcessNameTextBox.Text = selected;
        }
    }

    private void RunningProcessesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RunningProcessesListBox.SelectedItem is string selected)
        {
            ProcessNameTextBox.Text = selected;
            ConfirmAndClose();
        }
    }

    private void ProcessNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmAndClose();
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ConfirmAndClose();
    }

    private void ConfirmAndClose()
    {
        string raw = ProcessNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            ProcessNameTextBox.Focus();
            return;
        }

        SelectedProcessName = ConfigService.NormalizeProcessName(raw);
        DialogResult = true;
        Close();
    }
}
