using System.Collections.Generic;
using TMPro;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharEnemyVisualPresenterTests
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
    public void RefreshPresentation_LayoutsTextBaseAndShadowFromGroundingColliderBottom()
    {
        CharEnemyVisualPresenter presenter = CreatePresenter(out RectTransform textContainer, out RectTransform glyphRect, out SpriteRenderer runeBaseRenderer, out SpriteRenderer groundShadowRenderer);

        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.RefreshPresentation(), Is.True);

        Assert.That(textContainer.localPosition.y, Is.EqualTo(-2.4f).Within(0.001f));
        Assert.That(glyphRect.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(runeBaseRenderer.transform.localPosition.y, Is.EqualTo(-7.85f).Within(0.001f));
        Assert.That(groundShadowRenderer.transform.localPosition.y, Is.EqualTo(-7.95f).Within(0.001f));
        Assert.That(runeBaseRenderer.transform.localScale, Is.EqualTo(Vector3.one * 16f));
        Assert.That(groundShadowRenderer.transform.localScale, Is.EqualTo(Vector3.one * 18.4f));
        Assert.That(Quaternion.Angle(runeBaseRenderer.transform.localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(groundShadowRenderer.transform.localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.001f));
    }

    [Test]
    public void Prefab_ProvidesStableEnemyVisualContract()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy/CharEnemy.prefab");

        try
        {
            CharEnemyVisualPresenter presenter = prefabRoot.GetComponent<CharEnemyVisualPresenter>();
            Transform textContainer = prefabRoot.transform.Find("Text");
            Transform glyph = prefabRoot.transform.Find("Text/Glyph");
            Transform runeBaseCore = prefabRoot.transform.Find("RuneBaseCore");
            Transform groundShadow = prefabRoot.transform.Find("GroundShadow");
            TMP_Text[] texts = prefabRoot.GetComponentsInChildren<TMP_Text>(includeInactive: true);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(textContainer, Is.Not.Null);
            Assert.That(glyph, Is.Not.Null);
            Assert.That(runeBaseCore, Is.Not.Null);
            Assert.That(groundShadow, Is.Not.Null);
            Assert.That(texts, Has.Length.EqualTo(1));
            Assert.That(Quaternion.Angle(textContainer.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(glyph.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(runeBaseCore.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(groundShadow.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(Quaternion.Angle(runeBaseCore.localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(groundShadow.localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.001f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void ApplyVisualDefinition_GlyphScaleMultiplier_OnlyScalesTextContainer()
    {
        CharEnemyVisualPresenter presenter = CreatePresenter(out RectTransform textContainer, out _, out SpriteRenderer runeBaseRenderer, out SpriteRenderer groundShadowRenderer);

        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.ApplyVisualDefinition(new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = "测试",
            glyphScaleMultiplier = 3f,
            glyphColor = Color.white,
            runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
            groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
        }), Is.True);

        Assert.That(textContainer.localScale, Is.EqualTo(Vector3.one * 3f));
        Assert.That(runeBaseRenderer.transform.localScale, Is.EqualTo(Vector3.one * 16f));
        Assert.That(groundShadowRenderer.transform.localScale, Is.EqualTo(Vector3.one * 18.4f));
    }

    private CharEnemyVisualPresenter CreatePresenter(
        out RectTransform textContainer,
        out RectTransform glyphRect,
        out SpriteRenderer runeBaseRenderer,
        out SpriteRenderer groundShadowRenderer)
    {
        GameObject root = CreateGameObject("CharEnemy");

        GameObject textObject = CreateGameObject("Text");
        textContainer = textObject.AddComponent<RectTransform>();
        textObject.transform.SetParent(root.transform, false);

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphRect = glyphObject.AddComponent<RectTransform>();
        glyphObject.transform.SetParent(textObject.transform, false);
        glyphObject.AddComponent<TextMeshPro>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.transform.SetParent(root.transform, false);
        BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(16f, 16f, 16f);

        GameObject runeBaseObject = CreateGameObject("RuneBaseCore");
        runeBaseObject.transform.SetParent(root.transform, false);
        runeBaseRenderer = runeBaseObject.AddComponent<SpriteRenderer>();

        GameObject groundShadowObject = CreateGameObject("GroundShadow");
        groundShadowObject.transform.SetParent(root.transform, false);
        groundShadowRenderer = groundShadowObject.AddComponent<SpriteRenderer>();

        return root.AddComponent<CharEnemyVisualPresenter>();
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
