using System.Collections.Generic;
using TMPro;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;

public sealed class CharGlyphPresenterTests
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
    public void TryCacheBindings_FindsNestedGlyphText()
    {
        CharGlyphPresenter presenter = CreatePresenter(out TMP_Text glyphText);

        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.GlyphText, Is.SameAs(glyphText));
    }

    [Test]
    public void SetDisplayText_RefreshesBoundGlyphText()
    {
        CharGlyphPresenter presenter = CreatePresenter(out TMP_Text glyphText);

        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.SetDisplayText("坚"), Is.True);
        Assert.That(presenter.DefaultDisplayText, Is.EqualTo("坚"));
        Assert.That(glyphText.text, Is.EqualTo("坚"));
    }

    [Test]
    public void Prefabs_UseSingleGlyphPresenterContract()
    {
        AssertPrefabContract("Assets/Prefabs/BaseCharObject.prefab", "火", expectedLayer: 0);
        AssertPrefabContract("Assets/Prefabs/Enemy/CharEnemy.prefab", "坚", expectedLayer: 9);
    }

    private CharGlyphPresenter CreatePresenter(out TMP_Text glyphText)
    {
        GameObject root = CreateGameObject("EnemyRoot");
        CharGlyphPresenter presenter = root.AddComponent<CharGlyphPresenter>();

        GameObject textObject = CreateGameObject("Text");
        textObject.AddComponent<RectTransform>();
        textObject.transform.SetParent(root.transform, false);

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphObject.AddComponent<RectTransform>();
        glyphObject.transform.SetParent(textObject.transform, false);
        glyphText = glyphObject.AddComponent<TextMeshPro>();

        return presenter;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void AssertPrefabContract(string prefabPath, string expectedDefaultDisplayText, int expectedLayer)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            CharGlyphPresenter presenter = prefabRoot.GetComponent<CharGlyphPresenter>();
            Assert.That(presenter, Is.Not.Null, $"{prefabPath} is missing CharGlyphPresenter.");

            Transform textContainer = prefabRoot.transform.Find("Text");
            Assert.That(textContainer, Is.Not.Null, $"{prefabPath} is missing Text container.");
            Assert.That(textContainer.GetComponent<RectTransform>(), Is.Not.Null, $"{prefabPath} Text must remain a RectTransform container.");
            Assert.That(textContainer.GetComponent<TMP_Text>(), Is.Null, $"{prefabPath} Text container should not hold TMP_Text directly.");

            Transform glyph = prefabRoot.transform.Find("Text/Glyph");
            Assert.That(glyph, Is.Not.Null, $"{prefabPath} is missing Text/Glyph.");
            Assert.That(glyph.gameObject.layer, Is.EqualTo(expectedLayer), $"{prefabPath} Glyph layer mismatch.");

            TMP_Text[] texts = prefabRoot.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            Assert.That(texts, Has.Length.EqualTo(1), $"{prefabPath} should expose exactly one TMP_Text.");

            Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);
            Assert.That(presenter.GlyphText, Is.SameAs(texts[0]));
            Assert.That(presenter.DefaultDisplayText, Is.EqualTo(expectedDefaultDisplayText));
            Assert.That(texts[0].text, Is.EqualTo(expectedDefaultDisplayText));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
