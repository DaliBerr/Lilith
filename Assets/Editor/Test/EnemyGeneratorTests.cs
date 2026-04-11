using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyGeneratorTests
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
    public void TryGetSpawnPosition_ReturnsGroundTaggedCellOnly()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = authoring.GetCellWorldPosition(32, 32);

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TryGetSpawnPosition(out Vector3 spawnPosition);

        Assert.That(success, Is.True);
        Assert.That(authoring.TryGetCellCoordinateFromWorldPoint(spawnPosition, out Vector2Int coordinate), Is.True);
        Assert.That(authoring.TryGetCell(coordinate, out GameObject cellObject), Is.True);
        Assert.That(cellObject.CompareTag(MapGridAuthoring.GroundTagName), Is.True);
    }

    [Test]
    public void TryGetSpawnPositionAround_ReturnsGroundTaggedCellOnly()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TryGetSpawnPositionAround(authoring.GetCellWorldPosition(24, 24), 6f, out Vector3 spawnPosition);

        Assert.That(success, Is.True);
        Assert.That(authoring.TryGetCellCoordinateFromWorldPoint(spawnPosition, out Vector2Int coordinate), Is.True);
        Assert.That(authoring.TryGetCell(coordinate, out GameObject cellObject), Is.True);
        Assert.That(cellObject.CompareTag(MapGridAuthoring.GroundTagName), Is.True);
    }

    [Test]
    public void TryGetSpawnPosition_ReturnsFalseWhenNoGroundTaggedCellCanBeRolled()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.WallTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 4);

        bool success = generator.TryGetSpawnPosition(out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void TrySpawnEnemy_AppliesWaveConfigWithoutMutatingPrefab()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        GameObject enemyPrefab = CreateEnemyPrefab(100f);
        BaseCharEnemyNorm1 prefabEnemy = enemyPrefab.GetComponent<BaseCharEnemyNorm1>();
        EnemyDefinition definition = CreateEnemyDefinition("BasicEnemy", enemyPrefab.GetComponent<EnemyDefinitionBinder>());
        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        EnemyWaveConfig config = new(250f, 90f, 18f, 0.75f, 12f);

        bool success = generator.TrySpawnEnemy(definition, config, out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        Assert.That(spawnedEnemy, Is.TypeOf<BaseCharEnemyNorm1>());

        createdObjects.Add(spawnedEnemy.gameObject);
        BaseCharEnemyNorm1 spawnedBaseEnemy = (BaseCharEnemyNorm1)spawnedEnemy;
        Assert.That(spawnedBaseEnemy.EnemyName, Is.EqualTo("BasicEnemy"));
        Assert.That(spawnedBaseEnemy.MaxHealth, Is.EqualTo(250f));
        Assert.That(spawnedBaseEnemy.CurrentHealth, Is.EqualTo(250f));
        Assert.That(spawnedBaseEnemy.MoveSpeed, Is.EqualTo(90f));
        Assert.That(spawnedBaseEnemy.AttackRange, Is.EqualTo(18f));
        Assert.That(spawnedBaseEnemy.AttackCooldown, Is.EqualTo(0.75f));
        Assert.That(spawnedBaseEnemy.AttackDamage, Is.EqualTo(12f));
        Assert.That(spawnedBaseEnemy.StoppingDistance, Is.EqualTo(1f));

        Assert.That(prefabEnemy.MaxHealth, Is.EqualTo(100f));
        Assert.That(prefabEnemy.MoveSpeed, Is.EqualTo(120f));
        Assert.That(prefabEnemy.StoppingDistance, Is.EqualTo(1f));
        Assert.That(prefabEnemy.AttackRange, Is.EqualTo(0f));
        Assert.That(prefabEnemy.AttackCooldown, Is.EqualTo(0f));
        Assert.That(prefabEnemy.AttackDamage, Is.EqualTo(0f));
    }

    [Test]
    public void TrySpawnEnemy_UsesProvidedDefinitionRuntimePrefab()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        GameObject basicPrefab = CreateEnemyPrefab(100f);
        GameObject fastPrefab = CreateEnemyPrefab(80f);
        EnemyDefinition fastDefinition = CreateEnemyDefinition("FastEnemy", fastPrefab.GetComponent<EnemyDefinitionBinder>(), glyphText: "迅");

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        EnemyWaveConfig config = new(120f, 160f, 12f, 0.4f, 9f);

        bool success = generator.TrySpawnEnemy(fastDefinition, config, out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        Assert.That(spawnedEnemy.EnemyName, Is.EqualTo("FastEnemy"));
        Assert.That(spawnedEnemy.Definition, Is.SameAs(fastDefinition));
        Assert.That(spawnedEnemy.MoveSpeed, Is.EqualTo(160f));
        Assert.That(spawnedEnemy.AttackDamage, Is.EqualTo(9f));
        Assert.That(spawnedEnemy.gameObject.name, Does.Contain("EnemyPrefab"));
        Assert.That(spawnedEnemy.gameObject, Is.Not.SameAs(basicPrefab));
    }

    [Test]
    public void TrySpawnEnemy_AppliesWaveConfigToAllRootReceivers()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        GameObject enemyPrefab = CreateEnemyPrefab(100f);
        DummyWaveConfigReceiver dummyReceiver = enemyPrefab.AddComponent<DummyWaveConfigReceiver>();
        EnemyDefinition definition = CreateEnemyDefinition("DropEnemy", enemyPrefab.GetComponent<EnemyDefinitionBinder>());
        CoreTokenData droppedToken = CreateToken<CoreTokenData>("drop_fire", "Drop Fire");
        EnemyWaveConfig config = new(
            180f,
            95f,
            14f,
            0.8f,
            7f,
            new[]
            {
                new EnemyBulletTokenDropEntry(droppedToken, 0.6f),
            });

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TrySpawnEnemy(definition, config, out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        createdObjects.Add(spawnedEnemy.gameObject);

        DummyWaveConfigReceiver spawnedReceiver = spawnedEnemy.GetComponent<DummyWaveConfigReceiver>();
        Assert.That(spawnedReceiver, Is.Not.Null);
        Assert.That(spawnedReceiver.ApplyCount, Is.EqualTo(1));
        Assert.That(spawnedReceiver.LastConfig.maxHealth, Is.EqualTo(180f));
        Assert.That(spawnedReceiver.LastConfig.tokenDrops, Has.Count.EqualTo(1));
        Assert.That(spawnedReceiver.LastConfig.tokenDrops[0].token, Is.SameAs(droppedToken));
        Assert.That(spawnedReceiver.LastConfig.tokenDrops[0].dropChance, Is.EqualTo(0.6f));
        Assert.That(dummyReceiver.ApplyCount, Is.EqualTo(0), "Prefab receiver should not be mutated by spawned instance callbacks.");
    }

    [Test]
    public void TrySpawnEnemy_SnapsSpawnedEnemyRootToMapPlaneHeight()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName, 10f);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 40f, 32f);

        GameObject enemyPrefab = CreateEnemyPrefab(100f);
        EnemyDefinition definition = CreateEnemyDefinition("GroundedEnemy", enemyPrefab.GetComponent<EnemyDefinitionBinder>());
        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TrySpawnEnemy(definition, new EnemyWaveConfig(100f, 120f, 3f, 0.5f, 1f), out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        createdObjects.Add(spawnedEnemy.gameObject);
        Assert.That(spawnedEnemy.transform.position.y, Is.EqualTo(15f).Within(0.001f));
    }

    [Test]
    public void TrySpawnEnemyAt_BindsTargetsForMovementAndExtendedAttackers()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        GameObject playerObject = CreateGameObject("Player");
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        Transform player = playerObject.transform;
        player.position = new Vector3(32f, 0f, 32f);

        GameObject enemyPrefab = CreateEnemyPrefab(100f);
        EnemyDefinition definition = CreateEnemyDefinition("CasterEnemy", enemyPrefab.GetComponent<EnemyDefinitionBinder>());
        Assert.That(generator.TrySetTarget(player), Is.True);

        bool success = generator.TrySpawnEnemyAt(definition, new EnemyWaveConfig(100f, 120f, 14f, 0.75f, 6f), new Vector3(28f, 0f, 28f), out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        createdObjects.Add(spawnedEnemy.gameObject);
        Assert.That(GetPrivateField<Transform>(spawnedEnemy.GetComponent<CharEnemyMovement>(), "targetPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateField<Transform>(spawnedEnemy.GetComponent<EnemyMeleeAttacker>(), "targetPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateField<Transform>(spawnedEnemy.GetComponent<EnemyRangedTokenAttacker>(), "targetPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateField<PlayerHealth>(spawnedEnemy.GetComponent<EnemyRangedTokenAttacker>(), "targetPlayerHealth"), Is.SameAs(playerHealth));
        Assert.That(GetPrivateField<Transform>(spawnedEnemy.GetComponent<EnemySummoner>(), "targetPlayer"), Is.SameAs(player));
        Assert.That(GetPrivateField<EnemyGenerator>(spawnedEnemy.GetComponent<EnemySummoner>(), "enemyGenerator"), Is.SameAs(generator));
    }

    [Test]
    public void TrySpawnEnemyAt_BindsCombatMapAndPickupParentForSpawnedComponents()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform runtimeEnemyParent = CreateGameObject("RuntimeEnemies").transform;
        Transform runtimePickupParent = CreateGameObject("RuntimePickups").transform;
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        GameObject enemyPrefab = CreateEnemyPrefab(100f);
        enemyPrefab.AddComponent<EnemyBulletTokenDropper>();
        EnemyDefinition definition = CreateEnemyDefinition("DropEnemy", enemyPrefab.GetComponent<EnemyDefinitionBinder>());
        SetPrivateField(generator, "spawnedEnemyParent", runtimeEnemyParent);
        SetPrivateField(generator, "pickupParent", runtimePickupParent);

        Assert.That(generator.TrySetTarget(player), Is.True);
        Assert.That(generator.TrySetTargetMapGrid(authoring), Is.True);

        bool success = generator.TrySpawnEnemyAt(
            definition,
            new EnemyWaveConfig(100f, 120f, 14f, 0.75f, 6f),
            new Vector3(28f, 0f, 28f),
            out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        createdObjects.Add(spawnedEnemy.gameObject);
        Assert.That(spawnedEnemy.transform.parent, Is.SameAs(runtimeEnemyParent));
        Assert.That(GetPrivateField<MapGridAuthoring>(spawnedEnemy.GetComponent<CharEnemyMovement>(), "targetMapGrid"), Is.SameAs(authoring));
        Assert.That(GetPrivateField<MapGridAuthoring>(spawnedEnemy.GetComponent<EnemyBulletTokenDropper>(), "targetMapGrid"), Is.SameAs(authoring));
        Assert.That(GetPrivateField<Transform>(spawnedEnemy.GetComponent<EnemyBulletTokenDropper>(), "pickupParent"), Is.SameAs(runtimePickupParent));
    }

    [Test]
    public void TrySpawnEnemy_ReturnsFalseWhenDefinitionHasNoRuntimePrefab()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);
        EnemyDefinition definition = CreateEnemyDefinition("BrokenEnemy", runtimePrefab: null);

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TrySpawnEnemy(definition, new EnemyWaveConfig(100f, 120f, 3f, 0.5f, 1f), out Enemy spawnedEnemy);

        Assert.That(success, Is.False);
        Assert.That(spawnedEnemy, Is.Null);
    }

    private MapGridAuthoring CreateMapAuthoring(int width, int height, Vector2 cellSize, string fillTag, float planeY = 0f)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        mapRoot.transform.position = new Vector3(0f, planeY, 0f);
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

    private GameObject CreateGameObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateEnemyPrefab(float maxHealth)
    {
        GameObject enemyObject = CreateGameObject("EnemyPrefab");
        enemyObject.AddComponent<Rigidbody>();
        BoxCollider collider = enemyObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(10f, 10f, 10f);
        enemyObject.AddComponent<CharEnemyMovement>();
        enemyObject.AddComponent<EnemyMeleeAttacker>();
        enemyObject.AddComponent<EnemyRangedTokenAttacker>();
        enemyObject.AddComponent<EnemySummoner>();
        enemyObject.AddComponent<EnemyDefinitionBinder>();
        BaseCharEnemyNorm1 enemyData = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetEnemyHealth(enemyData, maxHealth);
        return enemyObject;
    }

    private EnemyDefinition CreateEnemyDefinition(string enemyId, EnemyDefinitionBinder runtimePrefab, string glyphText = "坚")
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

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
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
        return (T)field.GetValue(target);
    }

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", health);
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
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

    private sealed class DummyWaveConfigReceiver : MonoBehaviour, IEnemyWaveConfigReceiver
    {
        public EnemyWaveConfig LastConfig { get; private set; }
        public int ApplyCount { get; private set; }

        public void ApplyWaveConfig(EnemyWaveConfig config)
        {
            LastConfig = config;
            ApplyCount++;
        }
    }
}
