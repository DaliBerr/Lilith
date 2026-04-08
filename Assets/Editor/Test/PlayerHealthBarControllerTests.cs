using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerHealthBarControllerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] is GameObject gameObject && gameObject.TryGetComponent(out PlayerHealthBarController controller))
            {
                InvokePrivateMethod(controller, "OnDisable");
            }

            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void DamageEvent_UpdatesCellProgressesUsingTwentyHealthPerCell()
    {
        PlayerHealth playerHealth = CreatePlayerHealth(100f, 100f);
        GameObject cellPrefab = CreateHealthCellPrefab();
        PlayerHealthBarController controller = CreateController(cellPrefab, playerHealth, changeAnimationDuration: 0f);

        bool success = playerHealth.TryApplyDamage(35f, out _, out _);

        Assert.That(success, Is.True);
        Assert.That(controller.transform.childCount, Is.EqualTo(5));
        AssertCellProgress(controller.transform.GetChild(0).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(controller.transform.GetChild(1).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(controller.transform.GetChild(2).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(controller.transform.GetChild(3).GetComponent<StrokeRevealUIWord>(), 0.25f);
        AssertCellProgress(controller.transform.GetChild(4).GetComponent<StrokeRevealUIWord>(), 0f);
    }

    [Test]
    public void HealingEvent_AddsCellsWhenTargetMaxHealthRequiresMoreCapacity()
    {
        PlayerHealth playerHealth = CreatePlayerHealth(60f, 25f);
        GameObject cellPrefab = CreateHealthCellPrefab();
        PlayerHealthBarController controller = CreateController(cellPrefab, playerHealth, changeAnimationDuration: 0f);

        Assert.That(controller.transform.childCount, Is.EqualTo(3));

        SetPrivateField(playerHealth, "maxHealth", 100f);
        bool success = playerHealth.TryApplyHealing(30f, out _, out _);

        Assert.That(success, Is.True);
        Assert.That(controller.transform.childCount, Is.EqualTo(5));
        AssertCellProgress(controller.transform.GetChild(0).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(controller.transform.GetChild(1).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(controller.transform.GetChild(2).GetComponent<StrokeRevealUIWord>(), 0.75f);
        AssertCellProgress(controller.transform.GetChild(3).GetComponent<StrokeRevealUIWord>(), 0f);
        AssertCellProgress(controller.transform.GetChild(4).GetComponent<StrokeRevealUIWord>(), 0f);
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

    private GameObject CreateHealthCellPrefab()
    {
        GameObject cellObject = new("Health_Prefab", typeof(RectTransform));
        createdObjects.Add(cellObject);

        StrokeRevealUIWord word = cellObject.AddComponent<StrokeRevealUIWord>();
        word.playOnEnable = StrokeRevealUIWord.AutoPlayMode.None;
        word.strokes = new List<StrokeRevealUIWord.StrokeItem>
        {
            CreateStrokeItem("Stroke1"),
            CreateStrokeItem("Stroke2"),
            CreateStrokeItem("Stroke3"),
            CreateStrokeItem("Stroke4")
        };
        return cellObject;
    }

    private PlayerHealthBarController CreateController(GameObject healthCellPrefab, PlayerHealth targetHealth, float changeAnimationDuration)
    {
        GameObject barObject = new("Bar", typeof(RectTransform));
        createdObjects.Add(barObject);

        PlayerHealthBarController controller = barObject.AddComponent<PlayerHealthBarController>();
        SetPrivateField(controller, "healthCellPrefab", healthCellPrefab);
        SetPrivateField(controller, "targetHealth", targetHealth);
        SetPrivateField(controller, "hpPerCell", 20f);
        SetPrivateField(controller, "changeAnimationDuration", changeAnimationDuration);

        InvokePrivateMethod(controller, "OnEnable");
        return controller;
    }

    private StrokeRevealUIWord.StrokeItem CreateStrokeItem(string name)
    {
        Texture2D texture = new(2, 2);
        texture.name = name + "_Texture";
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        createdObjects.Add(sprite);

        return new StrokeRevealUIWord.StrokeItem
        {
            sprite = sprite,
            revealDirection = Vector2.right,
            duration = 0.1f,
            delayAfter = 0f
        };
    }

    private void AssertCellProgress(StrokeRevealUIWord cell, float expectedProgress)
    {
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.TryGetNormalizedProgress(out float progress), Is.True);
        Assert.That(progress, Is.EqualTo(expectedProgress).Within(0.0001f));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, null);
    }
}
