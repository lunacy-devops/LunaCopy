using System.Text;
using LunaCopy.Services;
using Xunit;

namespace LunaCopy.Tests;

public class ServiceTests
{
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
    [InlineData("wsl.exe", true)]
    [InlineData("bash", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("chrome.exe", false)]
    [InlineData("code.exe", false)]
    public void IsConsoleProcess_CorrectlyIdentifiesConsoles(string procName, bool expected)
    {
        bool actual = TextExtractorService.IsConsoleProcess(procName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExclusionLogic_WorksCaseInsensitively()
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
}
