using System.Globalization;
using System.IO;
using System.Windows;
using LunaCopy.Services;
using LunaCopy.Windows;
using Xunit;

namespace LunaCopy.Tests;

public class LocalizationTests : IDisposable
{
    private readonly string _tempConfigFile;

    public LocalizationTests()
    {
        _tempConfigFile = Path.Combine(Path.GetTempPath(), $"LunaCopy_LocTest_{Guid.NewGuid():N}.json");
        ConfigService.SetCustomConfigPathForTesting(_tempConfigFile);
    }

    public void Dispose()
    {
        ConfigService.SetCustomConfigPathForTesting(null);
        try { File.Delete(_tempConfigFile); } catch { /* ignore */ }
    }

    [Fact]
    public void Languages_HaveIdenticalKeySets()
    {
        Assert.Equal(Loc.Ru.Keys.OrderBy(k => k), Loc.En.Keys.OrderBy(k => k));
    }

    [Fact]
    public void AllTranslations_AreNonEmpty()
    {
        Assert.All(Loc.Ru, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
        Assert.All(Loc.En, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
    }

    [Theory]
    [InlineData("ru", "en-US", "ru")]
    [InlineData("en", "ru-RU", "en")]
    [InlineData("auto", "ru-RU", "ru")]
    [InlineData("auto", "de-DE", "en")]
    [InlineData(null, "en-GB", "en")]
    [InlineData("garbage", "ru-RU", "ru")]
    public void Resolve_PicksExpectedLanguage(string? configured, string culture, string expected)
    {
        Assert.Equal(expected, Loc.Resolve(configured, new CultureInfo(culture)));
    }

    [Fact]
    public void NextSetting_CyclesAutoRuEn()
    {
        Assert.Equal("ru", Loc.NextSetting("auto"));
        Assert.Equal("en", Loc.NextSetting("ru"));
        Assert.Equal("auto", Loc.NextSetting("en"));
    }

    [Fact]
    public void Get_UsesConfiguredLanguage_AndFallsBackToKey()
    {
        ConfigService.Current.Language = "en";
        Assert.Equal("Copied!", Loc.Get("Toast.Copied"));

        ConfigService.Current.Language = "ru";
        Assert.Equal("Скопировано!", Loc.Get("Toast.Copied"));

        Assert.Equal("No.Such.Key", Loc.Get("No.Such.Key"));
    }

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    public void AllWindows_LoadWithoutErrors(string language)
    {
        ConfigService.Current.Language = language;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                }

                var toast = new ToastWindow();
                var tray = new TrayMenuWindow(() => { }, () => { }, () => { }, () => "test.exe", () => { });
                var settings = new SettingsWindow();
                var add = new AddExclusionWindow();

                Assert.Equal(Loc.Get("Settings.WindowTitle"), settings.Title);
                Assert.Equal(Loc.Get("Add.WindowTitle"), add.Title);

                toast.Close();
                tray.Close();
                settings.Close();
                add.Close();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
    }
}
