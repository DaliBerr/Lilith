using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class GlobalModeSettingsServiceTests
{
    [TearDown]
    public void TearDown()
    {
        string saveDirectoryPath = BuildSaveDirectoryPath();
        if (Directory.Exists(saveDirectoryPath))
        {
            Directory.Delete(saveDirectoryPath, recursive: true);
        }
    }

    [Test]
    public void LoadMode_WhenMissingFile_UsesDefaultAndCreatesGlobalModeJson()
    {
        PrepareCleanSaveEnvironment();

        GameMode mode = GlobalModeSettingsService.LoadMode(GameMode.Dev, forceReload: true);

        Assert.That(mode, Is.EqualTo(GameMode.Dev));
        Assert.That(File.Exists(BuildGlobalModePath()), Is.True);
    }

    [Test]
    public void SetMode_PersistsAcrossForcedReload()
    {
        PrepareCleanSaveEnvironment();

        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
        Assert.That(GlobalModeSettingsService.SetMode(GameMode.Dev), Is.True);

        GameMode reloadedMode = GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);

        Assert.That(reloadedMode, Is.EqualTo(GameMode.Dev));
    }

    [Test]
    public void SetProfileSlotState_PersistsAcrossForcedReload()
    {
        PrepareCleanSaveEnvironment();

        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
        Assert.That(GlobalModeSettingsService.SetProfileSlotState(2, hasProfile: true, lastSavedUtcTicks: 42L), Is.True);

        ProfileSlotStateData[] reloadedSlots = GlobalModeSettingsService.GetProfileSlotStatesSnapshot();

        Assert.That(reloadedSlots[2].HasProfile, Is.True);
        Assert.That(reloadedSlots[2].LastSavedUtcTicks, Is.EqualTo(42L));
    }

    [Test]
    public void SetProfileSlotState_GrowsProfileSlotsAcrossForcedReload()
    {
        PrepareCleanSaveEnvironment();

        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
        Assert.That(GlobalModeSettingsService.SetProfileSlotState(6, hasProfile: true, lastSavedUtcTicks: 99L), Is.True);
        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);

        ProfileSlotStateData[] reloadedSlots = GlobalModeSettingsService.GetProfileSlotStatesSnapshot();

        Assert.That(reloadedSlots.Length, Is.GreaterThanOrEqualTo(7));
        Assert.That(reloadedSlots[6].HasProfile, Is.True);
        Assert.That(reloadedSlots[6].LastSavedUtcTicks, Is.EqualTo(99L));
    }

    [Test]
    public void LoadMode_LegacyFourSlotJson_PreservesSlots()
    {
        PrepareCleanSaveEnvironment();
        Directory.CreateDirectory(BuildSaveDirectoryPath());
        File.WriteAllText(
            BuildGlobalModePath(),
            "{\"DataVersion\":1,\"SelectedMode\":\"Normal\",\"ProfileSlots\":[{\"HasProfile\":true,\"LastSavedUtcTicks\":11},{\"HasProfile\":false,\"LastSavedUtcTicks\":0},{\"HasProfile\":true,\"LastSavedUtcTicks\":22},{\"HasProfile\":false,\"LastSavedUtcTicks\":0}]}");

        GameMode mode = GlobalModeSettingsService.LoadMode(GameMode.Dev, forceReload: true);
        ProfileSlotStateData[] reloadedSlots = GlobalModeSettingsService.GetProfileSlotStatesSnapshot();

        Assert.That(mode, Is.EqualTo(GameMode.Normal));
        Assert.That(reloadedSlots.Length, Is.EqualTo(4));
        Assert.That(reloadedSlots[0].HasProfile, Is.True);
        Assert.That(reloadedSlots[0].LastSavedUtcTicks, Is.EqualTo(11L));
        Assert.That(reloadedSlots[2].HasProfile, Is.True);
        Assert.That(reloadedSlots[2].LastSavedUtcTicks, Is.EqualTo(22L));
    }

    private static void PrepareCleanSaveEnvironment()
    {
        string saveDirectoryPath = BuildSaveDirectoryPath();
        if (Directory.Exists(saveDirectoryPath))
        {
            Directory.Delete(saveDirectoryPath, recursive: true);
        }
    }

    private static string BuildGlobalModePath()
    {
        return Path.Combine(Application.persistentDataPath, "Saves", "global-mode.json");
    }

    private static string BuildSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "Saves");
    }
}
