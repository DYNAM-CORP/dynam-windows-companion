namespace OpenClaw.Tray.Tests;

public sealed class FirstRunNodeConsentContractTests
{
    [Fact]
    public void FirstRun_RequiresLocalConsentAndPersistsChoicesBeforeConnecting()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OpenClaw.SetupEngine.UI",
            "Pages",
            "FirstRunSetupCodePage.xaml"));
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OpenClaw.SetupEngine.UI",
            "Pages",
            "FirstRunSetupCodePage.xaml.cs"));
        var windowManager = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OpenClaw.Tray.WinUI",
            "Services",
            "WindowManager.cs"));

        Assert.Contains("CapabilityConsentCheckBox.IsChecked != true", page);
        Assert.Contains("await applyCapabilities(selectedCapabilities)", page);
        Assert.True(page.IndexOf("await applyCapabilities(selectedCapabilities)", StringComparison.Ordinal) <
            page.IndexOf("ConnectWithFirstRunSetupCodeAsync", StringComparison.Ordinal));
        Assert.Contains("NodeScreenEnabled = capabilities.Screen", windowManager);
        Assert.Contains("NodeCameraEnabled = capabilities.Camera", windowManager);
        Assert.Contains("NodeLocationEnabled = capabilities.Location", windowManager);
        Assert.Contains("Capture-consent flags", windowManager);
        Assert.Contains("existing approval policy", windowManager);
        Assert.Contains("Browser control is unavailable in this setup.", xaml);
        Assert.DoesNotContain("NodeBrowserProxyEnabled = capabilities", windowManager);
    }

    [Fact]
    public void FreshInstall_DisablesSensitiveCaptureAndUnavailableBrowserUntilSelected()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "OpenClaw.Tray.WinUI", "App.xaml.cs"));

        Assert.Contains("!_settings.SettingsFileExistedAtLoad", app);
        Assert.Contains("_settings.NodeScreenEnabled = false", app);
        Assert.Contains("_settings.NodeCameraEnabled = false", app);
        Assert.Contains("_settings.NodeLocationEnabled = false", app);
        Assert.Contains("_settings.NodeBrowserProxyEnabled = false", app);
        Assert.DoesNotContain("ScreenRecordingConsentGiven = true", app);
        Assert.DoesNotContain("CameraRecordingConsentGiven = true", app);
        Assert.DoesNotContain("LocationConsentGiven = true", app);
    }
}
