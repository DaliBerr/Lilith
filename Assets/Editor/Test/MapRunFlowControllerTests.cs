using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using Kernel.Quest;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class MapRunFlowControllerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            Object createdObject = createdObjects[i];
            if (createdObject == null)
            {
                continue;
            }

            if (createdObject is GameObject gameObject && gameObject.TryGetComponent(out MapRunFlowController controller))
            {
                InvokeMethod(controller, "OnDisable");
            }

            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void PlayerDiedEvent_CreatesDefeatSettlementSnapshotWithoutImmediateReturn()
    {
        MapRunFlowController controller = CreateController(out PlayerPlaneMovement playerMovement, out PlayerHealth playerHealth, out _, out _, out _, out _, out _, out _, out _);
        InvokeMethod(controller, "OnEnable");
        SetPrivateField(controller, "currentState", RunFlowState.InCombat);

        EventManager.eventBus.Publish(new PlayerDiedEvent(playerHealth, 25f, 0f, 100f));

        Assert.That(controller.CurrentState, Is.EqualTo(RunFlowState.ShowingSettlement));
        Assert.That(controller.TryGetSettlementSnapshot(out SettlementSnapshot snapshot), Is.True);
        Assert.That(snapshot.Outcome, Is.EqualTo(SettlementOutcome.Defeat));
        Assert.That(snapshot.CompletedWaveCount, Is.EqualTo(0));
        Assert.That(playerMovement.transform.position, Is.Not.EqualTo(Vector3.zero), "Defeat should not immediately return player to StartRoom.");
    }

    [Test]
    public void CombatVictoryEvent_AggregatesEnemyBossAndRewardCountsIntoSettlementSnapshot()
    {
        MapRunFlowController controller = CreateController(out _, out _, out _, out _, out _, out _, out _, out _, out _);
        InvokeMethod(controller, "OnEnable");
        SetPrivateField(controller, "currentState", RunFlowState.InCombat);

        EventManager.eventBus.Publish(new EnemyDiedEvent(null, "野狗"));
        EventManager.eventBus.Publish(new EnemyDiedEvent(null, "野狗"));
        EventManager.eventBus.Publish(new EnemyDiedEvent(null, "猪"));
        EventManager.eventBus.Publish(new BossEncounterEndedEvent(null, "Boss", endedByDeath: true));
        EventManager.eventBus.Publish(new RunRewardCollectedEvent("残卷", 3));
        EventManager.eventBus.Publish(new CombatVictoryEvent(null, 2));

        Assert.That(controller.CurrentState, Is.EqualTo(RunFlowState.ShowingSettlement));
        Assert.That(controller.TryGetSettlementSnapshot(out SettlementSnapshot snapshot), Is.True);
        Assert.That(snapshot.Outcome, Is.EqualTo(SettlementOutcome.Victory));
        Assert.That(snapshot.CompletedWaveCount, Is.EqualTo(2));
        Assert.That(snapshot.DefeatedBossCount, Is.EqualTo(1));
        Assert.That(snapshot.HarvestEntries.Count, Is.EqualTo(1));
        Assert.That(snapshot.HarvestEntries[0].DisplayName, Is.EqualTo("残卷"));
        Assert.That(snapshot.HarvestEntries[0].Count, Is.EqualTo(3));
        Assert.That(snapshot.SummaryEntries.Count, Is.EqualTo(2));
        Assert.That(snapshot.SummaryEntries[0].DisplayName, Is.EqualTo("野狗"));
        Assert.That(snapshot.SummaryEntries[0].Count, Is.EqualTo(2));
        Assert.That(snapshot.SummaryEntries[1].DisplayName, Is.EqualTo("猪"));
        Assert.That(snapshot.SummaryEntries[1].Count, Is.EqualTo(1));
    }

    [Test]
    public void TryReturnToStartRoomAndResetRun_ClearsRuntimeStateAndRestoresPlayerDefaults()
    {
        CoreTokenData startingToken = CreateToken<CoreTokenData>("start", "Start");
        ResultTokenData combatToken = CreateToken<ResultTokenData>("combat", "Combat");
        MapRunFlowController controller = CreateController(
            out PlayerPlaneMovement playerMovement,
            out PlayerHealth playerHealth,
            out PlayerBulletTokenInventory inventory,
            out SpellBookLoadout loadout,
            out Transform runtimeEnemyContainer,
            out Transform runtimePickupContainer,
            out _,
            out _,
            out _,
            startingToken);

        SetPrivateField(controller, "currentState", RunFlowState.ShowingSettlement);
        SetPrivateField(playerHealth, "currentHealth", 15f);
        inventory.Clear();
        Assert.That(inventory.TryAddItem(combatToken, out _), Is.True);
        loadout.SetItems(new[] { combatToken });

        CreateGameObject("RuntimeEnemy", runtimeEnemyContainer);
        CreateGameObject("RuntimePickup", runtimePickupContainer);

        bool success = controller.TryReturnToStartRoomAndResetRun(out string error);

        Assert.That(success, Is.True, error);
        Assert.That(controller.CurrentState, Is.EqualTo(RunFlowState.InStartRoom));
        Assert.That(runtimeEnemyContainer.childCount, Is.EqualTo(0));
        Assert.That(runtimePickupContainer.childCount, Is.EqualTo(0));
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth).Within(0.0001f));
        Assert.That(inventory.GetCell(0).item, Is.SameAs(startingToken));
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(startingToken));
        Assert.That(controller.TryGetSettlementSnapshot(out _), Is.False);
        Assert.That(playerMovement.transform.position.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(playerMovement.transform.position.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void TryResolveWaveRewardSelectionLibrary_UsesSelectionPlan()
    {
        CoreTokenData rewardToken = CreateToken<CoreTokenData>("reward", "Reward");
        BulletTokenLibrary rewardLibrary = CreateLibrary("RewardLibrary", rewardToken);
        CombatEntryTokenSelectionPlan selectionPlan = CreateSelectionPlan(rewardLibrary);
        MapRunFlowController controller = CreateController(out _, out _, out _, out _, out _, out _, out _, out _, out _);

        bool success = InvokeMethodWithOutArgument(controller, "TryResolveWaveRewardSelectionLibrary", selectionPlan, out BulletTokenLibrary resolvedLibrary);

        Assert.That(success, Is.True);
        Assert.That(resolvedLibrary, Is.SameAs(rewardLibrary));
    }

    [Test]
    public void TryResolveInitialCombatSelectionLibrary_UsesControllerPlan()
    {
        CoreTokenData rewardToken = CreateToken<CoreTokenData>("initial_reward", "Initial Reward");
        BulletTokenLibrary rewardLibrary = CreateLibrary("InitialRewardLibrary", rewardToken);
        CombatEntryTokenSelectionPlan selectionPlan = CreateSelectionPlan(rewardLibrary);
        MapRunFlowController controller = CreateController(out _, out _, out _, out _, out _, out _, out _, out _, out _);
        SetPrivateField(controller, "initialCombatTokenSelectionPlan", selectionPlan);

        bool success = InvokeMethodWithOutResult(controller, "TryResolveInitialCombatSelectionLibrary", out BulletTokenLibrary resolvedLibrary);

        Assert.That(success, Is.True);
        Assert.That(resolvedLibrary, Is.SameAs(rewardLibrary));
    }

    [Test]
    public void TryResolveWaveRewardSelectionLibraries_UsesSpellBookSelectionPlan()
    {
        SpellBookData wideBook = CreateSpellBook("wide", "Wide Spellbook", 7, 0.4f);
        SpellBookRewardLibrary spellBookLibrary = CreateSpellBookLibrary("SpellBookRewardLibrary", wideBook);
        CombatEntryTokenSelectionPlan selectionPlan = CreateSpellBookSelectionPlan(spellBookLibrary);
        MapRunFlowController controller = CreateController(out _, out _, out _, out _, out _, out _, out _, out _, out _);

        bool success = InvokeMethodWithTwoOutArguments(
            controller,
            "TryResolveWaveRewardSelectionLibraries",
            selectionPlan,
            out BulletTokenLibrary resolvedTokenLibrary,
            out SpellBookRewardLibrary resolvedSpellBookLibrary);

        Assert.That(success, Is.True);
        Assert.That(resolvedTokenLibrary, Is.Null);
        Assert.That(resolvedSpellBookLibrary, Is.SameAs(spellBookLibrary));
    }

    [Test]
    public void TryAddSelectedTokenToInventory_AddsTokenToInventory()
    {
        CoreTokenData startingToken = CreateToken<CoreTokenData>("start", "Start");
        ResultTokenData selectedToken = CreateToken<ResultTokenData>("selected", "Selected");
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out PlayerBulletTokenInventory inventory,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            startingToken);
        List<RewardNotificationEvent> notifications = new();
        System.IDisposable subscription = EventManager.eventBus.Subscribe<RewardNotificationEvent>(notifications.Add);

        try
        {
            bool success = InvokeMethodWithOutArgument(controller, "TryAddSelectedTokenToInventory", selectedToken, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(error, Is.Null.Or.Empty);
            Assert.That(ContainsItem(inventory, startingToken), Is.True);
            Assert.That(ContainsItem(inventory, selectedToken), Is.True);
            Assert.That(notifications.Count, Is.EqualTo(1));
            Assert.That(notifications[0].title, Is.EqualTo("Selected"));
            Assert.That(notifications[0].description, Is.EqualTo("已收入背包"));
            Assert.That(notifications[0].kind, Is.EqualTo(RewardNotificationKind.Token));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    [Test]
    public void TryEquipSelectedSpellBook_UpdatesPlayerSpellBookLoadout()
    {
        SpellBookData quickBook = CreateSpellBook("quick", "Quick Spellbook", 4, 0.12f);
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out _,
            out SpellBookLoadout spellBookLoadout,
            out _,
            out _,
            out _,
            out _,
            out _);
        List<RewardNotificationEvent> notifications = new();
        System.IDisposable subscription = EventManager.eventBus.Subscribe<RewardNotificationEvent>(notifications.Add);

        try
        {
            bool success = InvokeMethodWithOutArgument(controller, "TryEquipSelectedSpellBook", quickBook, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(error, Is.Null.Or.Empty);
            Assert.That(spellBookLoadout.SpellBook, Is.SameAs(quickBook));
            Assert.That(spellBookLoadout.SlotCount, Is.EqualTo(4));
            Assert.That(spellBookLoadout.CastCooldownSeconds, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(notifications.Count, Is.EqualTo(1));
            Assert.That(notifications[0].title, Is.EqualTo("Quick Spellbook"));
            Assert.That(notifications[0].description, Does.Contain("4 槽"));
            Assert.That(notifications[0].kind, Is.EqualTo(RewardNotificationKind.SpellBook));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    [Test]
    public void TryApplySelectedReward_WithSpellBook_UpdatesPlayerSpellBookLoadout()
    {
        SpellBookData wideBook = CreateSpellBook("wide", "Wide Spellbook", 7, 0.4f);
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out _,
            out SpellBookLoadout spellBookLoadout,
            out _,
            out _,
            out _,
            out _,
            out _);

        bool success = InvokeMethodWithOutArgument(
            controller,
            "TryApplySelectedReward",
            RunRewardOption.FromSpellBook(wideBook),
            out string error);

        Assert.That(success, Is.True, error);
        Assert.That(error, Is.Null.Or.Empty);
        Assert.That(spellBookLoadout.SpellBook, Is.SameAs(wideBook));
        Assert.That(spellBookLoadout.SlotCount, Is.EqualTo(7));
    }

    [Test]
    public void TryReturnToStartRoomAndResetRun_DoesNotAddTutorialReturnToken()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(saveService.SetStoryFlag(TutorialQuestConstants.TeleporterTriggeredFlagId, true), Is.True);

        CoreTokenData startingToken = CreateToken<CoreTokenData>("start", "Start");
        CoreTokenData tutorialReturnToken = CreateToken<CoreTokenData>("init_return", "InitReturn");
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out PlayerBulletTokenInventory inventory,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            startingToken);

        SetPrivateField(controller, "tutorialReturnTokenOverride", tutorialReturnToken);
        SetPrivateField(controller, "currentState", RunFlowState.ShowingSettlement);

        bool success = controller.TryReturnToStartRoomAndResetRun(out string error);

        Assert.That(success, Is.True, error);
        Assert.That(ContainsItem(inventory, startingToken), Is.True);
        Assert.That(ContainsItem(inventory, tutorialReturnToken), Is.False);
    }

    [Test]
    public void TryGrantTutorialEntryTokenAfterTeleporterTriggered_AddsTutorialReturnToken()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(saveService.SetStoryFlag(TutorialQuestConstants.TeleporterTriggeredFlagId, true), Is.True);

        CoreTokenData startingToken = CreateToken<CoreTokenData>("start", "Start");
        CoreTokenData tutorialReturnToken = CreateToken<CoreTokenData>("init_return", "InitReturn");
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out PlayerBulletTokenInventory inventory,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            startingToken);

        SetPrivateField(controller, "tutorialReturnTokenOverride", tutorialReturnToken);
        List<RewardNotificationEvent> notifications = new();
        System.IDisposable subscription = EventManager.eventBus.Subscribe<RewardNotificationEvent>(notifications.Add);

        try
        {
            controller.TryGrantTutorialEntryTokenAfterTeleporterTriggered();

            Assert.That(ContainsItem(inventory, startingToken), Is.True);
            Assert.That(ContainsItem(inventory, tutorialReturnToken), Is.True);
            Assert.That(notifications.Count, Is.EqualTo(1));
            Assert.That(notifications[0].title, Is.EqualTo("InitReturn"));
            Assert.That(notifications[0].kind, Is.EqualTo(RewardNotificationKind.Token));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    [Test]
    public void TryGrantTutorialEntryTokenAfterTeleporterTriggered_WithoutTeleporterTriggeredFlag_DoesNotAddTutorialReturnToken()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        CoreTokenData startingToken = CreateToken<CoreTokenData>("start", "Start");
        CoreTokenData tutorialReturnToken = CreateToken<CoreTokenData>("init_return", "InitReturn");
        MapRunFlowController controller = CreateController(
            out _,
            out _,
            out PlayerBulletTokenInventory inventory,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            startingToken);

        SetPrivateField(controller, "tutorialReturnTokenOverride", tutorialReturnToken);

        controller.TryGrantTutorialEntryTokenAfterTeleporterTriggered();

        Assert.That(ContainsItem(inventory, startingToken), Is.True);
        Assert.That(ContainsItem(inventory, tutorialReturnToken), Is.False);
    }

    private MapRunFlowController CreateController(
        out PlayerPlaneMovement playerMovement,
        out PlayerHealth playerHealth,
        out PlayerBulletTokenInventory inventory,
        out SpellBookLoadout loadout,
        out Transform runtimeEnemyContainer,
        out Transform runtimePickupContainer,
        out MapGridAuthoring startRoomMap,
        out MapGridAuthoring combatMap,
        out WaveManager waveManager,
        params PlaceableTokenData[] startingLoadoutItems)
    {
        GameObject playerObject = CreateGameObject("Player");
        playerMovement = playerObject.AddComponent<PlayerPlaneMovement>();
        Rigidbody rigidbody = playerObject.AddComponent<Rigidbody>();
        playerObject.AddComponent<BoxCollider>();
        SetPrivateField(playerMovement, "targetRigidbody", rigidbody);
        playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 100f);
        SetPrivateField(playerHealth, "currentHealth", 100f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        inventory = playerObject.AddComponent<PlayerBulletTokenInventory>();
        SetPrivateField(inventory, "startingTokens", new List<PlaceableTokenData>(startingLoadoutItems ?? System.Array.Empty<PlaceableTokenData>()));
        inventory.EnsureInitialized();

        loadout = playerObject.AddComponent<SpellBookLoadout>();
        SetPrivateField(loadout, "equippedItems", new List<PlaceableTokenData>(startingLoadoutItems ?? System.Array.Empty<PlaceableTokenData>()));
        InvokeMethod(loadout, "Awake");

        GameObject startRoomRoot = CreateGameObject("StartRoomMapRoot");
        startRoomMap = startRoomRoot.AddComponent<MapGridAuthoring>();
        ConfigureMap(startRoomMap);

        GameObject combatRoot = CreateGameObject("CombatMapRoot");
        combatMap = combatRoot.AddComponent<MapGridAuthoring>();
        ConfigureMap(combatMap);
        ArenaSeedMapGenerator combatSeedGenerator = combatRoot.AddComponent<ArenaSeedMapGenerator>();

        GameObject enemyRoot = CreateGameObject("Enemy");
        runtimeEnemyContainer = CreateGameObject("RuntimeEnemies", enemyRoot.transform).transform;
        EnemyGenerator enemyGenerator = enemyRoot.AddComponent<EnemyGenerator>();

        GameObject pickupAuthoring = CreateGameObject("BulletTokenPickup_Authoring");
        pickupAuthoring.AddComponent<SphereCollider>();
        pickupAuthoring.AddComponent<BulletTokenPickup>();
        runtimePickupContainer = CreateGameObject("RuntimePickups", pickupAuthoring.transform).transform;

        GameObject waveManagerObject = CreateGameObject("WaveManager");
        waveManager = waveManagerObject.AddComponent<WaveManager>();

        GameObject controllerObject = CreateGameObject("MapRunFlowController");
        MapRunFlowController controller = controllerObject.AddComponent<MapRunFlowController>();
        SetPrivateField(controller, "targetPlayer", playerMovement);
        SetPrivateField(controller, "startRoomMapGrid", startRoomMap);
        SetPrivateField(controller, "combatMapGrid", combatMap);
        SetPrivateField(controller, "combatSeedGenerator", combatSeedGenerator);
        SetPrivateField(controller, "enemyGenerator", enemyGenerator);
        SetPrivateField(controller, "waveManager", waveManager);
        SetPrivateField(controller, "runtimeEnemyContainer", runtimeEnemyContainer);
        SetPrivateField(controller, "runtimePickupContainer", runtimePickupContainer);

        playerObject.transform.position = new Vector3(5f, 0f, 5f);
        return controller;
    }

    private void ConfigureMap(MapGridAuthoring mapAuthoring)
    {
        mapAuthoring.GridWidth = 2;
        mapAuthoring.GridHeight = 2;
        mapAuthoring.CellSize = Vector2.one;

        List<CellEntry> entries = new();
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                GameObject cell = CreateGameObject($"Cell_{x}_{y}");
                GameObject modelRoot = CreateGameObject("Model", cell.transform);
                CreateGameObject("wall Model", modelRoot.transform).SetActive(false);
                CreateGameObject("Ground Model", modelRoot.transform);

                BoxCollider wallCollider = cell.AddComponent<BoxCollider>();
                wallCollider.size = new Vector3(20f, 20f, 20f);
                wallCollider.center = new Vector3(0f, 10f, 0f);

                BoxCollider groundCollider = cell.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(20f, 1f, 20f);
                groundCollider.center = new Vector3(0f, -0.5f, 0f);

                Rigidbody rigidbody = cell.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;

                CellData cellData = cell.AddComponent<CellData>();
                cellData.SetCoordinates(new Vector2Int(x, y));
                Assert.That(cellData.TryRefreshSurfacePresentation(), Is.True);

                cell.transform.position = mapAuthoring.GetCellWorldPosition(x, y);
                entries.Add(new CellEntry(x, y, cell));
            }
        }

        mapAuthoring.ReplaceCellEntries(entries);
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private BulletTokenLibrary CreateLibrary(string name, params PlaceableTokenData[] tokens)
    {
        BulletTokenLibrary library = ScriptableObject.CreateInstance<BulletTokenLibrary>();
        library.name = name;
        library.SetTokens(tokens);
        createdObjects.Add(library);
        return library;
    }

    private CombatEntryTokenSelectionPlan CreateSelectionPlan(params BulletTokenLibrary[] libraries)
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        for (int i = 0; i < libraries.Length; i++)
        {
            plan.AddLibrary(libraries[i], 1f);
        }

        return plan;
    }

    private CombatEntryTokenSelectionPlan CreateSpellBookSelectionPlan(params SpellBookRewardLibrary[] libraries)
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        for (int i = 0; i < libraries.Length; i++)
        {
            plan.AddSpellBookLibrary(libraries[i], 1f);
        }

        return plan;
    }

    private SpellBookRewardLibrary CreateSpellBookLibrary(string name, params SpellBookData[] spellBooks)
    {
        SpellBookRewardLibrary library = ScriptableObject.CreateInstance<SpellBookRewardLibrary>();
        library.name = name;
        library.SetSpellBooks(spellBooks);
        createdObjects.Add(library);
        return library;
    }

    private SpellBookData CreateSpellBook(string spellBookId, string displayName, int slotCount, float castCooldownSeconds)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = displayName;
        spellBook.SlotCount = slotCount;
        spellBook.CastCooldownSeconds = castCooldownSeconds;
        spellBook.CastsPerActivation = 1;
        spellBook.name = spellBookId;
        createdObjects.Add(spellBook);
        return spellBook;
    }

    private GameObject CreateGameObject(string name, Transform parent = null)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = CreateGameObject("RuntimeSaveService");
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokeMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static bool InvokeMethodWithOutArgument<TInput, TOutput>(object target, string methodName, TInput input, out TOutput output)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        object[] arguments = { input, null };
        bool result = (bool)method.Invoke(target, arguments);
        output = arguments[1] is TOutput typedOutput ? typedOutput : default;
        return result;
    }

    private static bool InvokeMethodWithOutResult<TOutput>(object target, string methodName, out TOutput output)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        object[] arguments = { null };
        bool result = (bool)method.Invoke(target, arguments);
        output = arguments[0] is TOutput typedOutput ? typedOutput : default;
        return result;
    }

    private static bool InvokeMethodWithTwoOutArguments<TInput, TOutputA, TOutputB>(
        object target,
        string methodName,
        TInput input,
        out TOutputA outputA,
        out TOutputB outputB)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        object[] arguments = { input, null, null };
        bool result = (bool)method.Invoke(target, arguments);
        outputA = arguments[1] is TOutputA typedOutputA ? typedOutputA : default;
        outputB = arguments[2] is TOutputB typedOutputB ? typedOutputB : default;
        return result;
    }

    private static bool ContainsItem(PlayerBulletTokenInventory inventory, PlaceableTokenData token)
    {
        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            if (inventory.GetCell(i).item == token)
            {
                return true;
            }
        }

        return false;
    }

    private static FieldInfo FindInstanceField(System.Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static MethodInfo FindInstanceMethod(System.Type type, string methodName)
    {
        while (type != null)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
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
}
