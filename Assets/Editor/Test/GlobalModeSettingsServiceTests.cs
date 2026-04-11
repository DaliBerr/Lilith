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
