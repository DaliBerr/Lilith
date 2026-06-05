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
