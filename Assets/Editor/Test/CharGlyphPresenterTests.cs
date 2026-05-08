using System.Collections.Generic;
using TMPro;
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
}
