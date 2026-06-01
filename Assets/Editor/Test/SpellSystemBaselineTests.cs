using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SpellSystemBaselineTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        CharBullet[] strayBullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < strayBullets.Length; i++)
        {
            if (strayBullets[i] != null)
            {
                Object.DestroyImmediate(strayBullets[i].gameObject);
            }
        }

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
    public void CompilerBaseline_RequiresCoreAndDefaultsCoreOnlyAttack()
    {
        ValueTokenData looseValue = CreateValueToken("value_two", "二", 2f);

        CompiledSpellProgram missingCore = SpellProgramCompiler.Compile(new BaseTokenData[] { looseValue });

        Assert.That(missingCore.CanCast, Is.False);
        Assert.That(CountMessages(missingCore, AttackCompileMessageSeverity.Error), Is.EqualTo(1));

        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);

        CompiledSpellProgram coreOnly = SpellProgramCompiler.Compile(new BaseTokenData[] { fireCore });
        SpellProjectileNode coreProjectile = GetPrimaryProjectile(coreOnly);

        Assert.That(coreOnly.CanCast, Is.True);
        Assert.That(coreProjectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(coreProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Straight));
        Assert.That(coreProjectile.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(coreProjectile.ProjectileCount, Is.EqualTo(1));
        Assert.That(coreOnly.Messages, Is.Empty);
    }

    [Test]
    public void CompilerBaseline_ConsumesValuesAndLinkedItemsWithCurrentRules()
    {
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, true, 0f);
        split.DefaultTriggerCount = 2;
        split.ChildDamageMultiplier = 0.5f;
        ValueTokenData valueFive = CreateValueToken("value_five", "五", 5f);

        CompiledSpellProgram valueAttack = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fireCore,
            spread,
            valueThree,
            split,
            valueFive,
        });
        SpellProjectileNode valueProjectile = GetPrimaryProjectile(valueAttack);

        Assert.That(valueAttack.CanCast, Is.True);
        Assert.That(valueProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        Assert.That(valueProjectile.ProjectileCount, Is.EqualTo(3));
        Assert.That(valueProjectile.ResultType, Is.EqualTo(AttackResultType.Split));
        Assert.That(valueProjectile.ResultEffects.splitProjectileCount, Is.EqualTo(5));

        ResultTokenData direct = CreateResultToken("direct", "击", AttackResultType.DirectDamage, false, 0f);
        LinkedTokenData acceptedLinked = CreateLinkedToken("linked_fire_direct", 2f, fireCore, direct);

        CompiledSpellProgram acceptedLinkedAttack = SpellProgramCompiler.Compile(new PlaceableTokenData[] { acceptedLinked });
        SpellProjectileNode acceptedLinkedProjectile = GetPrimaryProjectile(acceptedLinkedAttack);

        Assert.That(acceptedLinkedAttack.CanCast, Is.True);
        Assert.That(acceptedLinkedProjectile.AttackSpec.damage, Is.EqualTo(2f).Within(0.0001f));

        BehaviorTokenData invalidPrefix = CreateBehaviorToken("invalid_spread", "散", AttackBehaviorType.Spread, true, 3, 10f);
        LinkedTokenData partiallyAcceptedLinked = CreateLinkedToken("linked_invalid_prefix", 2f, invalidPrefix, direct);

        CompiledSpellProgram partialLinkedAttack = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            partiallyAcceptedLinked,
            fireCore,
        });
        SpellProjectileNode partialLinkedProjectile = GetPrimaryProjectile(partialLinkedAttack);

        Assert.That(partialLinkedAttack.CanCast, Is.True);
        Assert.That(partialLinkedProjectile.AttackSpec.damage, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(CountMessages(partialLinkedAttack, AttackCompileMessageSeverity.Warning), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void SpellBookLoadoutBaseline_ExpandsItemsCompilesAndTracksRevision()
    {
        GameObject owner = CreateGameObject("Player");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        ResultTokenData direct = CreateResultToken("direct", "击", AttackResultType.DirectDamage, false, 0f);
        LinkedTokenData linked = CreateLinkedToken("linked_fire_direct", 1.5f, fireCore, direct);
        int changeCount = 0;
        loadout.Changed += () => changeCount++;

        loadout.SetItems(new PlaceableTokenData[] { linked });
        int revisionAfterLinked = loadout.Revision;

        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.Tokens.Count, Is.EqualTo(2));
        Assert.That(loadout.TryGetCompiledProgram(out CompiledSpellProgram compiledProgram), Is.True);
        Assert.That(compiledProgram.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(1));
        Assert.That(compiledProgram.PrimaryCastBlock.Projectiles[0].AttackSpec.damage, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(changeCount, Is.EqualTo(1));

        loadout.SetTokens(new BaseTokenData[] { fireCore });

        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.Tokens.Count, Is.EqualTo(1));
        Assert.That(loadout.Revision, Is.GreaterThan(revisionAfterLinked));
        Assert.That(loadout.CurrentCompiledProgram.CanCast, Is.True);
        Assert.That(loadout.CurrentCompiledProgram.PrimaryCastBlock.Projectiles[0].CanFire, Is.True);
        Assert.That(changeCount, Is.EqualTo(2));
    }

    [Test]
    public void EmitterBaseline_SpreadAttackSpawnsConfiguredProjectiles()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fireCore,
            spread,
            valueThree,
        });
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(spawnedBullets.Count, Is.EqualTo(3));
        for (int i = 0; i < spawnedBullets.Count; i++)
        {
            Assert.That(spawnedBullets[i].CurrentProjectileNode, Is.Not.Null);
            Assert.That(spawnedBullets[i].CurrentAttackSpec.behaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        }
    }

    [Test]
    public void ImpactBaseline_ExplosionDamagesPrimaryAndNearbyTargets()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 5f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(1f, 0f, 2f), 5f);
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false, 1f);
        explosion.ExplosionDamageMultiplier = 0.3f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fireCore,
            explosion,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        Assert.That(bullet.CurrentProjectileNode, Is.Not.Null);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(3.7f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(4.7f).Within(0.0001f));
    }

    [Test]
    public void ImpactBaseline_SplitChildrenLoseSplitResult()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bulletPrefab = CreateBulletPrefab();
        CharBullet bullet = Object.Instantiate(bulletPrefab);
        createdObjects.Add(bullet.gameObject);
        bullet.SetSpawnTemplate(bulletPrefab);
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 20f);
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, true, 0f);
        split.DefaultTriggerCount = 2;
        split.ChildDamageMultiplier = 0.5f;
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fireCore,
            split,
            valueThree,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        int splitChildCount = 0;

        Assert.That(handled, Is.True);
        for (int i = 0; i < bullets.Length; i++)
        {
            CharBullet candidate = bullets[i];
            if (candidate == null || candidate == bullet || candidate == bulletPrefab)
            {
                continue;
            }

            splitChildCount++;
            Assert.That(candidate.CurrentProjectileNode, Is.Not.Null);
            Assert.That(candidate.CurrentProjectileNode.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
            Assert.That(candidate.CurrentAttackSpec.resultType, Is.EqualTo(AttackResultType.DirectDamage));
        }

        Assert.That(splitChildCount, Is.EqualTo(3));
    }

    [Test]
    public void EnemyBaseline_RangedTokenAttackerCompilesFormulaItemsAndTargetsPlayer()
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
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        EnemyDefinition definition = CreateEnemyDefinition();
        SetPrivateField(definition, "rangedBulletAttack", new EnemyDefinition.RangedBulletAttackDefinition
        {
            bulletPrefab = bulletPrefab,
            formulaItems = new List<PlaceableTokenData> { fireCore },
            targetPolicy = BulletTargetPolicy.PlayerOnly,
        });

        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 0f, 16f, 0f, 3f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(attacker, "Awake");

        bool didFire = (bool)InvokePrivateMethod(attacker, "TryPerformAttack", 0f);
        CharBullet emittedBullet = FindSpawnedBullet(bulletPrefab);

        Assert.That(didFire, Is.True);
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.TargetPolicy, Is.EqualTo(BulletTargetPolicy.PlayerOnly));
        Assert.That(emittedBullet.CurrentProjectileNode, Is.Not.Null);
        Assert.That(emittedBullet.CurrentAttackSpec.damage, Is.EqualTo(3f).Within(0.0001f));
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = CreateToken<CoreTokenData>(tokenId, displayText);
        token.CoreType = coreType;
        token.Damage = 1f;
        token.ProjectileLife = 1;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 320f;
        token.MaxLifetime = 2f;
        token.MaxTravelDistance = 512f;
        token.ImpactMask = ~0;
        token.ArmoredEnemyId = string.Empty;
        token.ArmoredDamageMultiplier = 1f;
        token.BurnTriggerCount = 0;
        token.BurnDamagePerSecond = 0f;
        token.BurnDuration = 0f;
        token.SlowPercent = 0f;
        token.SlowDuration = 0f;
        token.ThunderChainTargetCount = 0;
        token.ThunderChainRadius = 0f;
        token.ThunderChainDamage = 0f;
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(
        string tokenId,
        string displayText,
        AttackBehaviorType behaviorType,
        bool acceptsNumericValue,
        int defaultProjectileCount,
        float spreadAngleStep)
    {
        BehaviorTokenData token = CreateToken<BehaviorTokenData>(tokenId, displayText);
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        token.SpreadAngleStep = spreadAngleStep;
        token.ProjectileDamageMultiplier = 1f;
        token.PierceLifetimeDistanceScalePerCount = 0.2f;
        return token;
    }

    private ResultTokenData CreateResultToken(
        string tokenId,
        string displayText,
        AttackResultType resultType,
        bool acceptsNumericValue,
        float defaultExplosionRadius)
    {
        ResultTokenData token = CreateToken<ResultTokenData>(tokenId, displayText);
        token.ResultType = resultType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultExplosionRadius = defaultExplosionRadius;
        token.ExplosionDamageMultiplier = 1f;
        token.DefaultTriggerCount = 0;
        token.EffectDuration = 0f;
        token.ChildDamageMultiplier = 0.5f;
        return token;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateToken<ValueTokenData>(tokenId, displayText);
        token.NumericValue = numericValue;
        return token;
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(token);
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = $"{tokenId}_asset";
        return token;
    }

    private LinkedTokenData CreateLinkedToken(string itemId, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        createdObjects.Add(token);
        token.ItemId = itemId;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        return token;
    }

    private CharBullet CreateBulletPrefab()
    {
        GameObject bulletObject = CreateGameObject("BulletPrefab");
        SphereCollider sphereCollider = bulletObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.5f;

        GameObject glyphObject = CreateGameObject("BulletGlyph");
        glyphObject.transform.SetParent(bulletObject.transform, false);
        TextMeshPro textMeshPro = glyphObject.AddComponent<TextMeshPro>();
        textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20f);
        textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
        return bulletObject.AddComponent<CharBullet>();
    }

    private TestEnemy CreateEnemy(string name, Vector3 position, float health)
    {
        GameObject enemyObject = CreateGameObject(name);
        enemyObject.transform.position = position;
        enemyObject.tag = "Enemy_Object";
        enemyObject.AddComponent<BoxCollider>();
        TestEnemy enemy = enemyObject.AddComponent<TestEnemy>();
        enemy.SetHealth(health);
        return enemy;
    }

    private EnemyDefinition CreateEnemyDefinition()
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "baseline_ranged_enemy");
        SetPrivateField(definition, "displayName", "Baseline Ranged Enemy");
        SetPrivateField(definition, "movementKind", EnemyMovementKind.None);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.RangedBulletToken);
        return definition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InitializeShotFromProgram(
        CharBullet bullet,
        Transform owner,
        Vector3 spawnPosition,
        Vector3 direction,
        CompiledSpellProgram program,
        BulletTargetPolicy targetPolicy = BulletTargetPolicy.EnemiesOnly)
    {
        SpellProjectileNode projectileNode = GetPrimaryProjectile(program);
        bullet.InitializeShot(owner, spawnPosition, direction, projectileNode.AttackSpec, projectileNode, targetPolicy);
    }

    private static SpellProjectileNode GetPrimaryProjectile(CompiledSpellProgram program)
    {
        Assert.That(program, Is.Not.Null);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode projectile), Is.True);
        Assert.That(projectile, Is.Not.Null);
        return projectile;
    }

    private static int CountMessages(CompiledSpellProgram program, AttackCompileMessageSeverity severity)
    {
        int count = 0;
        for (int i = 0; i < program.Messages.Count; i++)
        {
            if (program.Messages[i].severity == severity)
            {
                count++;
            }
        }

        return count;
    }

    private static bool InvokeTryRegisterImpact(CharBullet bullet, Collider collider)
    {
        MethodInfo tryRegisterImpact = typeof(CharBullet).GetMethod("TryRegisterImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(tryRegisterImpact, Is.Not.Null);
        return (bool)tryRegisterImpact.Invoke(bullet, new object[] { collider });
    }

    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", health);
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static FieldInfo FindInstanceField(Type type, string fieldName)
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

    private static MethodInfo FindInstanceMethod(Type type, string methodName)
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

    private static CharBullet FindSpawnedBullet(CharBullet bulletPrefab)
    {
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null && bullets[i] != bulletPrefab)
            {
                return bullets[i];
            }
        }

        return null;
    }

    private sealed class TestEnemy : Enemy
    {
        private float maxHealth = 1f;
        private float currentHealth = 1f;

        public override float MoveSpeed => 0f;
        public override float RotationSpeed => 0f;
        public override float StoppingDistance => 0f;
        public override float MaxHealth => maxHealth;
        public override float CurrentHealth => currentHealth;
        public override float AttackRange => throw new NotImplementedException();
        public override float AttackCooldown => throw new NotImplementedException();
        public override float AttackDamage => throw new NotImplementedException();

        public void SetHealth(float health)
        {
            maxHealth = Mathf.Max(0f, health);
            currentHealth = maxHealth;
        }

        public override bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead)
        {
            if (damage <= 0f || currentHealth <= 0f)
            {
                remainingHealth = currentHealth;
                isDead = currentHealth <= 0f;
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            remainingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            return true;
        }

        public override bool TryApplyHealing(float healing, out float resultingHealth, out bool isDead)
        {
            resultingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            if (healing <= 0f || isDead || currentHealth >= maxHealth)
            {
                return false;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + healing);
            resultingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            return true;
        }
    }
}
