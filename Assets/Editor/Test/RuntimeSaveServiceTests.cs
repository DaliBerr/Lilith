using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeSaveServiceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

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
        DeleteSaveDirectory();
    }

    [Test]
    public void SelectProfileSlot_NewSlot_CreatesDefaultProfileAndSummary()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        bool selectSuccess = saveService.SelectProfileSlot(0, out bool isNewSlot);
        ProfileSlotSummary[] summaries = saveService.GetSlotSummaries();

        Assert.That(selectSuccess, Is.True);
        Assert.That(isNewSlot, Is.True);
        Assert.That(File.Exists(BuildProfilePath(0)), Is.True);
        Assert.That(saveService.ActiveProfileSlotIndex, Is.EqualTo(0));
        Assert.That(saveService.GetProfileSnapshot().RemnantCount, Is.EqualTo(0));
        Assert.That(summaries[0].HasProfile, Is.True);
        Assert.That(summaries[0].LastSavedUtcTicks, Is.GreaterThan(0L));
    }

    [Test]
    public void SelectProfileSlot_ExistingSlot_RestoresRemnantWalletCount()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        Assert.That(saveService.SelectProfileSlot(1, out bool firstSelectionIsNew), Is.True);
        Assert.That(firstSelectionIsNew, Is.True);
        Assert.That(wallet.ApplyLoadedRemnants(12), Is.True);
        Assert.That(saveService.SaveProfile(), Is.True);

        Assert.That(wallet.ApplyLoadedRemnants(1), Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(1));

        bool selectSuccess = saveService.SelectProfileSlot(1, out bool secondSelectionIsNew);

        Assert.That(selectSuccess, Is.True);
        Assert.That(secondSelectionIsNew, Is.False);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(12));
    }

    [Test]
    public void DeleteProfileSlot_RemovesFileAndClearsSummary()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        Assert.That(saveService.SelectProfileSlot(2, out _), Is.True);
        Assert.That(File.Exists(BuildProfilePath(2)), Is.True);

        bool deleteSuccess = saveService.DeleteProfileSlot(2);
        ProfileSlotSummary[] summaries = saveService.GetSlotSummaries();

        Assert.That(deleteSuccess, Is.True);
        Assert.That(File.Exists(BuildProfilePath(2)), Is.False);
        Assert.That(summaries[2].HasProfile, Is.False);
        Assert.That(saveService.HasSelectedProfileSlot, Is.False);
    }

    [Test]
    public void ResetProfile_ResetsRemnantsButLeavesGlobalModeUntouched()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(wallet.ApplyLoadedRemnants(11), Is.True);
        Assert.That(saveService.SaveProfile(), Is.True);

        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
        GlobalModeSettingsService.SetMode(GameMode.Dev);

        bool resetSuccess = saveService.ResetProfile();
        GameMode restoredMode = GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);

        Assert.That(resetSuccess, Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(0));
        Assert.That(saveService.GetProfileSnapshot().RemnantCount, Is.EqualTo(0));
        Assert.That(restoredMode, Is.EqualTo(GameMode.Dev));
    }

    [Test]
    public void RemnantChanges_AreDeferredUntilRunEndCommit()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();

        Assert.That(saveService.SelectProfileSlot(0, out bool isNewSlot), Is.True);
        Assert.That(isNewSlot, Is.True);
        Assert.That(ReadProfileFromDisk(0).RemnantCount, Is.EqualTo(0));

        Assert.That(saveService.SetRemnantCount(7), Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(7));
        Assert.That(ReadProfileFromDisk(0).RemnantCount, Is.EqualTo(0));

        Assert.That(saveService.AddRemnants(2, out int resultingCount), Is.True);
        Assert.That(resultingCount, Is.EqualTo(9));
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(9));
        Assert.That(ReadProfileFromDisk(0).RemnantCount, Is.EqualTo(0));

        Assert.That(saveService.CommitRunEndProfileState(), Is.True);
        Assert.That(ReadProfileFromDisk(0).RemnantCount, Is.EqualTo(9));
    }

    [Test]
    public void ReloadProfile_DoesNotRestoreRunDataOutsidePermanentProfile()
    {
        PrepareCleanSaveEnvironment();
        PlayerRemnantWallet wallet = CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PlayerHealth health = CreateComponent<PlayerHealth>("PlayerHealth");
        AttackFormulaLoadout loadout = CreateComponent<AttackFormulaLoadout>("AttackFormulaLoadout");
        PlayerBulletTokenInventory inventory = CreateComponent<PlayerBulletTokenInventory>("PlayerBulletTokenInventory");
        TestTokenData loadoutToken = CreateToken("loadout_token");
        TestTokenData inventoryToken = CreateToken("inventory_token");

        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(wallet.ApplyLoadedRemnants(9), Is.True);
        Assert.That(saveService.SaveProfile(), Is.True);

        Assert.That(health.TryApplyDamage(25f, out float remainingHealth, out _), Is.True);
        loadout.SetTokens(new[] { loadoutToken });
        Assert.That(inventory.SetToken(0, inventoryToken), Is.True);
        Assert.That(wallet.ApplyLoadedRemnants(2), Is.True);

        bool reloadSuccess = saveService.ReloadProfile();

        Assert.That(reloadSuccess, Is.True);
        Assert.That(wallet.CurrentRemnants, Is.EqualTo(9));
        Assert.That(health.CurrentHealth, Is.EqualTo(remainingHealth));
        Assert.That(loadout.Items.Count, Is.EqualTo(1));
        Assert.That(loadout.Items[0], Is.SameAs(loadoutToken));
        Assert.That(inventory.GetToken(0), Is.SameAs(inventoryToken));
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

    private T CreateComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = CreateGameObject(objectName);
        return gameObject.AddComponent<T>();
    }

    private TestTokenData CreateToken(string tokenId)
    {
        TestTokenData token = ScriptableObject.CreateInstance<TestTokenData>();
        token.TokenId = tokenId;
        token.DisplayText = tokenId;
        createdObjects.Add(token);
        return token;
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

    private static string BuildProfilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, "Saves", $"profile-slot-{slotIndex + 1}.json");
    }

    private static PermanentProfileData ReadProfileFromDisk(int slotIndex)
    {
        string profilePath = BuildProfilePath(slotIndex);
        string json = File.ReadAllText(profilePath);
        return JsonConvert.DeserializeObject<PermanentProfileData>(json);
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

    private sealed class TestTokenData : BaseTokenData
    {
    }
}
