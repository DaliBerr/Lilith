using System.Collections.Generic;
using Kernel.Bullet;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class CharBulletPrefabTests
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
    public void TryCacheBindings_PrefersTextContainerForSizeTarget()
    {
        GameObject root = CreateGameObject("CharBullet");
        root.AddComponent<Rigidbody>();

        GameObject textObject = CreateGameObject("Text");
        textObject.AddComponent<RectTransform>();
        textObject.transform.SetParent(root.transform, false);

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphObject.transform.SetParent(textObject.transform, false);
        TMP_Text glyphText = glyphObject.AddComponent<TextMeshPro>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.transform.SetParent(root.transform, false);
        SphereCollider impactCollider = colliderObject.AddComponent<SphereCollider>();
        impactCollider.isTrigger = true;

        CharBullet bullet = root.AddComponent<CharBullet>();

        Assert.That(bullet.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(bullet.GlyphText, Is.SameAs(glyphText));
        Assert.That(bullet.MovementTarget, Is.SameAs(root.transform));
        Assert.That(bullet.SizeTarget, Is.SameAs(textObject.transform));
    }

    [Test]
    public void Prefab_UsesTextGlyphContractAndKeepsRedGlyphTint()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Bullet/CharBullet.prefab");

        try
        {
            CharBullet bullet = prefabRoot.GetComponent<CharBullet>();
            CharGlyphPresenter presenter = prefabRoot.GetComponent<CharGlyphPresenter>();
            CharBulletVisualPresenter visualPresenter = prefabRoot.GetComponent<CharBulletVisualPresenter>();
            Transform textContainer = prefabRoot.transform.Find("Text");
            Transform glyph = prefabRoot.transform.Find("Text/Glyph");
            Transform glyphShadow = prefabRoot.transform.Find("Text/GlyphShadow");
            Transform runeBaseCore = prefabRoot.transform.Find("RuneBaseCore");
            Transform runeBaseResult = prefabRoot.transform.Find("RuneBaseResult");
            Transform trail = prefabRoot.transform.Find("Trail");
            TMP_Text glyphText = glyph != null ? glyph.GetComponent<TMP_Text>() : null;

            Assert.That(bullet, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(visualPresenter, Is.Not.Null);
            Assert.That(textContainer, Is.Not.Null);
            Assert.That(textContainer.GetComponent<GameplayBillboard>(), Is.Not.Null);
            Assert.That(glyphText, Is.Not.Null);
            Assert.That(glyphShadow, Is.Not.Null);
            Assert.That(glyphShadow.GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(runeBaseCore, Is.Not.Null);
            Assert.That(runeBaseCore.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(runeBaseResult, Is.Not.Null);
            Assert.That(runeBaseResult.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(trail, Is.Not.Null);
            Assert.That(trail.GetComponent<TrailRenderer>(), Is.Not.Null);

            Assert.That(bullet.TryCacheBindings(overwriteExisting: true), Is.True);
            Assert.That(bullet.GlyphText, Is.SameAs(glyphText));
            Assert.That(bullet.MovementTarget, Is.SameAs(prefabRoot.transform));
            Assert.That(bullet.SizeTarget, Is.SameAs(textContainer));
            Assert.That(glyphText.color, Is.EqualTo(Color.red));
            Assert.That(presenter.DefaultDisplayText, Is.EqualTo("火"));
            Assert.That(visualPresenter.TryCacheBindings(overwriteExisting: true), Is.True);
            Assert.That(Quaternion.Angle(glyph.localRotation, Quaternion.identity), Is.LessThan(1f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
