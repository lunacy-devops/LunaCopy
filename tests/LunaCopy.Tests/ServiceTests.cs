using System.IO;
using LunaCopy.Native;
using LunaCopy.Services;
using Xunit;

namespace LunaCopy.Tests;

public class ServiceTests : IDisposable
{
    private readonly string _tempConfigFile;

    public ServiceTests()
    {
        _tempConfigFile = Path.Combine(Path.GetTempPath(), $"LunaCopy_TestConfig_{Guid.NewGuid():N}.json");
        ConfigService.SetCustomConfigPathForTesting(_tempConfigFile);
    }

    public void Dispose()
    {
        ConfigService.SetCustomConfigPathForTesting(null);
        ConfigService.ResetCurrentForTesting();
        try
        {
            if (File.Exists(_tempConfigFile))
            {
                File.Delete(_tempConfigFile);
            }
        }
        catch
        {
            // Ignore
        }
    }

    [Theory]
    [InlineData("notepad", "notepad.exe")]
    [InlineData("notepad.exe", "notepad.exe")]
    [InlineData("Notepad.EXE", "Notepad.exe")]
    [InlineData(@"C:\Windows\System32\cmd.exe", "cmd.exe")]
    [InlineData(@"D:\Games\CS2\game\bin\win64\cs2.exe", "cs2.exe")]
    [InlineData("  telegram  ", "telegram.exe")]
    public void NormalizeProcessName_ReturnsCleanExeName(string input, string expected)
    {
        string actual = ConfigService.NormalizeProcessName(input);
        Assert.Equal(expected, actual, ignoreCase: true);
    }

    [Theory]
    [InlineData("cmd.exe", true)]
    [InlineData("powershell.exe", true)]
    [InlineData("pwsh.exe", true)]
    [InlineData("conhost.exe", true)]
    [InlineData("windowsterminal.exe", true)]
    [InlineData("wt.exe", true)]
    [InlineData("alacritty.exe", true)]
    [InlineData("wezterm.exe", true)]
    [InlineData("putty.exe", true)]
    [InlineData("tabby.exe", true)]
    [InlineData("wsl.exe", true)]
    [InlineData("bash", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("chrome.exe", false)]
    [InlineData("code.exe", false)]
    public void IsConsoleProcess_CorrectlyIdentifiesConsolesAndTerminals(string procName, bool expected)
    {
        bool actual = TextExtractorService.IsConsoleProcess(procName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExclusionLogic_IsolatedTestConfig_WorksCaseInsensitively()
    {
        string testApp = "TestMockApp_" + Guid.NewGuid().ToString("N") + ".exe";

        Assert.False(ConfigService.IsExcluded(testApp));

        bool added = ConfigService.AddExclusion(testApp);
        Assert.True(added);
        Assert.True(ConfigService.IsExcluded(testApp.ToUpperInvariant()));
        Assert.True(ConfigService.IsExcluded(testApp.ToLowerInvariant()));

        // Cannot add duplicate
        Assert.False(ConfigService.AddExclusion(testApp));

        bool removed = ConfigService.RemoveExclusion(testApp.ToUpperInvariant());
        Assert.True(removed);
        Assert.False(ConfigService.IsExcluded(testApp));
    }

    [Fact]
    public void AppConfig_DefaultValues_AreCorrect()
    {
        var config = new AppConfig();
        Assert.True(config.IsEnabled);
        Assert.False(config.RunOnStartup);
        Assert.True(config.SoundEnabled);
        Assert.True(config.ToastEnabled);
        Assert.Contains("cs2.exe", config.ExcludedProcesses);
        Assert.Contains("Photoshop.exe", config.ExcludedProcesses);
    }

    [Theory]
    [InlineData(0, 0, 3, 4, 5.0, false)]      // Exactly 5px: threshold is > 5px -> false
    [InlineData(0, 0, 4, 4, 5.656, true)]    // > 5px -> true
    [InlineData(100, 100, 101, 102, 2.236, false)]
    [InlineData(10, 10, 50, 60, 64.03, true)]
    public void SelectionGestureDetector_IsDragSelection_EvaluatesProductionLogic(int x1, int y1, int x2, int y2, double expectedDist, bool exceedsThreshold)
    {
        var pt1 = new NativeMethods.POINT { X = x1, Y = y1 };
        var pt2 = new NativeMethods.POINT { X = x2, Y = y2 };

        double distance = SelectionGestureDetector.GetDistance(pt1, pt2);
        bool isDrag = SelectionGestureDetector.IsDragSelection(pt1, pt2);

        Assert.Equal(expectedDist, distance, precision: 2);
        Assert.Equal(exceedsThreshold, isDrag);
    }

    [Theory]
    [InlineData(1000, 1200, 500, true)]   // 200ms diff <= 500ms and 0 distance -> valid double click
    [InlineData(1000, 1600, 500, false)]  // 600ms diff > 500ms -> too slow for double click
    public void SelectionGestureDetector_IsDoubleClick_EvaluatesProductionTiming(uint downTime, uint nextClickTime, uint doubleClickMax, bool isSequence)
    {
        var pt = new NativeMethods.POINT { X = 100, Y = 100 };
        bool result = SelectionGestureDetector.IsDoubleClick(downTime, nextClickTime, pt, pt, doubleClickMax);
        Assert.Equal(isSequence, result);
    }

    [Fact]
    public void SelectionGestureDetector_IsDoubleClick_RejectsMovedClicks()
    {
        var pt1 = new NativeMethods.POINT { X = 100, Y = 100 };
        var pt2 = new NativeMethods.POINT { X = 110, Y = 110 }; // Distance ~14px > 5px

        bool result = SelectionGestureDetector.IsDoubleClick(1000, 1150, pt1, pt2, 500);
        Assert.False(result);
    }
}
