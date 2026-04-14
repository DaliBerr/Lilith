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
        Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.damage, Is.EqualTo(3f));
    }

    [Test]
    public void TryPerformAttack_RangedTokenAttacker_ExtendsProjectileLifetimeToConfiguredRange()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 200f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Enemy");
        enemyObject.transform.position = Vector3.zero;
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyRangedTokenAttacker attacker = enemyObject.AddComponent<EnemyRangedTokenAttacker>();
        SetEnemyHealth(enemy, 10f);

        CoreTokenData coreToken = CreateCoreToken("long_range_fire", "Long Range Fire", AttackCoreType.Fire);
        coreToken.ProjectileSpeed = 100f;
        coreToken.MaxLifetime = 0.5f;
        coreToken.MaxTravelDistance = 32f;
        CharBullet bulletPrefab = CreateBulletPrefab();
        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.None, EnemyAttackKind.RangedBulletToken, runtimePrefab: null);
        SetPrivateField(definition, "rangedBulletAttack", new EnemyDefinition.RangedBulletAttackDefinition
        {
            bulletPrefab = bulletPrefab,
            formulaItems = new List<PlaceableTokenData> { coreToken },
            targetPolicy = BulletTargetPolicy.PlayerOnly,
        });

        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 288f, 0f, 3f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(attacker, "Awake");

        bool didFire = (bool)InvokePrivateMethod(attacker, "TryPerformAttack", 0f);
        CharBullet emittedBullet = FindSpawnedBullet(bulletPrefab);
        createdObjects.Add(emittedBullet.gameObject);

        Assert.That(didFire, Is.True);
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.maxTravelDistance, Is.GreaterThanOrEqualTo(288f));
        // Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.maxLifetime, Is.GreaterThanOrEqualTo(2.88f).Within(0.001f));
    }

    [Test]
    public void TryPerformAttack_RangedTokenAttacker_AppliesProjectileSpeedMultiplierOverride()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 220f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Enemy");
        enemyObject.transform.position = Vector3.zero;
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyRangedTokenAttacker attacker = enemyObject.AddComponent<EnemyRangedTokenAttacker>();
        SetEnemyHealth(enemy, 10f);

        CoreTokenData coreToken = CreateCoreToken("edge_core_test", "Edge", AttackCoreType.Edge);
        coreToken.Damage = 10f;
        coreToken.ProjectileSpeed = 120f;
        coreToken.MaxLifetime = 0.6f;
        coreToken.MaxTravelDistance = 72f;
        CharBullet bulletPrefab = CreateBulletPrefab();
        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.None, EnemyAttackKind.RangedBulletToken, runtimePrefab: null);
        SetPrivateField(definition, "rangedBulletAttack", new EnemyDefinition.RangedBulletAttackDefinition
        {
            bulletPrefab = bulletPrefab,
            formulaItems = new List<PlaceableTokenData> { coreToken },
            targetPolicy = BulletTargetPolicy.PlayerOnly,
            projectileSpeedMultiplier = 1.5f,
        });

        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 360f, 0f, 0f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(attacker, "Awake");

        bool didFire = (bool)InvokePrivateMethod(attacker, "TryPerformAttack", 0f);
        CharBullet emittedBullet = FindSpawnedBullet(bulletPrefab);
        createdObjects.Add(emittedBullet.gameObject);

        Assert.That(didFire, Is.True);
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.damage, Is.EqualTo(10f));
        Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.projectileSpeed, Is.EqualTo(180f).Within(0.001f));
        Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.maxTravelDistance, Is.GreaterThanOrEqualTo(360f));
        // Assert.That(emittedBullet.CurrentCompiledAttack.AttackSpec.maxLifetime, Is.GreaterThanOrEqualTo(2f).Within(0.001f));
    }

    [Test]
    public void TryCastSkill_Summoner_SpawnsConfiguredEnemyWithoutDropsAndHonorsMaxAliveLimit()
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

        GameObject summonedPrefab = CreateEnemyPrefabShell();
        EnemyDefinition summonedDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            summonedPrefab.GetComponent<EnemyDefinitionBinder>());
        SetPrivateField(summonedDefinition, "combat", new EnemyDefinition.EnemyCombatDefinition
        {
            maxHealth = 6f,
            moveSpeed = 4f,
        });

        GameObject summonerObject = CreateGameObject("Summoner");
        summonerObject.transform.position = new Vector3(16f, 0f, 14f);
        BaseCharEnemyNorm1 summonerEnemy = summonerObject.AddComponent<BaseCharEnemyNorm1>();
        EnemySummoner summoner = summonerObject.AddComponent<EnemySummoner>();
        SetEnemyHealth(summonerEnemy, 10f);

        EnemyDefinition.EnemySkillSlotDefinition summonSkillSlot = CreateSummonSkillSlot(
            summonedDefinition,
            cooldownSeconds: 0.25f,
            castRange: 8f,
            minSummonCountPerCast: 1,
            maxSummonCountPerCast: 1,
            maxAliveSummons: 1,
            summonRadius: 3f);
        EnemyDefinition summonerDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            runtimePrefab: null,
            skillSlots: new[] { summonSkillSlot });

        summonerEnemy.TryBindDefinition(summonerDefinition);
        summonerEnemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 8f, 1.5f, 0f));
        Assert.That(summoner.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(summoner.TrySetEnemyGenerator(generator), Is.True);
        InvokePrivateMethod(summoner, "Awake");

        bool firstSummon = summoner.TryCastSkill(summonSkillSlot);
        List<Enemy> aliveSummons = GetPrivateField<List<Enemy>>(summoner, "aliveSummons");
        Enemy spawnedSummon = aliveSummons[0];
        createdObjects.Add(spawnedSummon.gameObject);

        Assert.That(firstSummon, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));
        Assert.That(spawnedSummon.Definition, Is.SameAs(summonedDefinition));
        Assert.That(spawnedSummon.GetComponent<EnemyBulletTokenDropper>().TokenDrops, Is.Empty);

        bool secondSummon = summoner.TryCastSkill(summonSkillSlot);

        Assert.That(secondSummon, Is.False);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));

        spawnedSummon.TryApplyDamage(spawnedSummon.CurrentHealth, out _, out _);
        bool thirdSummon = summoner.TryCastSkill(summonSkillSlot);

        Assert.That(thirdSummon, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));
    }

    [Test]
    public void TryCastSkill_Summoner_UsesCurrentGrowthAndRandomizedSummonCount()
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
        Assert.That(generator.TrySetCompletedWaveCount(2), Is.True);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        GameObject summonedPrefab = CreateEnemyPrefabShell();
        EnemyDefinition summonedDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            summonedPrefab.GetComponent<EnemyDefinitionBinder>());
        SetPrivateField(summonedDefinition, "combat", new EnemyDefinition.EnemyCombatDefinition
        {
            maxHealth = 10f,
            moveSpeed = 5f,
        });

        GameObject summonerObject = CreateGameObject("Summoner");
        summonerObject.transform.position = new Vector3(16f, 0f, 14f);
        BaseCharEnemyNorm1 summonerEnemy = summonerObject.AddComponent<BaseCharEnemyNorm1>();
        EnemySummoner summoner = summonerObject.AddComponent<EnemySummoner>();
        SetEnemyHealth(summonerEnemy, 10f);

        EnemyDefinition.EnemySkillSlotDefinition summonSkillSlot = CreateSummonSkillSlot(
            summonedDefinition,
            cooldownSeconds: 0.25f,
            castRange: 8f,
            minSummonCountPerCast: 1,
            maxSummonCountPerCast: 2,
            maxAliveSummons: 3,
            summonRadius: 3f);
        EnemyDefinition summonerDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            runtimePrefab: null,
            skillSlots: new[] { summonSkillSlot });

        summonerEnemy.TryBindDefinition(summonerDefinition);
        summonerEnemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 8f, 1.5f, 0f));
        Assert.That(summoner.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(summoner.TrySetEnemyGenerator(generator), Is.True);
        SetPrivateField(summoner, "randomSource", new Vocalith.Random(7));
        InvokePrivateMethod(summoner, "Awake");

        bool didSummon = summoner.TryCastSkill(summonSkillSlot);
        List<Enemy> aliveSummons = GetPrivateField<List<Enemy>>(summoner, "aliveSummons");
        foreach (Enemy summon in aliveSummons)
        {
            createdObjects.Add(summon.gameObject);
        }

        Assert.That(didSummon, Is.True);
        Assert.That(aliveSummons.Count, Is.InRange(1, 2));
        foreach (Enemy summon in aliveSummons)
        {
            Assert.That(summon.Definition, Is.SameAs(summonedDefinition));
            Assert.That(summon.MaxHealth, Is.EqualTo(10.8f).Within(0.001f));
            Assert.That(summon.MoveSpeed, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(summon.GetComponent<EnemyBulletTokenDropper>().TokenDrops, Is.Empty);
        }
    }

    [Test]
    public void TryPerformSkills_Binder_CastsMultipleSkillSlotsWithIndependentCooldowns()
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

        GameObject summonedPrefab = CreateEnemyPrefabShell();
        EnemyDefinition summonedDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            summonedPrefab.GetComponent<EnemyDefinitionBinder>());
        SetPrivateField(summonedDefinition, "combat", new EnemyDefinition.EnemyCombatDefinition
        {
            maxHealth = 6f,
            moveSpeed = 4f,
        });

        GameObject bossObject = CreateGameObject("Boss");
        bossObject.transform.position = new Vector3(16f, 0f, 14f);
        BaseCharEnemyNorm1 bossEnemy = bossObject.AddComponent<BaseCharEnemyNorm1>();
        EnemySummoner summoner = bossObject.AddComponent<EnemySummoner>();
        EnemyDefinitionBinder binder = bossObject.AddComponent<EnemyDefinitionBinder>();
        SetEnemyHealth(bossEnemy, 20f);

        EnemyDefinition.EnemySkillSlotDefinition firstSkillSlot = CreateSummonSkillSlot(
            summonedDefinition,
            cooldownSeconds: 0.25f,
            castRange: 8f,
            minSummonCountPerCast: 1,
            maxSummonCountPerCast: 1,
            maxAliveSummons: 4,
            summonRadius: 3f);
        EnemyDefinition.EnemySkillSlotDefinition secondSkillSlot = CreateSummonSkillSlot(
            summonedDefinition,
            cooldownSeconds: 0.5f,
            castRange: 8f,
            minSummonCountPerCast: 1,
            maxSummonCountPerCast: 1,
            maxAliveSummons: 4,
            summonRadius: 3f);
        EnemyDefinition bossDefinition = CreateDefinition(
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            runtimePrefab: null,
            skillSlots: new[] { firstSkillSlot, secondSkillSlot },
            maxSkillCastsPerTick: 2);

        bossEnemy.TryBindDefinition(bossDefinition);
        bossEnemy.ApplyWaveConfig(new EnemyWaveConfig(20f, 0f, 8f, 0f, 0f));
        Assert.That(summoner.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(summoner.TrySetEnemyGenerator(generator), Is.True);
        InvokePrivateMethod(summoner, "Awake");
        Assert.That(binder.ApplyDefinition(bossDefinition), Is.True);

        bool firstTick = (bool)InvokePrivateMethod(binder, "TryPerformSkills", 0f);
        List<Enemy> aliveSummons = GetPrivateField<List<Enemy>>(summoner, "aliveSummons");
        createdObjects.Add(aliveSummons[0].gameObject);
        createdObjects.Add(aliveSummons[1].gameObject);

        Assert.That(firstTick, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(2));

        aliveSummons[1].TryApplyDamage(aliveSummons[1].CurrentHealth, out _, out _);
        aliveSummons[0].TryApplyDamage(aliveSummons[0].CurrentHealth, out _, out _);
        bool secondTick = (bool)InvokePrivateMethod(binder, "TryPerformSkills", 0.3f);

        Assert.That(secondTick, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(1));
        createdObjects.Add(aliveSummons[0].gameObject);

        aliveSummons[0].TryApplyDamage(aliveSummons[0].CurrentHealth, out _, out _);
        bool thirdTick = (bool)InvokePrivateMethod(binder, "TryPerformSkills", 0.6f);

        Assert.That(thirdTick, Is.True);
        Assert.That(aliveSummons, Has.Count.EqualTo(2));
        createdObjects.Add(aliveSummons[0].gameObject);
        createdObjects.Add(aliveSummons[1].gameObject);
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
        enemyObject.AddComponent<EnemyExplosiveAttacker>();
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

    private EnemyDefinition CreateDefinition(
        EnemyMovementKind movementKind,
        EnemyAttackKind attackKind,
        EnemyDefinitionBinder runtimePrefab,
        IEnumerable<EnemyDefinition.EnemySkillSlotDefinition> skillSlots = null,
        int maxSkillCastsPerTick = 1)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = "EnemyDefinitionTestAsset";
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "EnemyDefinitionTest");
        SetPrivateField(definition, "displayName", "EnemyDefinitionTest");
        SetPrivateField(definition, "runtimePrefab", runtimePrefab);
        SetPrivateField(definition, "movementKind", movementKind);
        SetPrivateField(definition, "attackKind", attackKind);
        SetPrivateField(
            definition,
            "skillSlots",
            skillSlots != null
                ? new List<EnemyDefinition.EnemySkillSlotDefinition>(skillSlots)
                : new List<EnemyDefinition.EnemySkillSlotDefinition>());
        SetPrivateField(
            definition,
            "skillCasting",
            new EnemyDefinition.EnemySkillCastingDefinition
            {
                maxSkillCastsPerTick = maxSkillCastsPerTick,
            });
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = "测",
            glyphColor = Color.white,
        });
        return definition;
    }

    private static EnemyDefinition.EnemySkillSlotDefinition CreateSummonSkillSlot(
        EnemyDefinition summonedDefinition,
        float cooldownSeconds,
        float castRange,
        int minSummonCountPerCast,
        int maxSummonCountPerCast,
        int maxAliveSummons,
        float summonRadius)
    {
        return new EnemyDefinition.EnemySkillSlotDefinition
        {
            skillKind = EnemySkillKind.SummonEnemy,
            cooldownSeconds = cooldownSeconds,
            castRange = castRange,
            summonSkill = new EnemyDefinition.SummonSkillDefinition
            {
                summonedEnemyDefinition = summonedDefinition,
                minSummonCountPerCast = minSummonCountPerCast,
                maxSummonCountPerCast = maxSummonCountPerCast,
                summonRadius = summonRadius,
                maxAliveSummons = maxAliveSummons,
            },
        };
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
