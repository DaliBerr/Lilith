using System.Collections.Generic;
using System.IO;
using Kernel.Bullet;
using Kernel.Upgrade;
using NUnit.Framework;
using UnityEngine;

public sealed class PermanentUpgradeServiceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingPermanentUpgradeService();
        DestroyExistingRuntimeSaveService();
        DestroyExistingWallet();

        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void TryDeserializeCatalogJson_DuplicateEntryId_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"dup\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0},{\"id\":\"dup\",\"title\":\"B\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("duplicated"));
    }

    [Test]
    public void TryDeserializeCatalogJson_EmptyEntryId_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\" \",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("missing a valid id"));
    }

    [Test]
    public void TryDeserializeCatalogJson_NegativeCost_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":-1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("negative Remnant cost"));
    }

    [Test]
    public void TryDeserializeCatalogJson_MaxLevelLessThanOne_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":0,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("maxLevel >= 1"));
    }

    [Test]
    public void TryDeserializeCatalogJson_OldJson_UsesDefaultLayoutValues()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out PermanentUpgradeCatalogData catalog, out string errorMessage);

        Assert.That(success, Is.True, errorMessage);
        Assert.That(catalog.CanvasSize.X, Is.EqualTo(1800f));
        Assert.That(catalog.CanvasSize.Y, Is.EqualTo(1200f));
        Assert.That(catalog.Edges, Is.Empty);
        PermanentUpgradeEntryData entry = catalog.Sections[0].Entries[0];
        Assert.That(entry.Position.X, Is.EqualTo(0f));
        Assert.That(entry.Position.Y, Is.EqualTo(0f));
        Assert.That(entry.Size.X, Is.EqualTo(100f));
        Assert.That(entry.Size.Y, Is.EqualTo(100f));
        Assert.That(entry.Shape, Is.EqualTo(PermanentUpgradeNodeShape.Rectangle));
        Assert.That(entry.Requires, Is.Empty);
        Assert.That(entry.BackgroundColor, Is.EqualTo("#1F2937"));
        Assert.That(entry.BorderColor, Is.EqualTo("#66E35F"));
        Assert.That(entry.BorderWidth, Is.EqualTo(4f));
    }

    [Test]
    public void TryDeserializeCatalogJson_NewJson_ParsesLayoutEdgesAndRequires()
    {
        const string json = "{\"canvasSize\":{\"x\":640,\"y\":480},\"edges\":[{\"from\":\"damage_1\",\"to\":\"damage_2\",\"color\":\"#123456\",\"width\":6}],\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage_1\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":0.1,\"requires\":[],\"position\":{\"x\":12,\"y\":24},\"size\":{\"x\":96,\"y\":88},\"shape\":\"Circle\",\"iconAddress\":\"Assets/Icon\",\"backgroundColor\":\"#111111\",\"borderColor\":\"#222222\",\"borderWidth\":3},{\"id\":\"damage_2\",\"title\":\"B\",\"costRemnants\":2,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":0.2,\"requires\":[\"damage_1\"],\"position\":{\"x\":160,\"y\":24},\"size\":{\"x\":100,\"y\":100},\"shape\":\"Diamond\",\"iconAddress\":\"\",\"backgroundColor\":\"#333333\",\"borderColor\":\"#444444\",\"borderWidth\":5}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out PermanentUpgradeCatalogData catalog, out string errorMessage);

        Assert.That(success, Is.True, errorMessage);
        Assert.That(catalog.CanvasSize.X, Is.EqualTo(640f));
        Assert.That(catalog.CanvasSize.Y, Is.EqualTo(480f));
        Assert.That(catalog.Edges, Has.Count.EqualTo(1));
        Assert.That(catalog.Edges[0].From, Is.EqualTo("damage_1"));
        Assert.That(catalog.Edges[0].To, Is.EqualTo("damage_2"));
        Assert.That(catalog.Edges[0].Color, Is.EqualTo("#123456"));
        Assert.That(catalog.Edges[0].Width, Is.EqualTo(6f));
        PermanentUpgradeEntryData firstEntry = catalog.Sections[0].Entries[0];
        PermanentUpgradeEntryData secondEntry = catalog.Sections[0].Entries[1];
        Assert.That(firstEntry.Position.X, Is.EqualTo(12f));
        Assert.That(firstEntry.Size.Y, Is.EqualTo(88f));
        Assert.That(firstEntry.Shape, Is.EqualTo(PermanentUpgradeNodeShape.Circle));
        Assert.That(firstEntry.IconAddress, Is.EqualTo("Assets/Icon"));
        Assert.That(secondEntry.Requires, Is.EquivalentTo(new[] { "damage_1" }));
        Assert.That(secondEntry.Shape, Is.EqualTo(PermanentUpgradeNodeShape.Diamond));
        Assert.That(secondEntry.BorderWidth, Is.EqualTo(5f));
    }

    [Test]
    public void TryDeserializeCatalogJson_UnknownRequirement_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"requires\":[\"missing\"]}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("requires unknown entry"));
    }

    [Test]
    public void TryDeserializeCatalogJson_SelfRequirement_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"requires\":[\"damage\"]}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("cannot require itself"));
    }

    [Test]
    public void TryDeserializeCatalogJson_CyclicRequirement_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"a\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"requires\":[\"b\"]},{\"id\":\"b\",\"title\":\"B\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"requires\":[\"a\"]}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("cycle"));
    }

    [Test]
    public void TryDeserializeCatalogJson_UnknownEdgeEndpoint_IsRejected()
    {
        const string json = "{\"edges\":[{\"from\":\"damage\",\"to\":\"missing\",\"color\":\"#66E35F\",\"width\":8}],\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("unknown to endpoint"));
    }

    [Test]
    public void TryDeserializeCatalogJson_InvalidColor_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"borderColor\":\"not-a-color\"}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("valid HTML color"));
    }

    [Test]
    public void TryDeserializeCatalogJson_InvalidSize_IsRejected()
    {
        const string json = "{\"sections\":[{\"id\":\"combat\",\"title\":\"Combat\",\"entries\":[{\"id\":\"damage\",\"title\":\"A\",\"costRemnants\":1,\"maxLevel\":1,\"effectType\":\"DamageMultiplierBonus\",\"effectValue\":1.0,\"size\":{\"x\":0,\"y\":100}}]}]}";

        bool success = PermanentUpgradeService.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("positive x/y"));
    }

    [Test]
    public void TrySpendCurrentRemnants_InsufficientBalance_DoesNotMutateWallet()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(5, out int configuredCount), Is.True);
        Assert.That(configuredCount, Is.EqualTo(5));

        bool spendSuccess = PlayerRemnantWallet.TrySpendCurrentRemnants(6, out int remainingCount);

        Assert.That(spendSuccess, Is.False);
        Assert.That(remainingCount, Is.EqualTo(5));
        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(5));
        Assert.That(saveService.GetCurrentRemnantCount(), Is.EqualTo(5));
    }

    [Test]
    public void TryPurchase_Once_UpdatesStoredLevelAndCompiledDamageMultiplier()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();
        CoreTokenData coreToken = CreateCoreToken(2f, "火");

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(10, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreateCatalog(costRemnants: 10, maxLevel: 1, effectValue: 1f), out string errorMessage), Is.True, errorMessage);
        Assert.That(upgradeService.TryPurchase("damage_test", out PermanentUpgradePurchaseResult purchaseResult), Is.True, purchaseResult.Message);

        CompiledSpellProgram compiledProgram = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken });

        Assert.That(purchaseResult.NewLevel, Is.EqualTo(1));
        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(0));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_test")), Is.EqualTo(1));
        Assert.That(upgradeService.GetDamageMultiplier(), Is.EqualTo(2f));
        Assert.That(compiledProgram.TryGetPrimaryProjectile(out SpellProjectileNode projectile), Is.True);
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(4f));
    }

    [Test]
    public void TryPurchase_MaxLevelReached_DoesNotSpendTwice()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(20, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreateCatalog(costRemnants: 10, maxLevel: 1, effectValue: 1f), out string errorMessage), Is.True, errorMessage);
        Assert.That(upgradeService.TryPurchase("damage_test", out _), Is.True);

        bool secondPurchaseSuccess = upgradeService.TryPurchase("damage_test", out PermanentUpgradePurchaseResult secondResult);

        Assert.That(secondPurchaseSuccess, Is.False);
        Assert.That(secondResult.FailureReason, Is.EqualTo(PermanentUpgradePurchaseFailureReason.MaxLevelReached));
        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(10));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_test")), Is.EqualTo(1));
    }

    [Test]
    public void TryPurchase_PrerequisiteMissing_DoesNotSpendOrMutateSave()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(30, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreatePrerequisiteCatalog(), out string errorMessage), Is.True, errorMessage);

        bool purchaseSuccess = upgradeService.TryPurchase("damage_child", out PermanentUpgradePurchaseResult purchaseResult);

        Assert.That(purchaseSuccess, Is.False);
        Assert.That(purchaseResult.FailureReason, Is.EqualTo(PermanentUpgradePurchaseFailureReason.PrerequisiteMissing));
        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(30));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_child")), Is.EqualTo(0));
    }

    [Test]
    public void TryPurchase_AfterPrerequisite_TargetCanBePurchased()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(30, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreatePrerequisiteCatalog(), out string errorMessage), Is.True, errorMessage);
        Assert.That(upgradeService.TryPurchase("damage_parent", out _), Is.True);

        bool childPurchaseSuccess = upgradeService.TryPurchase("damage_child", out PermanentUpgradePurchaseResult childResult);

        Assert.That(childPurchaseSuccess, Is.True, childResult.Message);
        Assert.That(childResult.NewLevel, Is.EqualTo(1));
        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(0));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_parent")), Is.EqualTo(1));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_child")), Is.EqualTo(1));
    }

    private PermanentUpgradeCatalogData CreateCatalog(int costRemnants, int maxLevel, float effectValue)
    {
        return new PermanentUpgradeCatalogData
        {
            Sections = new List<PermanentUpgradeSectionData>
            {
                new()
                {
                    Id = "combat",
                    Title = "Combat",
                    Entries = new List<PermanentUpgradeEntryData>
                    {
                        new()
                        {
                            Id = "damage_test",
                            Title = "Attack +100%",
                            CostRemnants = costRemnants,
                            MaxLevel = maxLevel,
                            EffectType = PermanentUpgradeEffectType.DamageMultiplierBonus,
                            EffectValue = effectValue,
                        }
                    }
                }
            }
        };
    }

    private PermanentUpgradeCatalogData CreatePrerequisiteCatalog()
    {
        return new PermanentUpgradeCatalogData
        {
            Edges = new List<PermanentUpgradeEdgeData>
            {
                new()
                {
                    From = "damage_parent",
                    To = "damage_child",
                    Color = "#66E35F",
                    Width = 8f,
                }
            },
            Sections = new List<PermanentUpgradeSectionData>
            {
                new()
                {
                    Id = "combat",
                    Title = "Combat",
                    Entries = new List<PermanentUpgradeEntryData>
                    {
                        new()
                        {
                            Id = "damage_parent",
                            Title = "Parent",
                            CostRemnants = 10,
                            MaxLevel = 1,
                            EffectType = PermanentUpgradeEffectType.DamageMultiplierBonus,
                            EffectValue = 0.1f,
                            Position = new PermanentUpgradeVector2Data { X = 0f, Y = 0f },
                            Size = new PermanentUpgradeVector2Data { X = 100f, Y = 100f },
                        },
                        new()
                        {
                            Id = "damage_child",
                            Title = "Child",
                            CostRemnants = 20,
                            MaxLevel = 1,
                            EffectType = PermanentUpgradeEffectType.DamageMultiplierBonus,
                            EffectValue = 0.2f,
                            Requires = new List<string> { "damage_parent" },
                            Position = new PermanentUpgradeVector2Data { X = 160f, Y = 0f },
                            Size = new PermanentUpgradeVector2Data { X = 100f, Y = 100f },
                        },
                    }
                }
            }
        };
    }

    private CoreTokenData CreateCoreToken(float damage, string displayText)
    {
        CoreTokenData token = ScriptableObject.CreateInstance<CoreTokenData>();
        token.TokenId = "core";
        token.DisplayText = displayText;
        token.Damage = damage;
        createdObjects.Add(token);
        return token;
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

    private PermanentUpgradeService CreateUpgradeService()
    {
        DestroyExistingPermanentUpgradeService();
        GameObject upgradeObject = CreateGameObject("PermanentUpgradeService");
        return upgradeObject.AddComponent<PermanentUpgradeService>();
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

    private static void DeleteSaveDirectory()
    {
        string saveDirectoryPath = Path.Combine(Application.persistentDataPath, "Saves");
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

    private static void DestroyExistingPermanentUpgradeService()
    {
        PermanentUpgradeService existingService = UnityEngine.Object.FindFirstObjectByType<PermanentUpgradeService>();
        if (existingService != null)
        {
            UnityEngine.Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
