using System.IO;
using System.Reflection;
using Kernel;
using Kernel.Quest;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class GlobalStartupTests
{
    private readonly System.Collections.Generic.List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();
        DestroyExistingGlobalStartup();

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
    public void HandleStartStorySequenceCompleted_CompletedStory_PersistsIntroductionReadFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        GlobalStartup startup = CreateGlobalStartup();
        InvokeNonPublic(startup, "HandleStartStorySequenceCompleted", new StorySequenceResult(StorySequenceCompletionStatus.Completed));

        Assert.That(saveService.HasStoryFlag(TutorialQuestConstants.IntroductionReadFlagId), Is.True);
    }

    [Test]
    public void HandleStartStorySequenceCompleted_FailedStory_DoesNotPersistIntroductionReadFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        GlobalStartup startup = CreateGlobalStartup();
        InvokeNonPublic(startup, "HandleStartStorySequenceCompleted", new StorySequenceResult(StorySequenceCompletionStatus.Failed, "failed"));

        Assert.That(saveService.HasStoryFlag(TutorialQuestConstants.IntroductionReadFlagId), Is.False);
    }

    private GlobalStartup CreateGlobalStartup()
    {
        DestroyExistingGlobalStartup();
        GameObject startupObject = CreateGameObject("GlobalStartup");
        return startupObject.AddComponent<GlobalStartup>();
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

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, arguments);
    }

    private static void PrepareCleanSaveEnvironment()
    {
        DeleteSaveDirectory();
        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
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

    private static void DestroyExistingGlobalStartup()
    {
        GlobalStartup existingStartup = Object.FindFirstObjectByType<GlobalStartup>();
        if (existingStartup != null)
        {
            Object.DestroyImmediate(existingStartup.gameObject);
        }
    }
}
