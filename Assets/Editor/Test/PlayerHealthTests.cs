using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class PlayerHealthTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();
    private readonly List<IDisposable> subscriptions = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = subscriptions.Count - 1; i >= 0; i--)
        {
            subscriptions[i]?.Dispose();
        }

        subscriptions.Clear();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void TryApplyDamage_PublishesPlayerHealthChangedEvent()
    {
        PlayerHealth playerHealth = CreatePlayerHealth(100f, 100f);
        List<PlayerHealthChangedEvent> events = new();
        subscriptions.Add(EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(events.Add));

        bool success = playerHealth.TryApplyDamage(35f, out float remainingHealth, out bool isDead);

        Assert.That(success, Is.True);
        Assert.That(remainingHealth, Is.EqualTo(65f).Within(0.0001f));
        Assert.That(isDead, Is.False);
        Assert.That(events.Count, Is.EqualTo(1));
        Assert.That(events[0].source, Is.SameAs(playerHealth));
        Assert.That(events[0].previousHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(events[0].currentHealth, Is.EqualTo(65f).Within(0.0001f));
        Assert.That(events[0].maxHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(events[0].delta, Is.EqualTo(-35f).Within(0.0001f));
        Assert.That(events[0].isDead, Is.False);
    }

    [Test]
    public void TryApplyHealing_PublishesPlayerHealthChangedEventAndClampsToMaxHealth()
    {
        PlayerHealth playerHealth = CreatePlayerHealth(100f, 70f);
        List<PlayerHealthChangedEvent> events = new();
        subscriptions.Add(EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(events.Add));

        bool success = playerHealth.TryApplyHealing(50f, out float resultingHealth, out bool isDead);

        Assert.That(success, Is.True);
        Assert.That(resultingHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(isDead, Is.False);
        Assert.That(events.Count, Is.EqualTo(1));
        Assert.That(events[0].previousHealth, Is.EqualTo(70f).Within(0.0001f));
        Assert.That(events[0].currentHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(events[0].delta, Is.EqualTo(30f).Within(0.0001f));
        Assert.That(events[0].isDead, Is.False);
    }

    [Test]
    public void TryApplyHealing_ReturnsFalseWithoutPublishingWhenHealingIsInvalid()
    {
        PlayerHealth fullHealthPlayer = CreatePlayerHealth(100f, 100f);
        PlayerHealth deadPlayer = CreatePlayerHealth(100f, 0f);
        List<PlayerHealthChangedEvent> events = new();
        subscriptions.Add(EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(events.Add));

        Assert.That(fullHealthPlayer.TryApplyHealing(10f, out _, out _), Is.False);
        Assert.That(deadPlayer.TryApplyHealing(10f, out _, out _), Is.False);
        Assert.That(events.Count, Is.EqualTo(0));
    }

    [Test]
    public void TryApplyDamage_PlayerDies_PublishesPlayerDiedEventOnlyOnce()
    {
        PlayerHealth playerHealth = CreatePlayerHealth(100f, 100f);
        List<PlayerDiedEvent> deathEvents = new();
        subscriptions.Add(EventManager.eventBus.Subscribe<PlayerDiedEvent>(deathEvents.Add));

        bool firstDamageApplied = playerHealth.TryApplyDamage(100f, out float remainingHealth, out bool isDead);
        bool secondDamageApplied = playerHealth.TryApplyDamage(5f, out _, out _);

        Assert.That(firstDamageApplied, Is.True);
        Assert.That(secondDamageApplied, Is.False);
        Assert.That(remainingHealth, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(isDead, Is.True);
        Assert.That(deathEvents.Count, Is.EqualTo(1));
        Assert.That(deathEvents[0].source, Is.SameAs(playerHealth));
        Assert.That(deathEvents[0].previousHealth, Is.EqualTo(100f).Within(0.0001f));
        Assert.That(deathEvents[0].currentHealth, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(deathEvents[0].maxHealth, Is.EqualTo(100f).Within(0.0001f));
    }

    private PlayerHealth CreatePlayerHealth(float maxHealth, float currentHealth)
    {
        GameObject gameObject = new("Player");
        createdObjects.Add(gameObject);

        PlayerHealth playerHealth = gameObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", maxHealth);
        SetPrivateField(playerHealth, "currentHealth", Mathf.Clamp(currentHealth, 0f, maxHealth));
        SetPrivateField(playerHealth, "hasInitializedHealth", true);
        return playerHealth;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
