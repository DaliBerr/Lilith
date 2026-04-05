using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class BackPackInventoryTests
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
    public void Inventory_AlwaysInitializesToFixedCapacity()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();

        inventory.EnsureInitialized();

        Assert.That(inventory.Slots.Count, Is.EqualTo(PlayerBulletTokenInventory.Capacity));
        Assert.That(inventory.FindFirstEmptySlot(), Is.EqualTo(0));
    }

    [Test]
    public void Inventory_AllowsDuplicateTokenReferences()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        CoreTokenData duplicateToken = CreateToken<CoreTokenData>("dup", "Dup");

        inventory.SetToken(0, duplicateToken);
        inventory.SetToken(1, duplicateToken);

        Assert.That(inventory.GetToken(0), Is.SameAs(duplicateToken));
        Assert.That(inventory.GetToken(1), Is.SameAs(duplicateToken));
    }

    [Test]
    public void Inventory_SwapSlots_ExchangesOccupiedAndEmptyEntries()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        CoreTokenData leftToken = CreateToken<CoreTokenData>("left", "Left");

        inventory.SetToken(0, leftToken);
        bool swapped = inventory.SwapSlots(0, 5);

        Assert.That(swapped, Is.True);
        Assert.That(inventory.GetToken(0), Is.Null);
        Assert.That(inventory.GetToken(5), Is.SameAs(leftToken));
    }

    [Test]
    public void SpellBookLayout_CompactsNonEmptyTokensBeforeWritingLoadout()
    {
        CoreTokenData firstToken = CreateToken<CoreTokenData>("fire", "Fire");
        BehaviorTokenData secondToken = CreateToken<BehaviorTokenData>("spread", "Spread");
        ResultTokenData thirdToken = CreateToken<ResultTokenData>("boom", "Boom");

        List<BaseTokenData> compacted = BackPackTokenLayoutUtility.BuildCompactLoadoutTokens(new BaseTokenData[]
        {
            firstToken,
            null,
            secondToken,
            null,
            thirdToken,
        });

        Assert.That(compacted.Count, Is.EqualTo(3));
        Assert.That(compacted[0], Is.SameAs(firstToken));
        Assert.That(compacted[1], Is.SameAs(secondToken));
        Assert.That(compacted[2], Is.SameAs(thirdToken));
    }

    [Test]
    public void SpellBookLayout_OverflowTokensSpillIntoInventoryAfterFirstFive()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        BaseTokenData[] loadoutTokens = new BaseTokenData[7];
        for (int i = 0; i < loadoutTokens.Length; i++)
        {
            loadoutTokens[i] = CreateToken<CoreTokenData>($"token_{i}", $"Token {i}");
        }

        BaseTokenData[] spellBookSlots = new BaseTokenData[5];
        int storedOverflowCount = BackPackTokenLayoutUtility.PopulateSpellBookSlots(loadoutTokens, spellBookSlots, inventory, out int droppedOverflowCount);

        Assert.That(storedOverflowCount, Is.EqualTo(2));
        Assert.That(droppedOverflowCount, Is.EqualTo(0));
        Assert.That(spellBookSlots[0], Is.SameAs(loadoutTokens[0]));
        Assert.That(spellBookSlots[4], Is.SameAs(loadoutTokens[4]));
        Assert.That(inventory.GetToken(0), Is.SameAs(loadoutTokens[5]));
        Assert.That(inventory.GetToken(1), Is.SameAs(loadoutTokens[6]));
    }

    [Test]
    public void Inventory_TryAddToken_ReturnsFalseWhenInventoryIsFull()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        CoreTokenData existingToken = CreateToken<CoreTokenData>("existing", "Existing");
        CoreTokenData overflowToken = CreateToken<CoreTokenData>("overflow", "Overflow");

        for (int i = 0; i < PlayerBulletTokenInventory.Capacity; i++)
        {
            Assert.That(inventory.SetToken(i, existingToken), Is.True);
        }

        bool added = inventory.TryAddToken(overflowToken, out int insertedIndex);

        Assert.That(added, Is.False);
        Assert.That(insertedIndex, Is.EqualTo(-1));
    }

    private PlayerBulletTokenInventory CreateInventory()
    {
        GameObject gameObject = new("PlayerInventory");
        createdObjects.Add(gameObject);
        PlayerBulletTokenInventory inventory = gameObject.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        return inventory;
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
}
