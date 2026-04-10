using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class RuntimeSaveServiceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();
    private readonly List<string> createdSavePaths = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();
        DestroyExistingWallet();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();

        for (int i = 0; i < createdSavePaths.Count; i++)
        {
            string path = createdSavePaths[i];
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
        }

        createdSavePaths.Clear();
    }

    [Test]
    public void TrySaveAndTryLoad_RestoresRemnantWalletCount()
    {
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 12);
        RuntimeSaveService saveService = CreateSaveService();
        string slotName = "runtime_save_test_" + Guid.NewGuid().ToString("N");

        bool saveSuccess = saveService.TrySave(slotName, out string savePath);
        createdSavePaths.Add(savePath);

        Assert.That(saveSuccess, Is.True);
        Assert.That(File.Exists(savePath), Is.True);

        Assert.That(wallet.TrySetRemnants(1, out _), Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(1));

        bool loadSuccess = saveService.TryLoad(slotName, out string loadedPath);

        Assert.That(loadSuccess, Is.True);
        Assert.That(loadedPath, Is.EqualTo(savePath));
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(12));
    }

    [Test]
    public void EventRequests_SaveThenLoad_RestoresRemnantWalletCount()
    {
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 7);
        CreateSaveService();
        string slotName = "runtime_event_save_test_" + Guid.NewGuid().ToString("N");

        EventManager.eventBus.Publish(new EventList.SaveGameRequest(slotName));
        string savePath = BuildSavePath(slotName);
        createdSavePaths.Add(savePath);

        Assert.That(File.Exists(savePath), Is.True);

        Assert.That(wallet.TrySetRemnants(0, out _), Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(0));

        EventManager.eventBus.Publish(new EventList.LoadGameRequest(slotName));

        Assert.That(wallet.CurrentRemnants, Is.EqualTo(7));
    }

    private PlayerRemnantWallet CreateWallet(int initialCount)
    {
        DestroyExistingWallet();
        GameObject walletObject = CreateGameObject("Wallet");
        PlayerRemnantWallet wallet = walletObject.AddComponent<PlayerRemnantWallet>();
        wallet.TrySetRemnants(initialCount, out _);
        return wallet;
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

    private static string BuildSavePath(string slotName)
    {
        return Path.Combine(Application.persistentDataPath, "Saves", slotName + ".json");
    }

    private static void DestroyExistingWallet()
    {
        PlayerRemnantWallet existingWallet = UnityEngine.Object.FindFirstObjectByType<PlayerRemnantWallet>();
        if (existingWallet != null)
        {
            UnityEngine.Object.DestroyImmediate(existingWallet.gameObject);
        }
    }

    private static void DestroyExistingRuntimeSaveService()
    {
        RuntimeSaveService existingService = UnityEngine.Object.FindFirstObjectByType<RuntimeSaveService>();
        if (existingService != null)
        {
            UnityEngine.Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
