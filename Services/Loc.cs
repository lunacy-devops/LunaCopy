using System.Globalization;
using System.Windows.Markup;

namespace LunaCopy.Services;

/// <summary>
/// Minimal in-memory localization. Language comes from <c>AppConfig.Language</c>:
/// "auto" (follow the Windows UI language), "ru" or "en".
/// </summary>
public static class Loc
{
    public const string Auto = "auto";
    public const string Russian = "ru";
    public const string English = "en";

    internal static readonly IReadOnlyDictionary<string, string> Ru = new Dictionary<string, string>
    {
        ["App.TooltipActive"] = "LunaCopy — Активен",
        ["App.TooltipDisabled"] = "LunaCopy — Отключен",
        ["App.AlreadyRunning"] = "LunaCopy уже запущен и работает в системном трее возле часов.",
        ["App.LegacyRunning"] = "Обнаружен работающий процесс предыдущей версии (AutoCopy).\nПожалуйста, закройте его в системном трее перед запуском LunaCopy.",
        ["App.LegacyTitle"] = "LunaCopy — Конфликт версий",

        ["Toast.Copied"] = "Скопировано!",

        ["Tray.Enabled"] = "Включено",
        ["Tray.Settings"] = "Настройки",
        ["Tray.Exit"] = "Выход",
        ["Tray.NoProcess"] = "Нет активного процесса",

        ["Toggle.On"] = "Вкл.",
        ["Toggle.Off"] = "Откл.",

        ["Settings.WindowTitle"] = "LunaCopy — Параметры",
        ["Settings.Heading"] = "Параметры",
        ["Settings.Subtitle"] = "Автоматическое копирование выделенного текста в Windows 10/11",
        ["Settings.General"] = "Основные",
        ["Settings.Startup.Title"] = "Запуск вместе с Windows",
        ["Settings.Startup.Desc"] = "Автоматический запуск программы при старте",
        ["Settings.Sound.Title"] = "Звуковой сигнал",
        ["Settings.Sound.Desc"] = "Воспроизведение звука при завершении копирования",
        ["Settings.Toast.Title"] = "Всплывающая подсказка",
        ["Settings.Toast.Desc"] = "Отображение уведомления «Скопировано!» возле курсора",
        ["Settings.Language.Title"] = "Язык интерфейса",
        ["Settings.Language.Desc"] = "Нажмите, чтобы переключить: Авто → Русский → English",
        ["Settings.Language.Auto"] = "Авто",
        ["Settings.Language.Ru"] = "Русский",
        ["Settings.Language.En"] = "English",
        ["Settings.Exclusions"] = "Исключения",
        ["Settings.Excluded.Title"] = "Исключенные приложения",
        ["Settings.Excluded.Desc"] = "Процессы, в которых LunaCopy не перехватывает выделение",
        ["Settings.Add"] = "Добавить",
        ["Settings.Remove"] = "Удалить",
        ["Settings.StartupError"] = "Не удалось изменить параметр автозапуска в реестре Windows. Возможно, требуются права администратора.",
        ["Settings.StartupErrorTitle"] = "LunaCopy — Ошибка автозапуска",

        ["Add.WindowTitle"] = "Добавить исключение",
        ["Add.Prompt"] = "Имя процесса (например: notepad)",
        ["Add.Browse"] = "ОБЗОР",
        ["Add.Running"] = "Или выберите из запущенных приложений:",
        ["Add.Confirm"] = "ДОБАВИТЬ",
        ["Add.BrowseTitle"] = "Выберите исполняемый файл (.exe)",
        ["Add.BrowseFilter"] = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
    };

    internal static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["App.TooltipActive"] = "LunaCopy — Active",
        ["App.TooltipDisabled"] = "LunaCopy — Disabled",
        ["App.AlreadyRunning"] = "LunaCopy is already running in the system tray near the clock.",
        ["App.LegacyRunning"] = "A running process of the previous version (AutoCopy) was detected.\nPlease close it in the system tray before starting LunaCopy.",
        ["App.LegacyTitle"] = "LunaCopy — Version conflict",

