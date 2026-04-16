using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class WaveManagerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void CompleteSequence_RaisesSequenceCompletedOnceAndUpdatesState()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        int completionCount = 0;
        waveManager.SequenceCompleted += () => completionCount++;
        SetPrivateField(waveManager, "isSequenceRunning", true);
        SetPrivateField(waveManager, "hasCompletedSequence", false);

        InvokePrivateMethod<object>(waveManager, "CompleteSequence");
        InvokePrivateMethod<object>(waveManager, "CompleteSequence");

        Assert.That(completionCount, Is.EqualTo(1));
        Assert.That(waveManager.IsSequenceRunning, Is.False);
        Assert.That(waveManager.HasCompletedSequence, Is.True);
    }

    [Test]
    public void TryStartBossEncounter_PublishesBossLifecycleEvents()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        BaseCharEnemyNorm1 boss = CreateGameObject("Boss").AddComponent<BaseCharEnemyNorm1>();
        SetPrivateField(boss, "health", 120f);
        SetPrivateField(boss, "currentHealth", 120f);
        SetPrivateField(boss, "hasInitializedHealth", true);

        BossEncounterStartedEvent? startedEvent = null;
        BossEncounterEndedEvent? endedEvent = null;
        System.IDisposable startSubscription = EventManager.eventBus.Subscribe<BossEncounterStartedEvent>(evt => startedEvent = evt);
        System.IDisposable endSubscription = EventManager.eventBus.Subscribe<BossEncounterEndedEvent>(evt => endedEvent = evt);

        try
        {
            WaveEnemySpawnEntry bossEntry = new(
                enemyDefinition: null,
                spawnCount: 1,
                tokenDrops: null,
                isBossEncounter: true,
                bossDisplayNameOverride: "Final Boss");

            InvokePrivateMethod<object>(waveManager, "TryStartBossEncounter", bossEntry, boss);

            Assert.That(startedEvent.HasValue, Is.True);
            Assert.That(startedEvent.Value.boss, Is.SameAs(boss));
            Assert.That(startedEvent.Value.displayName, Is.EqualTo("Final Boss"));
            Assert.That(startedEvent.Value.maxHealth, Is.EqualTo(120f));

            bool damageApplied = boss.TryApplyDamage(120f, out _, out bool isDead);

            Assert.That(damageApplied, Is.True);
            Assert.That(isDead, Is.True);
            Assert.That(endedEvent.HasValue, Is.True);
            Assert.That(endedEvent.Value.boss, Is.SameAs(boss));
            Assert.That(endedEvent.Value.displayName, Is.EqualTo("Final Boss"));
            Assert.That(endedEvent.Value.endedByDeath, Is.True);
        }
        finally
        {
            startSubscription.Dispose();
            endSubscription.Dispose();
        }
    }

    [Test]
    public void TryStartBossEncounter_AttachesBossPhaseControllerAndSwitchesDefinitionAtThreshold()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        GameObject bossObject = CreateGameObject("Boss");
        BaseCharEnemyNorm1 boss = bossObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyDefinitionBinder binder = bossObject.AddComponent<EnemyDefinitionBinder>();
        SetPrivateField(boss, "health", 120f);
        SetPrivateField(boss, "currentHealth", 120f);
        SetPrivateField(boss, "hasInitializedHealth", true);

        EnemyDefinition phaseOneDefinition = CreateEnemyDefinition("BossPhaseOne", runtimePrefab: null, glyphText: "\u58f9");
        SetPrivateField(phaseOneDefinition, "movementKind", EnemyMovementKind.None);
        SetPrivateField(phaseOneDefinition, "attackKind", EnemyAttackKind.None);
        EnemyDefinition phaseTwoDefinition = CreateEnemyDefinition("BossPhaseTwo", runtimePrefab: null, glyphText: "\u8d30");
        SetPrivateField(phaseTwoDefinition, "movementKind", EnemyMovementKind.None);
        SetPrivateField(phaseTwoDefinition, "attackKind", EnemyAttackKind.None);

        Assert.That(binder.ApplyDefinition(phaseOneDefinition), Is.True);

        WaveEnemySpawnEntry bossEntry = new(
            enemyDefinition: phaseOneDefinition,
            spawnCount: 1,
            tokenDrops: null,
            isBossEncounter: true,
            bossDisplayNameOverride: "Final Boss",
            bossPhaseTwoDefinition: phaseTwoDefinition,
            bossPhaseTransitionHealthRatio: 0.5f);

        InvokePrivateMethod<object>(waveManager, "TryStartBossEncounter", bossEntry, boss);

        BossPhaseController phaseController = boss.GetComponent<BossPhaseController>();
        Assert.That(phaseController, Is.Not.Null);

        bool didDamage = boss.TryApplyDamage(60f, out _, out _);

        Assert.That(didDamage, Is.True);
        Assert.That(boss.Definition, Is.SameAs(phaseTwoDefinition));
    }

    [Test]
    public void CompleteSequence_PublishesCombatVictoryEventWithCompletedWaveCount()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        CombatVictoryEvent? victoryEvent = null;
        System.IDisposable subscription = EventManager.eventBus.Subscribe<CombatVictoryEvent>(evt => victoryEvent = evt);

        try
        {
            SetPrivateField(waveManager, "isSequenceRunning", true);
            SetPrivateField(waveManager, "hasCompletedSequence", false);
            SetPrivateField(waveManager, "completedWaveCount", 3);

            InvokePrivateMethod<object>(waveManager, "CompleteSequence");
            InvokePrivateMethod<object>(waveManager, "CompleteSequence");

            Assert.That(victoryEvent.HasValue, Is.True);
            Assert.That(victoryEvent.Value.source, Is.SameAs(waveManager));
            Assert.That(victoryEvent.Value.completedWaveCount, Is.EqualTo(3));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    [Test]
    public void TryStartSequence_ResetsEnemyGeneratorCompletedWaveCount()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        EnemyDefinition enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(enemyDefinition);
        WaveDefinition wave = CreateWaveDefinition(0.5f, null, new WaveEnemySpawnEntry(enemyDefinition, 1));

        Assert.That(generator.TrySetCompletedWaveCount(3), Is.True);
        SetPrivateField(waveManager, "enemyGenerator", generator);
        SetPrivateField(waveManager, "waves", new List<WaveDefinition> { wave });
        SetPrivateField(waveManager, "autoStartOnEnable", false);

        bool started = waveManager.TryStartSequence();

        Assert.That(started, Is.True);
        Assert.That(waveManager.CompletedWaveCount, Is.EqualTo(0));
        Assert.That(generator.CompletedWaveCount, Is.EqualTo(0));
    }

    [Test]
    public void TryAdvanceWave_PausesForRewardSelectionUntilResumed()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        CombatEntryTokenSelectionPlan selectionPlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(selectionPlan);

        EnemyDefinition enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(enemyDefinition);

        WaveDefinition firstWave = CreateWaveDefinition(
            0.5f,
            selectionPlan,
            new WaveEnemySpawnEntry(enemyDefinition, 1));
        WaveDefinition secondWave = CreateWaveDefinition(
            0.5f,
            null,
            new WaveEnemySpawnEntry(enemyDefinition, 1));

        SetPrivateField(waveManager, "enemyGenerator", generator);
        SetPrivateField(waveManager, "waves", new List<WaveDefinition> { firstWave, secondWave });
        SetPrivateField(waveManager, "currentWaveIndex", 0);
        SetPrivateField(waveManager, "spawnedCountInCurrentWave", firstWave.TotalSpawnCount);
        SetPrivateField(waveManager, "nextWaveStartTime", 0f);
        SetPrivateField(waveManager, "isSequenceRunning", true);
        SetPrivateField(waveManager, "hasCompletedSequence", false);

        int requestedWaveIndex = -1;
        WaveDefinition requestedWave = null;
        CombatEntryTokenSelectionPlan requestedSelectionPlan = null;
        waveManager.WaveRewardSelectionRequested += (waveIndex, wave, selection) =>
        {
            requestedWaveIndex = waveIndex;
            requestedWave = wave;
            requestedSelectionPlan = selection;
        };

        InvokePrivateMethod<object>(waveManager, "TryAdvanceWave", firstWave, 1f);

        Assert.That(requestedWaveIndex, Is.EqualTo(0));
        Assert.That(requestedWave, Is.SameAs(firstWave));
        Assert.That(requestedSelectionPlan, Is.SameAs(selectionPlan));
        Assert.That(waveManager.CompletedWaveCount, Is.EqualTo(1));
        Assert.That(generator.CompletedWaveCount, Is.EqualTo(1));
        Assert.That(waveManager.IsAwaitingWaveRewardSelection, Is.True);

        bool resumed = waveManager.TryContinueAfterWaveRewardSelection();

        Assert.That(resumed, Is.True);
        Assert.That(waveManager.IsAwaitingWaveRewardSelection, Is.False);
        Assert.That(GetPrivateField<int>(waveManager, "currentWaveIndex"), Is.EqualTo(1));
        Assert.That(waveManager.HasCompletedSequence, Is.False);
    }

    [Test]
    public void TryAdvanceWave_NonBossRewardSelectionPlanUsesSequenceProgression()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();

        CombatEntryTokenSelectionPlan legacyWavePlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        CombatEntryTokenSelectionPlan sequenceWavePlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(legacyWavePlan);
        createdObjects.Add(sequenceWavePlan);

        EnemyDefinition enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(enemyDefinition);

        WaveDefinition firstWave = CreateWaveDefinition(
            0.5f,
            legacyWavePlan,
            new WaveEnemySpawnEntry(enemyDefinition, 1));

        WaveSequenceProgressionConfig progressionConfig = ScriptableObject.CreateInstance<WaveSequenceProgressionConfig>();
        createdObjects.Add(progressionConfig);
        SetPrivateField(
            progressionConfig,
            "rewardsByWave",
            new List<WaveSequenceProgressionConfig.WaveRewardEntry>
            {
                new()
                {
                    waveNumber = 1,
                    tokenDrops = new List<EnemyBulletTokenDropEntry>(),
                    postWaveTokenSelectionPlan = sequenceWavePlan,
                },
            });

        SetPrivateField(waveManager, "enemyGenerator", generator);
        SetPrivateField(waveManager, "waves", new List<WaveDefinition> { firstWave });
        SetPrivateField(waveManager, "nonBossWaveSequenceProgression", progressionConfig);
        SetPrivateField(waveManager, "currentWaveIndex", 0);
        SetPrivateField(waveManager, "spawnedCountInCurrentWave", firstWave.TotalSpawnCount);
        SetPrivateField(waveManager, "nextWaveStartTime", 0f);
        SetPrivateField(waveManager, "isSequenceRunning", true);
        SetPrivateField(waveManager, "hasCompletedSequence", false);

        CombatEntryTokenSelectionPlan requestedSelectionPlan = null;
        waveManager.WaveRewardSelectionRequested += (_, _, selectionPlan) =>
        {
            requestedSelectionPlan = selectionPlan;
        };

        InvokePrivateMethod<object>(waveManager, "TryAdvanceWave", firstWave, 1f);

        Assert.That(requestedSelectionPlan, Is.SameAs(sequenceWavePlan));
        Assert.That(requestedSelectionPlan, Is.Not.SameAs(legacyWavePlan));
    }

    [Test]
    public void TryAdvanceWave_BossRewardSelectionPlanKeepsWaveDefinitionPlan()
    {
        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();

        CombatEntryTokenSelectionPlan bossWavePlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        CombatEntryTokenSelectionPlan sequenceWavePlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(bossWavePlan);
        createdObjects.Add(sequenceWavePlan);

        EnemyDefinition enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(enemyDefinition);

        WaveDefinition bossWave = CreateWaveDefinition(
            0.5f,
            bossWavePlan,
            new WaveEnemySpawnEntry(enemyDefinition, 1));
        SetPrivateField(bossWave, "isBossWave", true);

        WaveSequenceProgressionConfig progressionConfig = ScriptableObject.CreateInstance<WaveSequenceProgressionConfig>();
        createdObjects.Add(progressionConfig);
        SetPrivateField(
            progressionConfig,
            "rewardsByWave",
            new List<WaveSequenceProgressionConfig.WaveRewardEntry>
            {
                new()
                {
                    waveNumber = 1,
                    tokenDrops = new List<EnemyBulletTokenDropEntry>(),
                    postWaveTokenSelectionPlan = sequenceWavePlan,
                },
            });

        SetPrivateField(waveManager, "enemyGenerator", generator);
        SetPrivateField(waveManager, "waves", new List<WaveDefinition> { bossWave });
        SetPrivateField(waveManager, "nonBossWaveSequenceProgression", progressionConfig);
        SetPrivateField(waveManager, "currentWaveIndex", 0);
        SetPrivateField(waveManager, "spawnedCountInCurrentWave", bossWave.TotalSpawnCount);
        SetPrivateField(waveManager, "nextWaveStartTime", 0f);
        SetPrivateField(waveManager, "isSequenceRunning", true);
        SetPrivateField(waveManager, "hasCompletedSequence", false);

        CombatEntryTokenSelectionPlan requestedSelectionPlan = null;
        waveManager.WaveRewardSelectionRequested += (_, _, selectionPlan) =>
        {
            requestedSelectionPlan = selectionPlan;
        };

        InvokePrivateMethod<object>(waveManager, "TryAdvanceWave", bossWave, 1f);

        Assert.That(requestedSelectionPlan, Is.SameAs(bossWavePlan));
        Assert.That(requestedSelectionPlan, Is.Not.SameAs(sequenceWavePlan));
    }

    // [Test]
    // public void Tick_SpawnsWaveByQuotaAndAdvancesAfterEnemiesAreCleared()
    // {
    //     CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);

    //     GameObject playerObject = CreateGameObject("Player");
    //     playerObject.transform.position = new Vector3(32f, 0f, 32f);

    //     GameObject enemyParent = CreateGameObject("SpawnedEnemies");
    //     GameObject sharedEnemyPrefab = CreateEnemyPrefab();
    //     EnemyDefinition basicDefinition = CreateEnemyDefinition("BasicEnemy", sharedEnemyPrefab.GetComponent<EnemyDefinitionBinder>(), "甲");
    //     EnemyDefinition fastDefinition = CreateEnemyDefinition("FastEnemy", sharedEnemyPrefab.GetComponent<EnemyDefinitionBinder>(), "迅");

    //     EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
    //     Assert.That(generator.TrySetTarget(playerObject.transform), Is.True);
    //     SetPrivateField(generator, "spawnDistance", 4f);
    //     SetPrivateField(generator, "maxGroundSpawnRolls", 8);
    //     SetPrivateField(generator, "spawnedEnemyParent", enemyParent.transform);

    //     WaveDefinition waveOne = CreateWaveDefinition(
    //         0.5f,
    //         new WaveEnemySpawnEntry(basicDefinition, 1, new EnemyWaveConfig(40f, 80f, 10f, 1f, 2f)),
    //         new WaveEnemySpawnEntry(fastDefinition, 2, new EnemyWaveConfig(60f, 140f, 12f, 0.75f, 4f)));
    //     WaveDefinition waveTwo = CreateWaveDefinition(
    //         0.5f,
    //         new WaveEnemySpawnEntry(fastDefinition, 1, new EnemyWaveConfig(75f, 160f, 14f, 0.5f, 6f)));

    //     WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
    //     SetPrivateField(waveManager, "enemyGenerator", generator);
    //     SetPrivateField(waveManager, "waves", new List<WaveDefinition> { waveOne, waveTwo });
    //     SetPrivateField(waveManager, "autoStartOnEnable", false);
    //     SetPrivateField(waveManager, "interWaveDelay", 1f);

    //     Assert.That(waveManager.TryStartSequence(), Is.True);

    //     InvokePrivateMethod<object>(waveManager, "Tick", 0f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));
    //     Assert.That(enemyParent.transform.GetChild(0).GetComponent<Enemy>().EnemyName, Is.EqualTo("BasicEnemy"));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 0.25f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 0.5f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(2));
    //     Assert.That(enemyParent.transform.GetChild(1).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 1f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(3));
    //     Assert.That(enemyParent.transform.GetChild(2).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 1.25f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(3), "Current wave should not advance before active enemies are cleared.");

    //     DestroyAllChildrenImmediate(enemyParent.transform);
    //     InvokePrivateMethod<object>(waveManager, "Tick", 1.25f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 1.75f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 2.25f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 3.26f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));
    //     Assert.That(enemyParent.transform.GetChild(0).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

    //     DestroyAllChildrenImmediate(enemyParent.transform);
    //     InvokePrivateMethod<object>(waveManager, "Tick", 3.5f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

    //     InvokePrivateMethod<object>(waveManager, "Tick", 4.5f);
    //     InvokePrivateMethod<object>(waveManager, "Tick", 10f);
    //     Assert.That(enemyParent.transform.childCount, Is.EqualTo(0), "Completed sequence should stop spawning new enemies.");
    // }

    private MapGridAuthoring CreateMapAuthoring(int width, int height, Vector2 cellSize, string fillTag)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        MapGridAuthoring authoring = mapRoot.AddComponent<MapGridAuthoring>();
        authoring.GridWidth = width;
        authoring.GridHeight = height;
        authoring.CellSize = cellSize;

        var entries = new List<CellEntry>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cell = CreateGameObject($"Cell_{x}_{y}");
                cell.tag = fillTag;
                cell.transform.position = authoring.GetCellWorldPosition(x, y);
                entries.Add(new CellEntry(x, y, cell));
            }
        }

        authoring.ReplaceCellEntries(entries);
        return authoring;
    }

    private GameObject CreateEnemyPrefab()
    {
        GameObject enemyObject = CreateGameObject("EnemyPrefab");
        enemyObject.AddComponent<CharEnemyMovement>();
        enemyObject.AddComponent<EnemyMeleeAttacker>();
        enemyObject.AddComponent<EnemyDefinitionBinder>();
        enemyObject.AddComponent<BaseCharEnemyNorm1>();
        return enemyObject;
    }

    private EnemyDefinition CreateEnemyDefinition(string enemyId, EnemyDefinitionBinder runtimePrefab, string glyphText)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = enemyId;
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        SetPrivateField(definition, "runtimePrefab", runtimePrefab);
        SetPrivateField(definition, "movementKind", EnemyMovementKind.ChaseTarget);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.MeleeContact);
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = glyphText,
            glyphColor = Color.white,
            runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
            groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
        });
        return definition;
    }

    private WaveDefinition CreateWaveDefinition(float spawnIntervalSeconds, CombatEntryTokenSelectionPlan selectionPlan, params WaveEnemySpawnEntry[] enemySpawns)
    {
        WaveDefinition waveDefinition = ScriptableObject.CreateInstance<WaveDefinition>();
        createdObjects.Add(waveDefinition);
        SetPrivateField(waveDefinition, "spawnIntervalSeconds", spawnIntervalSeconds);
        SetPrivateField(waveDefinition, "postWaveTokenSelectionPlan", selectionPlan);
        SetPrivateField(waveDefinition, "enemySpawns", new List<WaveEnemySpawnEntry>(enemySpawns));
        return waveDefinition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private void DestroyAllChildrenImmediate(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        return field.GetValue(target) is T typedValue ? typedValue : default;
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (T)method.Invoke(target, args);
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
}
