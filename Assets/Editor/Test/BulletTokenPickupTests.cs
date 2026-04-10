using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BulletTokenPickupTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingWallet();

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
    public void OnTriggerEnter_PlayerInventoryHasSpace_AddsTokenAndDestroysPickup()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        CoreTokenData token = CreateToken<CoreTokenData>("pickup_fire", "Pickup Fire");

        Assert.That(pickup.TrySetToken(token), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(inventory.GetToken(0), Is.SameAs(token));
        Assert.That(pickup == null, Is.True);
    }

    [Test]
    public void OnTriggerEnter_LinkedTokenWithoutContinuousSpace_KeepsPickupInWorld()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        CoreTokenData filler = CreateToken<CoreTokenData>("filler", "Fill");

        for (int i = 0; i < PlayerBulletTokenInventory.Capacity; i++)
        {
            if ((i % PlayerBulletTokenInventory.Columns) == PlayerBulletTokenInventory.Columns - 1)
            {
                continue;
            }

            Assert.That(inventory.SetToken(i, filler), Is.True);
        }

        Assert.That(pickup.TrySetToken(linked), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(pickup != null, Is.True);
        Assert.That(pickup.IsCollected, Is.False);
        Assert.That(inventory.FindFirstEmptySlot(), Is.EqualTo(PlayerBulletTokenInventory.Columns - 1));
    }

    [Test]
    public void OnTriggerEnter_LinkedTokenWithContinuousSpace_FillsWholeSpan()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));

        Assert.That(pickup.TrySetToken(linked), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(inventory.GetCell(0).item, Is.SameAs(linked));
        Assert.That(inventory.GetCell(1).item, Is.SameAs(linked));
        Assert.That(inventory.GetCell(0).isAnchor, Is.True);
        Assert.That(inventory.GetCell(1).isAnchor, Is.False);
        Assert.That(pickup == null, Is.True);
    }

    [Test]
    public void OnTriggerEnter_RemnantPickup_AddsWalletCountAndDestroysPickup()
    {
        PlayerRemnantWallet wallet = CreateWallet();
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        RemnantPickupTokenData token = CreateRemnantToken("pickup_remnant", "Remnant", 3);

        Assert.That(pickup.TrySetToken(token), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(wallet.CurrentRemnants, Is.EqualTo(3));
        Assert.That(pickup == null, Is.True);
    }

    [Test]
    public void OnTriggerEnter_HealingPickupAtFullHealth_IsConsumedWithoutHealing()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        PlayerHealth playerHealth = inventory.gameObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 100f);
        SetPrivateField(playerHealth, "currentHealth", 100f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        BulletTokenPickup pickup = CreatePickup();
        HealingPickupTokenData token = CreateHealingToken("pickup_heal", "Heal", 25f);

        Assert.That(pickup.TrySetToken(token), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(pickup == null, Is.True);
    }

    [Test]
    public void OnTriggerEnter_NonPlayerCollider_DoesNothing()
    {
        GameObject otherObject = CreateGameObject("Other");
        BoxCollider otherCollider = otherObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        CoreTokenData token = CreateToken<CoreTokenData>("pickup_fire", "Pickup Fire");

        Assert.That(pickup.TrySetToken(token), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", otherCollider);

        Assert.That(pickup != null, Is.True);
        Assert.That(pickup.IsCollected, Is.False);
        Assert.That(pickup.Token, Is.SameAs(token));
    }

    [Test]
    public void Prefab_GlyphAndShadowCarryGameplayBillboards()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Bullet/BulletTokenPickup.prefab");

        try
        {
            Transform glyph = prefabRoot.transform.Find("Glyph");
            Transform shadow = prefabRoot.transform.Find("Shadow");

            Assert.That(glyph, Is.Not.Null);
            Assert.That(shadow, Is.Not.Null);
            Assert.That(glyph.GetComponent<GameplayBillboard>(), Is.Not.Null);
            Assert.That(shadow.GetComponent<GameplayBillboard>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private PlayerBulletTokenInventory CreateInventory()
    {
        GameObject playerObject = CreateGameObject("Player");
        PlayerBulletTokenInventory inventory = playerObject.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        return inventory;
    }

    private BulletTokenPickup CreatePickup()
    {
        GameObject pickupObject = CreateGameObject("Pickup");
        SphereCollider collider = pickupObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        return pickupObject.AddComponent<BulletTokenPickup>();
    }

    private PlayerRemnantWallet CreateWallet()
    {
        DestroyExistingWallet();
        GameObject walletObject = CreateGameObject("Wallet");
        PlayerRemnantWallet wallet = walletObject.AddComponent<PlayerRemnantWallet>();
        wallet.TrySetRemnants(0, out _);
        return wallet;
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

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
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

    private static void DestroyExistingWallet()
    {
        PlayerRemnantWallet existingWallet = Object.FindFirstObjectByType<PlayerRemnantWallet>();
        if (existingWallet != null)
        {
            Object.DestroyImmediate(existingWallet.gameObject);
        }
    }
}
