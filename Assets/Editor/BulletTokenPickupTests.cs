using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;

public sealed class BulletTokenPickupTests
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
    public void OnTriggerEnter_PlayerInventoryFull_KeepsPickupInWorld()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BoxCollider playerCollider = inventory.gameObject.AddComponent<BoxCollider>();
        BulletTokenPickup pickup = CreatePickup();
        CoreTokenData existingToken = CreateToken<CoreTokenData>("existing", "Existing");
        CoreTokenData droppedToken = CreateToken<CoreTokenData>("dropped", "Dropped");

        for (int i = 0; i < PlayerBulletTokenInventory.Capacity; i++)
        {
            Assert.That(inventory.SetToken(i, existingToken), Is.True);
        }

        Assert.That(pickup.TrySetToken(droppedToken), Is.True);

        InvokePrivateMethod(pickup, "OnTriggerEnter", playerCollider);

        Assert.That(inventory.FindFirstEmptySlot(), Is.EqualTo(-1));
        Assert.That(pickup != null, Is.True);
        Assert.That(pickup.IsCollected, Is.False);
        Assert.That(pickup.Token, Is.SameAs(droppedToken));
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

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
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
}
