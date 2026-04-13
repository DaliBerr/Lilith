using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Kernel.Bullet;
using Kernel.Quest;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class QuestServiceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingQuestService();
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
        DeleteSaveDirectory();
    }

    [Test]
    public void TryUseCatalog_PrerequisiteScan_ActivatesQuestOnlyOnceAndPersistsProgress()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 5);
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(wallet.ApplyLoadedRemnants(5), Is.True);
        Assert.That(saveService.SetRemnantCount(5), Is.True);

        QuestService service = CreateQuestService();
        QuestCatalogData catalog = BuildCatalog(new QuestDefinitionData
        {
            Id = "quest_activate_once",
            Text = "Activate once",
            Prerequisites = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.RemnantsAtLeast, Value = 5 }
            },
            Completion = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.CombatVictoryCountAtLeast, Value = 99 }
            },
            Rewards = new List<QuestRewardData>
            {
                new() { Kind = QuestRewardKind.Remnants, Amount = 1 }
            }
        });

        Assert.That(TryUseCatalog(service, catalog, new Dictionary<string, PlaceableTokenData>(StringComparer.Ordinal), out string errorMessage), Is.True, errorMessage);
        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));
        Assert.That(service.GetActiveQuestSnapshots()[0].QuestId, Is.EqualTo("quest_activate_once"));
        Assert.That(saveService.GetActiveQuestProgressSnapshot().ContainsKey("quest_activate_once"), Is.True);

        InvokeNonPublic(service, "ScanForNewlyAvailableQuests");

        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));
        Assert.That(saveService.GetActiveQuestProgressSnapshot().Count, Is.EqualTo(1));
    }

    [Test]
    public void CombatVictoryCompletion_GrantsRewardsAndStaysCompletedAfterReload()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        QuestService service = CreateQuestService();
        QuestCatalogData catalog = BuildCatalog(new QuestDefinitionData
        {
            Id = "quest_win_once",
            Text = "Win once",
            Completion = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.CombatVictoryCountAtLeast, Value = 1 }
            },
            Rewards = new List<QuestRewardData>
            {
                new() { Kind = QuestRewardKind.Remnants, Amount = 10 },
                new() { Kind = QuestRewardKind.StoryFlagSet, StoryFlagId = "quest/win_once" }
            }
        });

        Assert.That(TryUseCatalog(service, catalog, new Dictionary<string, PlaceableTokenData>(StringComparer.Ordinal), out string errorMessage), Is.True, errorMessage);
        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));

        InvokeNonPublic(service, "HandleCombatVictory", new CombatVictoryEvent(null, 1));

        Assert.That(service.GetActiveQuestSnapshots(), Is.Empty);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(10));
        Assert.That(saveService.HasCompletedQuest("quest_win_once"), Is.True);
        Assert.That(saveService.HasStoryFlag("quest/win_once"), Is.True);

        Assert.That(saveService.ReloadProfile(), Is.True);
        DestroyExistingQuestService();

        QuestService reloadedService = CreateQuestService();
        Assert.That(TryUseCatalog(reloadedService, catalog, new Dictionary<string, PlaceableTokenData>(StringComparer.Ordinal), out errorMessage), Is.True, errorMessage);
        Assert.That(reloadedService.GetActiveQuestSnapshots(), Is.Empty);
    }

    [Test]
    public void FullInventory_BlocksCompletionAndKeepsQuestActive()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        TestTokenData occupiedToken = CreateToken("occupied_token", "Occupied");
        TestTokenData rewardToken = CreateToken("reward_token", "Reward");
        PlayerBulletTokenInventory inventory = CreateInventory();
        FillInventory(inventory, occupiedToken);

        QuestService service = CreateQuestService();
        QuestCatalogData catalog = BuildCatalog(new QuestDefinitionData
        {
            Id = "quest_reward_blocked",
            Text = "Reward blocked",
            Completion = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.CombatVictoryCountAtLeast, Value = 1 }
            },
            Rewards = new List<QuestRewardData>
            {
                new() { Kind = QuestRewardKind.InventoryToken, TokenAddress = "token/reward" }
            }
        });

        Dictionary<string, PlaceableTokenData> resolvedTokens = new(StringComparer.Ordinal)
        {
            ["token/reward"] = rewardToken
        };

        Assert.That(TryUseCatalog(service, catalog, resolvedTokens, out string errorMessage), Is.True, errorMessage);
        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));

        InvokeNonPublic(service, "HandleCombatVictory", new CombatVictoryEvent(null, 1));

        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));
        Assert.That(saveService.HasCompletedQuest("quest_reward_blocked"), Is.False);
        Assert.That(saveService.GetActiveQuestProgressSnapshot().ContainsKey("quest_reward_blocked"), Is.True);
    }

    [Test]
    public void InventoryChange_CanActivateAndCompleteInventoryDrivenQuest()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        TestTokenData keyToken = CreateToken("key_token", "Key");
        PlayerBulletTokenInventory inventory = CreateInventory();
        QuestService service = CreateQuestService();
        QuestCatalogData catalog = BuildCatalog(new QuestDefinitionData
        {
            Id = "quest_inventory_driven",
            Text = "Collect two keys",
            Prerequisites = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.InventoryContainsToken, TokenAddress = "token/key" }
            },
            Completion = new List<QuestConditionData>
            {
                new() { Kind = QuestConditionKind.InventoryTokenCountAtLeast, TokenAddress = "token/key", Value = 2 }
            },
            Rewards = new List<QuestRewardData>
            {
                new() { Kind = QuestRewardKind.UnlockId, UnlockId = "unlock/key_master" }
            }
        });

        Dictionary<string, PlaceableTokenData> resolvedTokens = new(StringComparer.Ordinal)
        {
            ["token/key"] = keyToken
        };

        Assert.That(TryUseCatalog(service, catalog, resolvedTokens, out string errorMessage), Is.True, errorMessage);
        Assert.That(service.GetActiveQuestSnapshots(), Is.Empty);

        Assert.That(inventory.TryAddItem(keyToken, out _), Is.True);
        InvokeNonPublic(service, "HandleInventoryChanged");

        Assert.That(service.GetActiveQuestSnapshots().Count, Is.EqualTo(1));
        Assert.That(saveService.GetActiveQuestProgressSnapshot().ContainsKey("quest_inventory_driven"), Is.True);

        Assert.That(inventory.TryAddItem(keyToken, out _), Is.True);
        InvokeNonPublic(service, "HandleInventoryChanged");

        Assert.That(service.GetActiveQuestSnapshots(), Is.Empty);
        Assert.That(saveService.HasCompletedQuest("quest_inventory_driven"), Is.True);
        Assert.That(saveService.GetProfileSnapshot().UnlockedIds.Contains("unlock/key_master"), Is.True);
    }

    private static QuestCatalogData BuildCatalog(params QuestDefinitionData[] quests)
    {
        QuestCatalogData catalog = new();
        catalog.Quests.AddRange(quests);
        return catalog;
    }

    private PlayerRemnantWallet CreateWallet(int initialCount)
    {
        DestroyExistingWallet();
        GameObject walletObject = CreateGameObject("Wallet");
        PlayerRemnantWallet wallet = walletObject.AddComponent<PlayerRemnantWallet>();
        wallet.ApplyLoadedRemnants(initialCount);
        return wallet;
    }

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = CreateGameObject("RuntimeSaveService");
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private QuestService CreateQuestService()
    {
        DestroyExistingQuestService();
        GameObject questObject = CreateGameObject("QuestService");
        return questObject.AddComponent<QuestService>();
    }

    private PlayerBulletTokenInventory CreateInventory()
    {
        GameObject inventoryObject = CreateGameObject("PlayerBulletTokenInventory");
        PlayerBulletTokenInventory inventory = inventoryObject.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        return inventory;
    }

    private void FillInventory(PlayerBulletTokenInventory inventory, PlaceableTokenData token)
    {
        for (int index = 0; index < PlayerBulletTokenInventory.Capacity; index++)
        {
            Assert.That(inventory.SetToken(index, token as BaseTokenData), Is.True, $"Failed to occupy inventory index {index}.");
        }
    }

    private TestTokenData CreateToken(string tokenId, string displayText)
    {
        TestTokenData token = ScriptableObject.CreateInstance<TestTokenData>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static bool TryUseCatalog(
        QuestService service,
        QuestCatalogData catalog,
        IReadOnlyDictionary<string, PlaceableTokenData> resolvedTokens,
        out string errorMessage)
    {
        object[] arguments =
        {
            catalog,
            resolvedTokens,
            null
        };

        bool success = (bool)GetMethod(typeof(QuestService), "TryUseCatalog").Invoke(service, arguments);
        errorMessage = arguments[2] as string;
        return success;
    }

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        GetMethod(target.GetType(), methodName).Invoke(target, arguments);
    }

    private static System.Reflection.MethodInfo GetMethod(Type type, string methodName)
    {
        System.Reflection.MethodInfo method = type.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} should exist.");
        return method;
    }

    private static void PrepareCleanSaveEnvironment()
    {
        DeleteSaveDirectory();
        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
    }

    private static string BuildSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "Saves");
    }

    private static void DeleteSaveDirectory()
    {
        string saveDirectoryPath = BuildSaveDirectoryPath();
        if (Directory.Exists(saveDirectoryPath))
        {
            Directory.Delete(saveDirectoryPath, recursive: true);
        }
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

    private static void DestroyExistingQuestService()
    {
        QuestService existingService = UnityEngine.Object.FindFirstObjectByType<QuestService>();
        if (existingService != null)
        {
            UnityEngine.Object.DestroyImmediate(existingService.gameObject);
        }
    }

    private sealed class TestTokenData : BaseTokenData
    {
    }
}