        ["Toast.Copied"] = "Copied!",

        ["Tray.Enabled"] = "Enabled",
        ["Tray.Settings"] = "Settings",
        ["Tray.Exit"] = "Exit",
        ["Tray.NoProcess"] = "No active process",

        ["Toggle.On"] = "On",
        ["Toggle.Off"] = "Off",

        ["Settings.WindowTitle"] = "LunaCopy — Settings",
        ["Settings.Heading"] = "Settings",
        ["Settings.Subtitle"] = "Automatically copy selected text in Windows 10/11",
        ["Settings.General"] = "General",
        ["Settings.Startup.Title"] = "Start with Windows",
        ["Settings.Startup.Desc"] = "Launch the app automatically at sign-in",
        ["Settings.Sound.Title"] = "Sound",
        ["Settings.Sound.Desc"] = "Play a sound when text has been copied",
        ["Settings.Toast.Title"] = "Pop-up notification",
        ["Settings.Toast.Desc"] = "Show a “Copied!” notice next to the cursor",
        ["Settings.Language.Title"] = "Interface language",
        ["Settings.Language.Desc"] = "Click to switch: Auto → Русский → English",
        ["Settings.Language.Auto"] = "Auto",
        ["Settings.Language.Ru"] = "Русский",
        ["Settings.Language.En"] = "English",
        ["Settings.Exclusions"] = "Exclusions",
        ["Settings.Excluded.Title"] = "Excluded applications",
        ["Settings.Excluded.Desc"] = "Processes where LunaCopy does not capture selections",
        ["Settings.Add"] = "Add",
        ["Settings.Remove"] = "Remove",
        ["Settings.StartupError"] = "Could not change the startup setting in the Windows registry. Administrator rights may be required.",
        ["Settings.StartupErrorTitle"] = "LunaCopy — Startup error",

        ["Add.WindowTitle"] = "Add exclusion",
        ["Add.Prompt"] = "Process name (e.g. notepad)",
        ["Add.Browse"] = "BROWSE",
        ["Add.Running"] = "Or pick from running applications:",
        ["Add.Confirm"] = "ADD",
        ["Add.BrowseTitle"] = "Select an executable (.exe)",
        ["Add.BrowseFilter"] = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
    };

    /// <summary>Resolves the effective language code ("ru" or "en") for a configured value.</summary>
    public static string Resolve(string? configured, CultureInfo? uiCulture = null)
    {
        if (string.Equals(configured, Russian, StringComparison.OrdinalIgnoreCase)) return Russian;
        if (string.Equals(configured, English, StringComparison.OrdinalIgnoreCase)) return English;

        string two = (uiCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        return string.Equals(two, Russian, StringComparison.OrdinalIgnoreCase) ? Russian : English;
    }

    public static string CurrentLanguage => Resolve(ConfigService.Current.Language);

    public static string Get(string key)
    {
        var table = CurrentLanguage == Russian ? Ru : En;
        if (table.TryGetValue(key, out var value)) return value;
        return Ru.TryGetValue(key, out var fallback) ? fallback : key;
    }

    /// <summary>Next value in the Auto → ru → en cycle.</summary>
    public static string NextSetting(string? configured)
    {
        if (string.Equals(configured, Russian, StringComparison.OrdinalIgnoreCase)) return English;
        if (string.Equals(configured, English, StringComparison.OrdinalIgnoreCase)) return Auto;
        return Russian;
    }

    public static string DescribeSetting(string? configured)
    {
        if (string.Equals(configured, Russian, StringComparison.OrdinalIgnoreCase)) return Get("Settings.Language.Ru");
        if (string.Equals(configured, English, StringComparison.OrdinalIgnoreCase)) return Get("Settings.Language.En");
        return Get("Settings.Language.Auto");
    }
}

/// <summary>XAML usage: <c>Text="{loc:Tr Settings.Heading}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
