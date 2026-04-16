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
        StatusController.ClearStatus();

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

        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 2f;
        attackSpec.resultType = AttackResultType.Explosion;

        CompiledAttack compiledAttack = new()
        {
            AttackSpec = attackSpec,
            CoreType = attackSpec.coreType,
            BehaviorType = attackSpec.behaviorType,
            ResultType = AttackResultType.Explosion,
            CanFire = true,
            ExplosionRadius = 10f,
            HasExplosion = true,
            ResultEffects = new ResultEffectPayload
            {
                explosionRadius = 10f,
                explosionDamageMultiplier = 1f,
                explosionDelaySeconds = 0.5f,
            }.GetSanitized(),
        };

        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, compiledAttack, BulletTargetPolicy.EnemiesOnly);
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

    private BaseCharEnemyNorm1 CreateEnemyWithTaggedChildCollider(string childColliderTag)
    {
        GameObject enemyRoot = CreateGameObject("Enemy");
        enemyRoot.tag = "Enemy_Object";
        BaseCharEnemyNorm1 enemy = enemyRoot.AddComponent<BaseCharEnemyNorm1>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.tag = childColliderTag;
        colliderObject.transform.SetParent(enemyRoot.transform, false);
        BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(8f, 8f, 8f);
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
}
