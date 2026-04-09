using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAttackExtensionTests
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
    public void TryPerformAttack_RangedTokenAttacker_EmitsPlayerTargetedBullet()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 8f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Enemy");
        enemyObject.transform.position = Vector3.zero;
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyRangedTokenAttacker attacker = enemyObject.AddComponent<EnemyRangedTokenAttacker>();
        SetEnemyHealth(enemy, 10f);

        CharBullet bulletPrefab = CreateBulletPrefab();
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.None, EnemyAttackKind.RangedBulletToken, runtimePrefab: null);
        SetPrivateField(definition, "rangedBulletAttack", new EnemyDefinition.RangedBulletAttackDefinition
        {
            bulletPrefab = bulletPrefab,
            formulaItems = new List<PlaceableTokenData> { coreToken },
            targetPolicy = BulletTargetPolicy.PlayerOnly,
        });

        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 16f, 0f, 3f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(attacker, "Awake");

        bool didFire = (bool)InvokePrivateMethod(attacker, "TryPerformAttack", 0f);
        CharBullet emittedBullet = FindSpawnedBullet(bulletPrefab);
        createdObjects.Add(emittedBullet.gameObject);

        Assert.That(didFire, Is.True);
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.TargetPolicy, Is.EqualTo(BulletTargetPolicy.PlayerOnly));
        Assert.That(emittedBullet.CurrentCompiledAttack, Is.Not.Null);
    }

    [Test]
    public void TryPerformSummon_SpawnsConfiguredEnemyWithoutDropsAndHonorsMaxAliveLimit()
    {
        CreateMapAuthoring(32, 32, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(16f, 0f, 16f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);
        Assert.That(generator.TrySetTarget(playerObject.transform), Is.True);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        CoreTokenData droppedToken = CreateCoreToken("drop_core", "Drop", AttackCoreType.Fire);
        GameObject summonedPrefab = CreateEnemyPrefabShell();
        EnemyDefinition summonedDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            summonedPrefab.GetComponent<EnemyDefinitionBinder>());

        GameObject summonerObject = CreateGameObject("Summoner");
        summonerObject.transform.position = new Vector3(16f, 0f, 14f);
        BaseCharEnemyNorm1 summonerEnemy = summonerObject.AddComponent<BaseCharEnemyNorm1>();
        EnemySummoner summoner = summonerObject.AddComponent<EnemySummoner>();
        SetEnemyHealth(summonerEnemy, 10f);

        EnemyDefinition summonerDefinition = CreateDefinition(EnemyMovementKind.None, EnemyAttackKind.SummonEnemy, runtimePrefab: null);
        SetPrivateField(summonerDefinition, "summonAttack", new EnemyDefinition.SummonAttackDefinition
        {
            summonedEnemyDefinition = summonedDefinition,
            summonedEnemyConfig = new EnemyWaveConfig(
                6f,
                4f,
                0f,
                0f,
                1f,
                new[]
                {
                    new EnemyBulletTokenDropEntry(droppedToken, 1f),
                }),
            summonCountPerCast = 1,
            summonRadius = 3f,
            maxAliveSummons = 1,
        });

        summonerEnemy.TryBindDefinition(summonerDefinition);
        summonerEnemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 8f, 0f, 0f));
        Assert.That(summoner.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(summoner.TrySetEnemyGenerator(generator), Is.True);
        InvokePrivateMethod(summoner, "Awake");

        bool firstSummon = (bool)InvokePrivateMethod(summoner, "TryPerformSummon", 0f);
        List<Enemy> aliveSummons = GetPrivateField<List<Enemy>>(summoner, "aliveSummons");
        Enemy spawnedSummon = aliveSummons[0];
        createdObjects.Add(spawnedSummon.gameObject);

        Assert.That(firstSummon, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));
        Assert.That(spawnedSummon.Definition, Is.SameAs(summonedDefinition));
        Assert.That(spawnedSummon.GetComponent<EnemyBulletTokenDropper>().TokenDrops, Is.Empty);

        bool secondSummon = (bool)InvokePrivateMethod(summoner, "TryPerformSummon", 0.1f);

        Assert.That(secondSummon, Is.False);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));
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

    private GameObject CreateEnemyPrefabShell()
    {
        GameObject enemyObject = CreateGameObject("EnemyPrefabShell");
        enemyObject.tag = "Enemy_Object";
        enemyObject.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 8f);
        enemyObject.AddComponent<CharEnemyMovement>();
        enemyObject.AddComponent<EnemyMeleeAttacker>();
        enemyObject.AddComponent<EnemyRangedTokenAttacker>();
        enemyObject.AddComponent<EnemySummoner>();
        enemyObject.AddComponent<EnemyBulletTokenDropper>();
        enemyObject.AddComponent<EnemyDefinitionBinder>();
        BaseCharEnemyNorm1 enemyData = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetEnemyHealth(enemyData, 6f);
        return enemyObject;
    }

    private CharBullet CreateBulletPrefab()
    {
        GameObject bulletObject = CreateGameObject("BulletPrefab");
        bulletObject.AddComponent<Rigidbody>().isKinematic = true;
        SphereCollider collider = bulletObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;
        return bulletObject.AddComponent<CharBullet>();
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = ScriptableObject.CreateInstance<CoreTokenData>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.CoreType = coreType;
        token.Damage = 1f;
        token.ProjectileLife = 1;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 320f;
        token.MaxLifetime = 2f;
        token.MaxTravelDistance = 512f;
        token.ImpactMask = ~0;
        createdObjects.Add(token);
        return token;
    }

    private EnemyDefinition CreateDefinition(EnemyMovementKind movementKind, EnemyAttackKind attackKind, EnemyDefinitionBinder runtimePrefab)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = "EnemyDefinitionTestAsset";
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "EnemyDefinitionTest");
        SetPrivateField(definition, "displayName", "EnemyDefinitionTest");
        SetPrivateField(definition, "runtimePrefab", runtimePrefab);
        SetPrivateField(definition, "movementKind", movementKind);
        SetPrivateField(definition, "attackKind", attackKind);
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = "测",
            glyphColor = Color.white,
        });
        return definition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static CharBullet FindSpawnedBullet(CharBullet bulletPrefab)
    {
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        foreach (CharBullet bullet in bullets)
        {
            if (bullet != null && bullet != bulletPrefab)
            {
                return bullet;
            }
        }

        return null;
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
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
}
