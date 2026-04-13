using System.IO;
using System.Reflection;
using Kernel.MapGrid;
using Kernel.Quest;
using NUnit.Framework;
using UnityEngine;

public sealed class StartRoomBattleTeleporterTests
{
    private readonly System.Collections.Generic.List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void IsTeleporterUnlocked_RequiresPermanentStoryFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        Assert.That(InvokeStatic<bool>(typeof(StartRoomBattleTeleporter), "IsTeleporterUnlocked"), Is.False);

        Assert.That(saveService.SetStoryFlag(TutorialQuestConstants.TeleporterUnlockedFlagId, true), Is.True);

        Assert.That(InvokeStatic<bool>(typeof(StartRoomBattleTeleporter), "IsTeleporterUnlocked"), Is.True);
    }

    [Test]
    public void MarkTeleporterTriggered_PersistsTriggerStoryFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        InvokeStatic(typeof(StartRoomBattleTeleporter), "MarkTeleporterTriggered");

        Assert.That(saveService.HasStoryFlag(TutorialQuestConstants.TeleporterTriggeredFlagId), Is.True);
    }

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = CreateGameObject("RuntimeSaveService");
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void PrepareCleanSaveEnvironment()
    {
        DeleteSaveDirectory();
        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
    }

    private static T InvokeStatic<T>(System.Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");
        return (T)method.Invoke(null, null);
    }

    private static void InvokeStatic(System.Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");
        method.Invoke(null, null);
    }

    private static void DeleteSaveDirectory()
    {
        string saveDirectoryPath = Path.Combine(Application.persistentDataPath, "Saves");
        if (Directory.Exists(saveDirectoryPath))
        {
            Directory.Delete(saveDirectoryPath, recursive: true);
        }
    }

    private static void DestroyExistingRuntimeSaveService()
    {
        RuntimeSaveService existingService = Object.FindFirstObjectByType<RuntimeSaveService>();
        if (existingService != null)
        {
            Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
