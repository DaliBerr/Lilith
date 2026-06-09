using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class CharBulletImpactTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        CharBullet.ConfuseCandidateIndexResolver = null;
        StatusController.ClearStatus();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();

        CharBullet[] strayBullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < strayBullets.Length; i++)
        {
            if (strayBullets[i] != null)
            {
                Object.DestroyImmediate(strayBullets[i].gameObject);
            }
        }
    }

    [Test]
    public void CheckImpactContacts_IgnoresGroundTaggedCollider()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(new Vector3(0f, 7.5f, 0f), 12f);
        CreateSurfaceCollider("GroundSurface", MapGridAuthoring.GroundTagName, Vector3.zero, new Vector3(20f, 1f, 20f));
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(bullet, Is.Not.Null);
        Assert.That(bullet.IsActiveShot, Is.True);
        Assert.That(bullet.RemainingLife, Is.EqualTo(2));
    }

    [Test]
    public void CheckImpactContacts_WallOverlapStillConsumesLife()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(new Vector3(0f, 7.5f, 0f), 12f);
        CreateSurfaceCollider("WallSurface", MapGridAuthoring.WallTagName, Vector3.zero, new Vector3(20f, 20f, 20f));
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(bullet, Is.Not.Null);
        Assert.That(bullet.IsActiveShot, Is.True);
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
    }

    [Test]
    public void CheckImpactContacts_WithBounceBehavior_ReflectsWithoutConsumingLife()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(new Vector3(0f, 7.5f, 0f), 12f);
        CreateSurfaceCollider("WallSurface", MapGridAuthoring.WallTagName, Vector3.zero, new Vector3(20f, 20f, 20f));
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.behaviorType = AttackBehaviorType.Bounce;
        attackSpec.bounceCount = 1;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(bullet, Is.Not.Null);
        Assert.That(bullet.IsActiveShot, Is.True);
        Assert.That(bullet.RemainingLife, Is.EqualTo(2));
        Assert.That(bullet.Direction.z, Is.LessThan(0f));
    }

    [Test]
    public void Update_WithStasisBehavior_StaysAtSpawnAndOnlyChecksDirectImpactOnce()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 10f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.damage = 1f;
        attackSpec.behaviorType = AttackBehaviorType.Stasis;
        attackSpec.behaviorParameter = 1.5f;
        attackSpec.projectileSpeed = 12f;

        bullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "Update");
        Vector3 afterFirstUpdate = bullet.MovementTarget.position;
        int lifeAfterFirstUpdate = bullet.RemainingLife;
        float healthAfterFirstUpdate = enemy.CurrentHealth;

        InvokePrivateMethod(bullet, "Update");

        Assert.That(afterFirstUpdate, Is.EqualTo(Vector3.zero));
        Assert.That(bullet.MovementTarget.position, Is.EqualTo(Vector3.zero));
        Assert.That(lifeAfterFirstUpdate, Is.EqualTo(2));
        Assert.That(bullet.RemainingLife, Is.EqualTo(2));
        Assert.That(healthAfterFirstUpdate, Is.EqualTo(9f));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(9f));
        Assert.That(bullet.Speed, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void MovementBehavior_RushIncreasesSpeedAndCaps()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.behaviorType = AttackBehaviorType.Rush;
        attackSpec.behaviorParameter = 2f;
        attackSpec.projectileSpeed = 100f;

        bullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, attackSpec, null);

        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 1f);
        Assert.That(bullet.Speed, Is.EqualTo(150f).Within(0.0001f));

        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 10f);
        Assert.That(bullet.Speed, Is.EqualTo(250f).Within(0.0001f));
    }

    [Test]
    public void MovementBehavior_SlowDecreasesSpeedAndKeepsFloor()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.behaviorType = AttackBehaviorType.Slow;
        attackSpec.behaviorParameter = 2f;
        attackSpec.projectileSpeed = 100f;

        bullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, attackSpec, null);

        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 1f);
        Assert.That(bullet.Speed, Is.EqualTo(50f).Within(0.0001f));

        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 10f);
        Assert.That(bullet.Speed, Is.EqualTo(15f).Within(0.0001f));
    }

    [Test]
    public void MovementBehavior_SnakeAndWanderChangeDirectionDeterministically()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet snakeBullet = CreateBullet(Vector3.zero, 0.5f);
        AttackSpec snakeSpec = CreateAttackSpec(projectileLife: 3);
        snakeSpec.behaviorType = AttackBehaviorType.Snake;
        snakeSpec.behaviorParameter = 1f;
        snakeSpec.projectileSpeed = 100f;
        snakeBullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, snakeSpec, null);

        InvokePrivateMethod(snakeBullet, "TryUpdateMovementBehavior", 0.0625f);
        Assert.That(Mathf.Abs(snakeBullet.Direction.x), Is.GreaterThan(0.0001f));

        AttackSpec wanderSpec = CreateAttackSpec(projectileLife: 3);
        wanderSpec.behaviorType = AttackBehaviorType.Wander;
        wanderSpec.behaviorParameter = 1f;
        wanderSpec.projectileSpeed = 100f;

        CharBullet firstWander = CreateBullet(new Vector3(2f, 0f, 1f), 0.5f);
        CharBullet secondWander = CreateBullet(new Vector3(2f, 0f, 1f), 0.5f);
        firstWander.InitializeShot(owner.transform, firstWander.transform.position, Vector3.forward, wanderSpec, null);
        secondWander.InitializeShot(owner.transform, secondWander.transform.position, Vector3.forward, wanderSpec, null);

        InvokePrivateMethod(firstWander, "TryUpdateMovementBehavior", 0.5f);
        InvokePrivateMethod(secondWander, "TryUpdateMovementBehavior", 0.5f);

        Assert.That(Mathf.Abs(firstWander.Direction.x), Is.GreaterThan(0.0001f));
        Assert.That(secondWander.Direction.x, Is.EqualTo(firstWander.Direction.x).Within(0.0001f));
        Assert.That(secondWander.Direction.z, Is.EqualTo(firstWander.Direction.z).Within(0.0001f));
    }

    [Test]
    public void MovementBehavior_SplitEmitsSafeDerivedChildrenOnSchedule()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f);
        CoreTokenData coreToken = CreateCoreToken(10f, 3);
        BehaviorTokenData splitToken = CreateBehaviorToken(AttackBehaviorType.Split, 2f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, splitToken });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);

        SetPrivateField(bullet, "elapsedLifetime", 0.1f);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.1f);
        Assert.That(Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

        SetPrivateField(bullet, "elapsedLifetime", 0.34f);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.01f);
        CharBullet[] afterFirstSplit = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        Assert.That(afterFirstSplit.Length, Is.EqualTo(3));

        SetPrivateField(bullet, "elapsedLifetime", 0.67f);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.01f);
        CharBullet[] afterSecondSplit = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        Assert.That(afterSecondSplit.Length, Is.EqualTo(5));

        for (int i = 0; i < afterSecondSplit.Length; i++)
        {
            CharBullet candidate = afterSecondSplit[i];
            if (candidate == bullet)
            {
                continue;
            }

            Assert.That(candidate.CurrentAttackSpec.behaviorType, Is.EqualTo(AttackBehaviorType.Straight));
            Assert.That(candidate.CurrentAttackSpec.resultType, Is.EqualTo(AttackResultType.DirectDamage));
            Assert.That(candidate.CurrentAttackSpec.damage, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(candidate.CurrentAttackSpec.behaviorParameter, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(candidate.CurrentPayloads, Is.Empty);
        }
    }

    [Test]
    public void MovementBehavior_SpinOrbitsOwnerAndSurvivesMissingOwner()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(new Vector3(3f, 0f, 0f), 0.5f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.behaviorType = AttackBehaviorType.Spin;
        attackSpec.behaviorParameter = 3f;
        attackSpec.projectileSpeed = 6f;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Assert.That(bullet.AutoMove, Is.False);
        Assert.That(Vector3.Distance(bullet.MovementTarget.position, owner.transform.position), Is.EqualTo(3f).Within(0.0001f));

        owner.transform.position = new Vector3(2f, 0f, 0f);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.5f);
        Assert.That(Vector3.Distance(bullet.MovementTarget.position, owner.transform.position), Is.EqualTo(3f).Within(0.0001f));
        Vector3 lastAnchor = owner.transform.position;

        Object.DestroyImmediate(owner);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.5f);
        Assert.That(Vector3.Distance(bullet.MovementTarget.position, lastAnchor), Is.EqualTo(3f).Within(0.0001f));
        Assert.That(float.IsNaN(bullet.MovementTarget.position.x), Is.False);
        Assert.That(float.IsNaN(bullet.MovementTarget.position.z), Is.False);
    }

    [Test]
    public void MovementBehavior_SpinWithZeroRadiusFollowsOwner()
    {
        GameObject owner = CreateGameObject("Owner");
        owner.transform.position = new Vector3(2f, 0f, 1f);
        CharBullet bullet = CreateBullet(new Vector3(6f, 0f, 1f), 0.5f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.behaviorType = AttackBehaviorType.Spin;
        attackSpec.behaviorParameter = 0f;
        attackSpec.projectileSpeed = 6f;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        InvokePrivateMethod(bullet, "TryUpdateMovementBehavior", 0.5f);

        Assert.That(bullet.MovementTarget.position, Is.EqualTo(owner.transform.position));
        Assert.That(float.IsNaN(bullet.MovementTarget.position.x), Is.False);
        Assert.That(float.IsNaN(bullet.MovementTarget.position.z), Is.False);
    }

    [Test]
    public void MovementBehavior_SpinWithMovementAnchorOrbitsAnchorAndExpiresWhenAnchorDies()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet primary = CreateBullet(Vector3.zero, 0.5f);
        AttackSpec primarySpec = CreateAttackSpec(projectileLife: 3);
        primary.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, primarySpec, null);

        CharBullet orbiter = CreateBullet(new Vector3(3f, 0f, 0f), 0.5f);
        AttackSpec orbitSpec = CreateAttackSpec(projectileLife: 3);
        orbitSpec.behaviorType = AttackBehaviorType.Spin;
        orbitSpec.behaviorParameter = 3f;
        orbitSpec.projectileSpeed = 6f;

        orbiter.InitializeShot(owner.transform, orbiter.transform.position, Vector3.forward, orbitSpec, null);
        orbiter.SetMovementAnchor(primary.MovementTarget, expireWhenInvalid: true);

        Assert.That(orbiter.OwnerRoot, Is.SameAs(owner.transform));
        Assert.That(orbiter.MovementAnchor, Is.SameAs(primary.MovementTarget));
        Assert.That(Vector3.Distance(orbiter.MovementTarget.position, primary.MovementTarget.position), Is.EqualTo(3f).Within(0.0001f));

        primary.TrySetWorldPosition(new Vector3(2f, 0f, 1f));
        InvokePrivateMethod(orbiter, "TryUpdateMovementBehavior", 0.5f);
        Assert.That(Vector3.Distance(orbiter.MovementTarget.position, primary.MovementTarget.position), Is.EqualTo(3f).Within(0.0001f));

        primary.Expire();
        InvokePrivateMethod(orbiter, "TryUpdateMovementBehavior", 0.1f);
        Assert.That(orbiter == null, Is.True);
    }

    [Test]
    public void CheckImpactContacts_DamagesEnemyWhenChildColliderStillCarriesGroundTag()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 5f);

        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 1f;
        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(4f));
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
    }

    [Test]
    public void CheckImpactContacts_PlayerOnlyDamagesPlayerAndConsumesLife()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        PlayerHealth playerHealth = CreatePlayer(Vector3.zero, new Vector3(8f, 8f, 8f), 10f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 2f;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null, BulletTargetPolicy.PlayerOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(8f));
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
        Assert.That(bullet.TargetPolicy, Is.EqualTo(BulletTargetPolicy.PlayerOnly));
    }

    [Test]
    public void CheckImpactContacts_PlayerOnlyIgnoresEnemyWithoutConsumingLife()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 5f);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 2f;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null, BulletTargetPolicy.PlayerOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy.CurrentHealth, Is.EqualTo(5f));
        Assert.That(bullet.RemainingLife, Is.EqualTo(2));
    }

    [Test]
    public void CheckImpactContacts_HealingResultRestoresEnemyHealth()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 5f);
        Assert.That(enemy.TryApplyDamage(2f, out _, out _), Is.True);

        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 1f;
        attackSpec.resultType = AttackResultType.Healing;

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null, BulletTargetPolicy.EnemiesOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy.CurrentHealth, Is.EqualTo(4f));
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
    }

    [Test]
    public void CheckImpactContacts_ExplosionResultWithDelay_DamagesNearbyEnemyAfterDelay()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 2f);

        BaseCharEnemyNorm1 primaryEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(primaryEnemy, 6f);

        BaseCharEnemyNorm1 secondaryEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        secondaryEnemy.transform.position = new Vector3(7f, 0f, 0f);
        SetEnemyHealth(secondaryEnemy, 6f);

        CoreTokenData coreToken = CreateCoreToken(damage: 2f, projectileLife: 2);
        ResultTokenData explosionToken = CreateExplosionResultToken(radius: 10f, damageMultiplier: 1f, delaySeconds: 0.5f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            explosionToken,
        });

        InitializeShotFromProgram(bullet, owner.transform, bullet.transform.position, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);
        Assert.That(bullet.CurrentProjectileNode, Is.Not.Null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(4f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(6f));

        DelayedExplosionEffectRuntime delayedExplosion = Object.FindAnyObjectByType<DelayedExplosionEffectRuntime>();
        Assert.That(delayedExplosion, Is.Not.Null);

        delayedExplosion.Tick(0.25f);
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(6f));

        delayedExplosion.Tick(0.35f);
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(4f));
    }

    [Test]
    public void CheckImpactContacts_DrainResultHealsOwner()
    {
        PlayerHealth owner = CreatePlayer(new Vector3(20f, 0f, 0f), new Vector3(1f, 1f, 1f), 10f);
        owner.TryApplyDamage(4f, out _, out _);
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 10f);

        CoreTokenData coreToken = CreateCoreToken(damage: 4f, projectileLife: 2);
        ResultTokenData drain = CreateResultToken(AttackResultType.Drain);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, drain });

        InitializeShotFromProgram(bullet, owner.transform, bullet.transform.position, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy.CurrentHealth, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(owner.CurrentHealth, Is.EqualTo(8f).Within(0.0001f));
    }

    [Test]
    public void CheckImpactContacts_ShieldResultAddsOwnerShieldBeforeHealthDamage()
    {
        PlayerHealth owner = CreatePlayer(new Vector3(20f, 0f, 0f), new Vector3(1f, 1f, 1f), 10f);
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 10f);

        CoreTokenData coreToken = CreateCoreToken(damage: 3f, projectileLife: 2);
        ResultTokenData shield = CreateResultToken(AttackResultType.Shield);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, shield });

        InitializeShotFromProgram(bullet, owner.transform, bullet.transform.position, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        DamageShieldController shieldController = owner.GetComponent<DamageShieldController>();
        Assert.That(shieldController, Is.Not.Null);
        Assert.That(shieldController.CurrentShield, Is.EqualTo(3f).Within(0.0001f));

        owner.TryApplyDamage(2f, out _, out _);
        Assert.That(owner.CurrentHealth, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(shieldController.CurrentShield, Is.EqualTo(1f).Within(0.0001f));

        SetPrivateField(shieldController, "remainingDuration", 0f);
        InvokePrivateMethod(shieldController, "Update");
        Assert.That(shieldController.CurrentShield, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void CheckImpactContacts_ConfuseResultAppliesResolvedCandidate()
    {
        PlayerHealth owner = CreatePlayer(new Vector3(20f, 0f, 0f), new Vector3(1f, 1f, 1f), 10f);
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(enemy, 10f);

        CoreTokenData coreToken = CreateCoreToken(damage: 3f, projectileLife: 2);
        ResultTokenData confuse = CreateResultToken(AttackResultType.Confuse);
        ResultTokenData shield = CreateResultToken(AttackResultType.Shield);
        confuse.SetRandomResultCandidates(shield);
        CharBullet.ConfuseCandidateIndexResolver = _ => 0;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, confuse });

        InitializeShotFromProgram(bullet, owner.transform, bullet.transform.position, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy.CurrentHealth, Is.EqualTo(7f).Within(0.0001f));
        DamageShieldController shieldController = owner.GetComponent<DamageShieldController>();
        Assert.That(shieldController, Is.Not.Null);
        Assert.That(shieldController.CurrentShield, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void CheckImpactContacts_LeaveResultSpawnsAreaThatTicksDamage()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.6f);
        BaseCharEnemyNorm1 primaryEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetEnemyHealth(primaryEnemy, 10f);
        BaseCharEnemyNorm1 secondaryEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName, 0.5f);
        secondaryEnemy.transform.position = new Vector3(2f, 0f, 0f);
        SetEnemyHealth(secondaryEnemy, 10f);

        CoreTokenData coreToken = CreateCoreToken(damage: 4f, projectileLife: 2);
        ResultTokenData leave = CreateResultToken(AttackResultType.Leave);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, leave });

        InitializeShotFromProgram(bullet, owner.transform, bullet.transform.position, Vector3.forward, program, BulletTargetPolicy.EnemiesOnly);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        LingeringResultAreaRuntime area = Object.FindAnyObjectByType<LingeringResultAreaRuntime>();
        Assert.That(area, Is.Not.Null);

        InvokePrivateMethod(area, "ApplyTick");

        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(9f).Within(0.0001f));
    }

    [Test]
    public void ResultDisplacement_PushAndPullMoveOnlyLightEnemies()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f);
        bullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, CreateAttackSpec(projectileLife: 2), null, BulletTargetPolicy.EnemiesOnly);

        BaseCharEnemyNorm1 lightEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName, 0.5f);
        lightEnemy.transform.position = new Vector3(1f, 0f, 0f);
        SetEnemyHealth(lightEnemy, 10f);
        SetEnemyDisplacementWeight(lightEnemy, 1f);

        BaseCharEnemyNorm1 heavyEnemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName, 0.5f);
        heavyEnemy.transform.position = new Vector3(2f, 0f, 0f);
        SetEnemyHealth(heavyEnemy, 10f);
        SetEnemyDisplacementWeight(heavyEnemy, 5f);

        ResultEffectPayload effects = new ResultEffectPayload
        {
            effectRadius = 3f,
            effectStrength = 1f,
        }.GetSanitized();

        Physics.SyncTransforms();
        InvokePrivateMethodWithArgs(bullet, "TryApplyDisplacementAt", Vector3.zero, effects, AttackResultType.Push);

        Assert.That(lightEnemy.transform.position.x, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(heavyEnemy.transform.position.x, Is.EqualTo(2f).Within(0.0001f));

        lightEnemy.transform.position = new Vector3(2f, 0f, 0f);
        Physics.SyncTransforms();
        InvokePrivateMethodWithArgs(bullet, "TryApplyDisplacementAt", Vector3.zero, effects, AttackResultType.Pull);

        Assert.That(lightEnemy.transform.position.x, Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(heavyEnemy.transform.position.x, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void FixedUpdate_InPauseMenuStatus_StopsAndResumesDynamicBulletVelocity()
    {
        StatusController.Initialize();

        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f, isKinematic: false);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.projectileSpeed = 12f;
        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);

        InvokePrivateMethod(bullet, "FixedUpdate");

        Rigidbody movementRigidbody = bullet.GetComponent<Rigidbody>();
        Assert.That(movementRigidbody, Is.Not.Null);
        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0.0001f));

        StatusController.AddStatus(StatusList.InPauseMenuStatus);
        InvokePrivateMethod(bullet, "FixedUpdate");

        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));

        StatusController.RemoveStatus(StatusList.InPauseMenuStatus);
        InvokePrivateMethod(bullet, "FixedUpdate");

        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0.0001f));
    }

    [Test]
    public void FixedUpdate_InBackPackStatus_StopsAndResumesDynamicBulletVelocity()
    {
        StatusController.Initialize();

        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f, isKinematic: false);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.projectileSpeed = 12f;
        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);

        InvokePrivateMethod(bullet, "FixedUpdate");

        Rigidbody movementRigidbody = bullet.GetComponent<Rigidbody>();
        Assert.That(movementRigidbody, Is.Not.Null);
        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0.0001f));

        StatusController.AddStatus(StatusList.InBackPackStatus);
        InvokePrivateMethod(bullet, "FixedUpdate");

        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));

        StatusController.RemoveStatus(StatusList.InBackPackStatus);
        InvokePrivateMethod(bullet, "FixedUpdate");

        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0.0001f));
    }

    [Test]
    public void FixedUpdate_InBackPackStatus_IgnoresPauseWhenConfigured()
    {
        StatusController.Initialize();

        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 0.5f, isKinematic: false);
        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 3);
        attackSpec.projectileSpeed = 12f;
        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        bullet.SetIgnoreGameplayPauseStatus(true);

        StatusController.AddStatus(StatusList.InBackPackStatus);
        InvokePrivateMethod(bullet, "FixedUpdate");

        Rigidbody movementRigidbody = bullet.GetComponent<Rigidbody>();
        Assert.That(movementRigidbody, Is.Not.Null);
        Assert.That(movementRigidbody.linearVelocity.sqrMagnitude, Is.GreaterThan(0.0001f));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private CharBullet CreateBullet(Vector3 position, float radius, bool isKinematic = true)
    {
        GameObject bulletObject = CreateGameObject("Bullet");
        bulletObject.transform.position = position;
        Rigidbody rigidbody = bulletObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = isKinematic;

        SphereCollider sphereCollider = bulletObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = radius;

        return bulletObject.AddComponent<CharBullet>();
    }

    private BoxCollider CreateSurfaceCollider(string name, string tagName, Vector3 position, Vector3 size)
    {
        GameObject surfaceObject = CreateGameObject(name);
        surfaceObject.tag = tagName;
        surfaceObject.transform.position = position;

        BoxCollider collider = surfaceObject.AddComponent<BoxCollider>();
        collider.size = size;
        return collider;
    }

    private BaseCharEnemyNorm1 CreateEnemyWithTaggedChildCollider(string childColliderTag, float colliderSize = 8f)
    {
        GameObject enemyRoot = CreateGameObject("Enemy");
        enemyRoot.tag = "Enemy_Object";
        BaseCharEnemyNorm1 enemy = enemyRoot.AddComponent<BaseCharEnemyNorm1>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.tag = childColliderTag;
        colliderObject.transform.SetParent(enemyRoot.transform, false);
        BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(colliderSize, colliderSize, colliderSize);
        collider.center = Vector3.zero;
        return enemy;
    }

    private PlayerHealth CreatePlayer(Vector3 position, Vector3 colliderSize, float maxHealth)
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = position;
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        BoxCollider collider = playerObject.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        SetPrivateField(playerHealth, "maxHealth", maxHealth);
        SetPrivateField(playerHealth, "currentHealth", maxHealth);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);
        return playerHealth;
    }

    private static AttackSpec CreateAttackSpec(int projectileLife)
    {
        AttackSpec attackSpec = AttackSpec.CreateDefault();
        attackSpec.projectileLife = projectileLife;
        attackSpec.impactLifeCost = 1;
        attackSpec.impactMask = Physics.DefaultRaycastLayers;
        return attackSpec;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, null);
    }

    private static void InvokePrivateMethod(object target, string methodName, float argument)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, new object[] { argument });
    }

    private static object InvokePrivateMethodWithArgs(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return method.Invoke(target, arguments);
    }

    private CoreTokenData CreateCoreToken(float damage, int projectileLife)
    {
        CoreTokenData token = ScriptableObject.CreateInstance<CoreTokenData>();
        createdObjects.Add(token);
        token.TokenId = "impact_core";
        token.DisplayText = "I";
        token.CoreType = AttackCoreType.Fire;
        token.Damage = damage;
        token.ProjectileLife = projectileLife;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 100f;
        token.MaxLifetime = 1f;
        token.MaxTravelDistance = 100f;
        token.ImpactMask = Physics.DefaultRaycastLayers;
        return token;
    }

    private ResultTokenData CreateExplosionResultToken(float radius, float damageMultiplier, float delaySeconds)
    {
        ResultTokenData token = ScriptableObject.CreateInstance<ResultTokenData>();
        createdObjects.Add(token);
        token.TokenId = "impact_explosion";
        token.DisplayText = "E";
        token.ResultType = AttackResultType.Explosion;
        token.DefaultExplosionRadius = radius;
        token.ExplosionDamageMultiplier = damageMultiplier;
        token.EffectDuration = delaySeconds;
        return token;
    }

    private ResultTokenData CreateResultToken(AttackResultType resultType)
    {
        ResultTokenData token = ScriptableObject.CreateInstance<ResultTokenData>();
        createdObjects.Add(token);
        token.TokenId = "impact_" + resultType.ToString().ToLowerInvariant();
        token.DisplayText = resultType.ToString();
        token.ResultType = resultType;
        token.AcceptsNumericValue = true;
        token.DefaultEffectRadius = resultType == AttackResultType.Leave ||
                                    resultType == AttackResultType.Push ||
                                    resultType == AttackResultType.Pull
            ? 3f
            : 0f;
        token.DefaultEffectStrength = 1f;
        token.EffectDuration = resultType == AttackResultType.Leave ? 3f : 0f;
        token.AreaTickSeconds = 0.5f;
        token.AreaDamageMultiplier = 0.25f;
        token.ShieldDuration = 6f;
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(AttackBehaviorType behaviorType, float defaultBehaviorParameter)
    {
        BehaviorTokenData token = ScriptableObject.CreateInstance<BehaviorTokenData>();
        createdObjects.Add(token);
        token.TokenId = $"behavior_{behaviorType.ToString().ToLowerInvariant()}";
        token.DisplayText = behaviorType.ToString();
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = true;
        token.DefaultProjectileCount = 1;
        token.DefaultBehaviorParameter = defaultBehaviorParameter;
        return token;
    }

    private static void InitializeShotFromProgram(
        CharBullet bullet,
        Transform owner,
        Vector3 spawnPosition,
        Vector3 direction,
        CompiledSpellProgram program,
        BulletTargetPolicy targetPolicy)
    {
        Assert.That(program, Is.Not.Null);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode projectileNode), Is.True);
        bullet.InitializeShot(owner, spawnPosition, direction, projectileNode.AttackSpec, projectileNode, targetPolicy);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", health);
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
    }

    private static void SetEnemyDisplacementWeight(BaseCharEnemyNorm1 enemy, float weight)
    {
        SetPrivateField(enemy, "displacementWeight", weight);
    }
}
