using System.IO;

namespace LunaCopy.Services;

public static class Logger
{
    private static readonly object LockObj = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                string baseDir = AppContext.BaseDirectory;
                string candidate = Path.Combine(baseDir, "LunaCopy.log");
                try
                {
                    // Test if writable
                    using (var fs = File.Open(candidate, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                    }
                    _logFilePath = candidate;
                }
                catch
                {
                    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LunaCopy");
                    Directory.CreateDirectory(appData);
                    _logFilePath = Path.Combine(appData, "LunaCopy.log");
                }
            }
            return _logFilePath;
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        string text = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        Write("ERROR", text);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (LockObj)
            {
                string path = LogFilePath;
                // Rotate if > 2MB
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > 2 * 1024 * 1024)
                    {
                        string old = path + ".old";
                        File.Delete(old);
                        File.Move(path, old);
                    }
                }

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logging should never crash the application
        }
    }
}
