using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace LunaCopy.Services;

public class AppConfig
{
    public bool IsEnabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = false;
    public bool SoundEnabled { get; set; } = true;
    public bool ToastEnabled { get; set; } = true;
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        "cs2.exe",
        "Photoshop.exe"
    };
}

public static class ConfigService
{
    private const string RegistryRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "LunaCopy";
    private const string LegacyAppName = "AutoCopy";

    private static readonly object SyncLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppConfig Current { get; private set; } = new();

    private static string GetPrimaryConfigPath()
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "config.json");
    }

    private static string GetFallbackConfigPath()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, AppName);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "LunaCopy_config.json");
        }
    }

    public static void Load()
    {
        lock (SyncLock)
        {
            MigrateLegacyData();

            string primary = GetPrimaryConfigPath();
            string fallback = GetFallbackConfigPath();

            string? pathToRead = null;
            if (File.Exists(primary))
            {
                pathToRead = primary;
            }
            else if (File.Exists(fallback))
            {
                pathToRead = fallback;
            }

            if (pathToRead != null)
            {
                try
                {
                    string json = File.ReadAllText(pathToRead);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (loaded != null)
                    {
                        loaded.ExcludedProcesses ??= new List<string>();
                        Current = loaded;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Corrupted config file at '{pathToRead}'. Creating backup.", ex);
                    try
                    {
                        string backupPath = pathToRead + $".corrupted_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                        File.Copy(pathToRead, backupPath, true);
                    }
                    catch
                    {
                        // Ignore backup copy failure
                    }
                    Current = new AppConfig();
                    SaveInternal();
                }
            }
            else
            {
                Current = new AppConfig();
                SaveInternal();
            }

            SyncAndVerifyStartupRegistry();
        }
    }

    private static void MigrateLegacyData()
    {
        try
        {
            // Remove legacy AutoCopy run entry if exists
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true))
            {
                if (key?.GetValue(LegacyAppName) != null)
                {
                    key.DeleteValue(LegacyAppName, false);
                    Logger.Info("Removed legacy 'AutoCopy' registry entry.");
                }
            }

            // Migrate old config if new does not exist
            string newConfig = GetFallbackConfigPath();
            if (!File.Exists(newConfig))
            {
                string oldAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    LegacyAppName,
                    "config.json");

                if (File.Exists(oldAppData))
                {
                    File.Copy(oldAppData, newConfig, false);
                    Logger.Info("Migrated legacy config.json from AutoCopy to LunaCopy.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Legacy migration notice: {ex.Message}");
        }
    }

    public static void Save()
    {
        lock (SyncLock)
        {
            SaveInternal();
        }
    }

    private static void SaveInternal()
    {
        string json = JsonSerializer.Serialize(Current, JsonOptions);
        string primary = GetPrimaryConfigPath();

        if (!TryAtomicWrite(primary, json))
        {
            string fallback = GetFallbackConfigPath();
            TryAtomicWrite(fallback, json);
        }
    }

    private static bool TryAtomicWrite(string targetPath, string content)
    {
        try
        {
            string dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempFile = targetPath + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllText(tempFile, content);

            // Atomic move / replace
            File.Move(tempFile, targetPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsExcluded(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string normalized = NormalizeProcessName(processName);

        lock (SyncLock)
        {
            return Current.ExcludedProcesses.Any(p =>
                string.Equals(NormalizeProcessName(p), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static void ToggleExclusion(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        string normalized = NormalizeProcessName(processName);

        lock (SyncLock)
        {
            var existing = Current.ExcludedProcesses.FirstOrDefault(p =>
                string.Equals(NormalizeProcessName(p), normalized, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                Current.ExcludedProcesses.Remove(existing);
            }
            else
            {
                Current.ExcludedProcesses.Add(normalized);
            }

            SaveInternal();
        }
    }

    public static bool AddExclusion(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string normalized = NormalizeProcessName(processName);

        lock (SyncLock)
        {
            if (Current.ExcludedProcesses.Any(p =>
                string.Equals(NormalizeProcessName(p), normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            Current.ExcludedProcesses.Add(normalized);
            SaveInternal();
            return true;
        }
    }

    public static bool RemoveExclusion(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string normalized = NormalizeProcessName(processName);

        lock (SyncLock)
        {
            int removed = Current.ExcludedProcesses.RemoveAll(p =>
                string.Equals(NormalizeProcessName(p), normalized, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                SaveInternal();
                return true;
            }
            return false;
        }
    }

    public static IReadOnlyList<string> GetExcludedProcessesSnapshot()
    {
        lock (SyncLock)
        {
            return Current.ExcludedProcesses.ToList();
        }
    }

    public static string NormalizeProcessName(string name)
    {
        string trimmed = Path.GetFileName(name.Trim());
        if (!trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed += ".exe";
        }
        return trimmed;
    }

    private static void SyncAndVerifyStartupRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return;

            string? currentRegVal = key.GetValue(AppName) as string;
            string? expectedExePath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(expectedExePath)) return;
            string expectedRegVal = $"\"{expectedExePath}\"";

            if (Current.RunOnStartup)
            {
                if (!string.Equals(currentRegVal, expectedRegVal, StringComparison.OrdinalIgnoreCase))
                {
                    // Path moved or missing: update registry
                    key.SetValue(AppName, expectedRegVal);
                    Logger.Info("Updated startup registry entry with current executable path.");
                }
            }
            else
            {
                if (currentRegVal != null)
                {
                    Current.RunOnStartup = true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Registry sync notice: {ex.Message}");
        }
    }

    public static bool SetStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
            if (key == null) return false;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }

            lock (SyncLock)
            {
                Current.RunOnStartup = enable;
                SaveInternal();
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update Windows startup registry.", ex);
            return false;
        }
    }
}
