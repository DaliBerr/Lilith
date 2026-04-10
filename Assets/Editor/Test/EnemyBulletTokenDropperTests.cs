using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyBulletTokenDropperTests
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
    public void EnemyDeath_SpawnsAllIndependentSuccessfulDrops()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        CoreTokenData guaranteedA = CreateToken<CoreTokenData>("drop_a", "Drop A");
        CoreTokenData guaranteedB = CreateToken<CoreTokenData>("drop_b", "Drop B");
        CoreTokenData neverDrop = CreateToken<CoreTokenData>("drop_c", "Drop C");

        dropper.ApplyWaveConfig(new EnemyWaveConfig(
            100f,
            120f,
            3f,
            0.5f,
            1f,
            new[]
            {
                new EnemyBulletTokenDropEntry(guaranteedA, 1f),
                new EnemyBulletTokenDropEntry(guaranteedB, 1f),
                new EnemyBulletTokenDropEntry(neverDrop, 0f),
            }));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(2));

        List<PlaceableTokenData> droppedTokens = new();
        for (int i = 0; i < pickupParent.transform.childCount; i++)
        {
            BulletTokenPickup spawnedPickup = pickupParent.transform.GetChild(i).GetComponent<BulletTokenPickup>();
            Assert.That(spawnedPickup, Is.Not.Null);
            droppedTokens.Add(spawnedPickup.Token);
        }

        Assert.That(droppedTokens, Has.Member(guaranteedA));
        Assert.That(droppedTokens, Has.Member(guaranteedB));
        Assert.That(droppedTokens, Has.No.Member(neverDrop));
    }

    [Test]
    public void EnemyDeath_CanDropLinkedToken()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        dropper.ApplyWaveConfig(new EnemyWaveConfig(
            100f,
            120f,
            3f,
            0.5f,
            1f,
            new[]
            {
                new EnemyBulletTokenDropEntry(linked, 1f),
            }));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(1));

        BulletTokenPickup spawnedPickup = pickupParent.transform.GetChild(0).GetComponent<BulletTokenPickup>();
        Assert.That(spawnedPickup, Is.Not.Null);
        Assert.That(spawnedPickup.Token, Is.SameAs(linked));
    }

    [Test]
    public void EnemyDeath_CanDropPickupOnlyTokens()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        RemnantPickupTokenData remnantToken = CreateRemnantToken("pickup_remnant", "Remnant", 2);
        HealingPickupTokenData healingToken = CreateHealingToken("pickup_heal", "Heal", 20f);

        dropper.ApplyWaveConfig(new EnemyWaveConfig(
            100f,
            120f,
            3f,
            0.5f,
            1f,
            new[]
            {
                new EnemyBulletTokenDropEntry(remnantToken, 1f),
                new EnemyBulletTokenDropEntry(healingToken, 1f),
            }));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(2));

        List<PlaceableTokenData> droppedTokens = new();
        for (int i = 0; i < pickupParent.transform.childCount; i++)
        {
            BulletTokenPickup spawnedPickup = pickupParent.transform.GetChild(i).GetComponent<BulletTokenPickup>();
            Assert.That(spawnedPickup, Is.Not.Null);
            droppedTokens.Add(spawnedPickup.Token);
        }

        Assert.That(droppedTokens, Has.Member(remnantToken));
        Assert.That(droppedTokens, Has.Member(healingToken));
    }

    [Test]
    public void EnemyDeath_UsesWaveDropCountForSpawnQuantity()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        CoreTokenData token = CreateToken<CoreTokenData>("drop_count_test", "Drop Count");
        dropper.ApplyWaveConfig(new EnemyWaveConfig(
            100f,
            120f,
            3f,
            0.5f,
            1f,
            new[]
            {
                new EnemyBulletTokenDropEntry(token, 1f, 3),
            }));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(3));

        for (int i = 0; i < pickupParent.transform.childCount; i++)
        {
            BulletTokenPickup spawnedPickup = pickupParent.transform.GetChild(i).GetComponent<BulletTokenPickup>();
            Assert.That(spawnedPickup, Is.Not.Null);
            Assert.That(spawnedPickup.Token, Is.SameAs(token));
        }
    }

    [Test]
    public void EnemyDeath_WithNoSuccessfulDrops_SpawnsNothing()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        dropper.ApplyWaveConfig(new EnemyWaveConfig(100f, 120f, 3f, 0.5f, 1f, new EnemyBulletTokenDropEntry[0]));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(0));
    }

    [Test]
    public void EnemyDeath_SpawnsPickupRelativeToMapPlaneHeight()
    {
        GameObject pickupParent = CreateGameObject("PickupParent");
        CreateMapAuthoring(10f);
        BulletTokenPickup pickupPrefab = CreatePickupPrefab();
        BaseCharEnemyNorm1 enemy = CreateEnemy();
        enemy.transform.position = new Vector3(4f, 30f, 6f);
        EnemyBulletTokenDropper dropper = enemy.gameObject.AddComponent<EnemyBulletTokenDropper>();

        Assert.That(dropper.TrySetPickupPrefab(pickupPrefab), Is.True);
        SetPrivateField(dropper, "pickupParent", pickupParent.transform);

        CoreTokenData guaranteed = CreateToken<CoreTokenData>("drop_a", "Drop A");
        dropper.ApplyWaveConfig(new EnemyWaveConfig(
            100f,
            120f,
            3f,
            0.5f,
            1f,
            new[]
            {
                new EnemyBulletTokenDropEntry(guaranteed, 1f),
            }));

        Assert.That(enemy.TryApplyDamage(enemy.CurrentHealth, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(pickupParent.transform.childCount, Is.EqualTo(1));

        Transform spawnedPickup = pickupParent.transform.GetChild(0);
        Assert.That(spawnedPickup.position.y, Is.EqualTo(16f));
    }

    private BaseCharEnemyNorm1 CreateEnemy()
    {
        GameObject enemyObject = CreateGameObject("Enemy");
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        return enemy;
    }

    private MapGridAuthoring CreateMapAuthoring(float planeY)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        mapRoot.transform.position = new Vector3(0f, planeY, 0f);
        return mapRoot.AddComponent<MapGridAuthoring>();
    }

    private BulletTokenPickup CreatePickupPrefab()
    {
        GameObject pickupObject = CreateGameObject("PickupPrefab");
        SphereCollider collider = pickupObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        BulletTokenPickup pickup = pickupObject.AddComponent<BulletTokenPickup>();
        return pickup;
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

    private LinkedTokenData CreateLinkedToken(string itemId, string pickupDisplay, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.PickupDisplayTextOverride = pickupDisplay;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private RemnantPickupTokenData CreateRemnantToken(string tokenId, string displayText, int remnantAmount)
    {
        RemnantPickupTokenData token = ScriptableObject.CreateInstance<RemnantPickupTokenData>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        SetPrivateField(token, "remnantAmount", remnantAmount);
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private HealingPickupTokenData CreateHealingToken(string tokenId, string displayText, float healingAmount)
    {
        HealingPickupTokenData token = ScriptableObject.CreateInstance<HealingPickupTokenData>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        SetPrivateField(token, "healingAmount", healingAmount);
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
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
