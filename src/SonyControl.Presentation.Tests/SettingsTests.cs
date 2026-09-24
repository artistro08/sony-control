using SonyControl.Presentation.Headsets;
using SonyControl.Presentation.Navigation;
using SonyControl.Presentation.Scenes;
using SonyControl.Presentation.Settings;

namespace SonyControl.Presentation.Tests;

[TestClass]
public sealed class AppSettingsTests
{
    [TestMethod]
    public void UsesDefaultsWhenNothingIsSaved()
    {
        var settings = new AppSettings(new InMemorySettingsStore());

        Assert.IsNull(settings.RememberedHeadsetId);
        Assert.IsTrue(settings.LowBatteryNotifications);
        Assert.AreEqual(AppTheme.System, settings.Theme);
        Assert.IsFalse(settings.DebugLogging);
        Assert.IsTrue(settings.IsAutoConnectEnabled("AC:80:0A:12:34:56"));
        CollectionAssert.AreEqual(Scene.Defaults.ToList(), settings.Scenes.ToList());
    }

    [TestMethod]
    public void RoundTripsEveryValue()
    {
        var store = new InMemorySettingsStore();
        var settings = new AppSettings(store)
        {
            RememberedHeadsetId = "AC:80:0A:12:34:56",
            LowBatteryNotifications = false,
            Theme = AppTheme.Dark,
            DebugLogging = true,
        };
        settings.SetAutoConnectEnabled("AC:80:0A:12:34:56", false);

        var reloaded = new AppSettings(store);
        Assert.AreEqual("AC:80:0A:12:34:56", reloaded.RememberedHeadsetId);
        Assert.IsFalse(reloaded.LowBatteryNotifications);
        Assert.AreEqual(AppTheme.Dark, reloaded.Theme);
        Assert.IsTrue(reloaded.DebugLogging);
        Assert.IsFalse(reloaded.IsAutoConnectEnabled("AC:80:0A:12:34:56"));
    }

    [TestMethod]
    public void RoundTripsScenes()
    {
        var store = new InMemorySettingsStore();
        Scene[] scenes = [new Scene("Gym", "", new NoiseControlSetting(NoiseMode.Ambient, 14, true))];
        new AppSettings(store).Scenes = scenes;

        CollectionAssert.AreEqual(scenes, new AppSettings(store).Scenes.ToList());
    }

    [TestMethod]
    public void FallsBackToDefaultScenesWhenSavedJsonIsBroken()
    {
        var store = new InMemorySettingsStore();
        store.SetString("Scenes", "{not json");

        CollectionAssert.AreEqual(Scene.Defaults.ToList(), new AppSettings(store).Scenes.ToList());
    }

    [TestMethod]
    public void ClearingRememberedHeadsetRemovesIt()
    {
        var settings = new AppSettings(new InMemorySettingsStore()) { RememberedHeadsetId = "A" };
        settings.RememberedHeadsetId = null;

        Assert.IsNull(settings.RememberedHeadsetId);
    }
}

[TestClass]
public sealed class FlyoutNavigatorTests
{
    private const string Xm6 = "AC:80:0A:00:00:06";
    private const string Xm4 = "AC:80:0A:00:00:04";

    private readonly AppSettings _settings = new(new InMemorySettingsStore());

    [TestMethod]
    public void NoHeadsetsShowsEmptyPage() =>
        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Empty, null, false), Navigator().Resolve([]));

    [TestMethod]
    public void OneHeadsetGoesStraightToItsPage() =>
        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Device, Xm6, false), Navigator().Resolve([Xm6]));

    [TestMethod]
    public void SeveralHeadsetsWithNothingRememberedShowsPicker() =>
        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Picker, null, false), Navigator().Resolve([Xm6, Xm4]));

    [TestMethod]
    public void PickingRemembersTheHeadset()
    {
        Navigator().Pick(Xm4);

        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Device, Xm4, true), Navigator().Resolve([Xm6, Xm4]));
    }

    [TestMethod]
    public void RememberedHeadsetDisconnectedFallsBackToPicker()
    {
        _settings.RememberedHeadsetId = Xm6;

        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Picker, null, false), Navigator().Resolve([Xm4, "AC:80:0A:00:00:05"]));
    }

    [TestMethod]
    public void RememberedHeadsetDisconnectedFallsBackToOnlyRemainingHeadset()
    {
        _settings.RememberedHeadsetId = Xm6;

        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Device, Xm4, false), Navigator().Resolve([Xm4]));
        Assert.AreEqual(Xm6, _settings.RememberedHeadsetId);
    }

    [TestMethod]
    public void RememberedHeadsetReconnectingReturnsToItsPage()
    {
        _settings.RememberedHeadsetId = Xm6;
        _ = Navigator().Resolve([Xm4]);

        Assert.AreEqual(new FlyoutRoute(FlyoutPageKind.Device, Xm6, true), Navigator().Resolve([Xm4, Xm6]));
    }

    [TestMethod]
    public void BackClearsTheRememberedHeadset()
    {
        _settings.RememberedHeadsetId = Xm6;
        Navigator().Back();

        Assert.IsNull(_settings.RememberedHeadsetId);
        Assert.AreEqual(FlyoutPageKind.Picker, Navigator().Resolve([Xm6, Xm4]).Kind);
    }

    [TestMethod]
    public void UnknownRememberedHeadsetShowsPicker()
    {
        _settings.RememberedHeadsetId = "UNPAIRED";

        Assert.AreEqual(FlyoutPageKind.Picker, Navigator().Resolve([Xm6, Xm4]).Kind);
    }

    private FlyoutNavigator Navigator() => new(_settings);
}
