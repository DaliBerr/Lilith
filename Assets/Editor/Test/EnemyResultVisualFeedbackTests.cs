using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyResultVisualFeedbackTests
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
    public void LateUpdate_DefaultStateForcesWhiteBaseWhilePreservingAlpha()
    {
        EnemyResultVisualFeedback feedback = CreateFeedbackTarget(
            out _,
            out SpriteRenderer runeBaseRenderer,
            out SpriteRenderer groundShadowRenderer,
            out TMP_Text glyphText);

        InvokePrivateMethod(feedback, "LateUpdate");

        AssertColorWithAlpha(runeBaseRenderer.color, new Color(1f, 1f, 1f, 0.5f));
        AssertColorWithAlpha(groundShadowRenderer.color, new Color(1f, 1f, 1f, 0.3f));
        AssertColorWithAlpha(glyphText.color, new Color(1f, 1f, 1f, 0.8f));
    }

    [Test]
    public void NotifyControlHitPulse_ShowsYellowAndStunTurnsGold()
    {
        EnemyResultVisualFeedback feedback = CreateFeedbackTarget(
            out EnemyStatusEffectController statusEffects,
            out SpriteRenderer runeBaseRenderer,
            out _,
            out _);

        feedback.NotifyControlHitPulse();
        InvokePrivateMethod(feedback, "LateUpdate");
        AssertColorWithAlpha(runeBaseRenderer.color, new Color(1f, 0.92f, 0.18f, 0.5f));

        Assert.That(statusEffects.RegisterControlHit(1, 1f), Is.True);
        InvokePrivateMethod(feedback, "LateUpdate");
        AssertColorWithAlpha(runeBaseRenderer.color, new Color(1f, 0.8f, 0.12f, 0.5f));
    }

    [Test]
    public void NotifyHealingHitPulse_ShowsGreenPulse()
    {
        EnemyResultVisualFeedback feedback = CreateFeedbackTarget(
            out _,
            out SpriteRenderer runeBaseRenderer,
            out _,
            out _);

        feedback.NotifyHealingHitPulse();
        InvokePrivateMethod(feedback, "LateUpdate");
        AssertColorWithAlpha(runeBaseRenderer.color, new Color(0.32f, 0.95f, 0.45f, 0.5f));
    }

    private EnemyResultVisualFeedback CreateFeedbackTarget(
        out EnemyStatusEffectController statusEffects,
        out SpriteRenderer runeBaseRenderer,
        out SpriteRenderer groundShadowRenderer,
        out TMP_Text glyphText)
    {
        GameObject root = CreateGameObject("EnemyRoot");
        root.tag = "Enemy_Object";
        root.AddComponent<BaseCharEnemyNorm1>();
        statusEffects = root.AddComponent<EnemyStatusEffectController>();
        CharEnemyVisualPresenter visualPresenter = root.AddComponent<CharEnemyVisualPresenter>();

        GameObject textObject = CreateGameObject("Text");
        textObject.transform.SetParent(root.transform, false);

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphObject.transform.SetParent(textObject.transform, false);
        glyphText = glyphObject.AddComponent<TextMeshPro>();
        glyphText.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.transform.SetParent(root.transform, false);
        BoxCollider groundingCollider = colliderObject.AddComponent<BoxCollider>();
        groundingCollider.size = new Vector3(16f, 16f, 16f);

        GameObject runeBaseObject = CreateGameObject("RuneBaseCore");
        runeBaseObject.transform.SetParent(root.transform, false);
        runeBaseRenderer = runeBaseObject.AddComponent<SpriteRenderer>();
        runeBaseRenderer.color = new Color(0.2f, 0.4f, 0.8f, 0.5f);

        GameObject groundShadowObject = CreateGameObject("GroundShadow");
        groundShadowObject.transform.SetParent(root.transform, false);
        groundShadowRenderer = groundShadowObject.AddComponent<SpriteRenderer>();
        groundShadowRenderer.color = new Color(0f, 0f, 0f, 0.3f);

        Assert.That(visualPresenter.TryCacheBindings(overwriteExisting: true), Is.True);

        EnemyResultVisualFeedback feedback = root.AddComponent<EnemyResultVisualFeedback>();
        Assert.That(feedback.TryCacheBindings(), Is.True);
        InvokePrivateMethod(feedback, "CaptureBaselineAlpha");
        return feedback;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, null);
    }

    private static void AssertColorWithAlpha(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.03f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.03f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.03f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}
