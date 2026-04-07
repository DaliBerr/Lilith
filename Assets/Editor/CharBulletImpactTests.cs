using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class CharBulletImpactTests
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
    public void CheckImpactContacts_DamagesEnemyWhenChildColliderStillCarriesGroundTag()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBullet(Vector3.zero, 6f);
        BaseCharEnemyNorm1 enemy = CreateEnemyWithTaggedChildCollider(MapGridAuthoring.GroundTagName);
        SetPrivateField(enemy, "health", 5f);

        AttackSpec attackSpec = CreateAttackSpec(projectileLife: 2);
        attackSpec.damage = 1f;
        bullet.InitializeShot(owner.transform, bullet.transform.position, Vector3.forward, attackSpec, null);
        Physics.SyncTransforms();

        InvokePrivateMethod(bullet, "CheckImpactContacts");

        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(4f));
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private CharBullet CreateBullet(Vector3 position, float radius)
    {
        GameObject bulletObject = CreateGameObject("Bullet");
        bulletObject.transform.position = position;
        Rigidbody rigidbody = bulletObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;

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
}
