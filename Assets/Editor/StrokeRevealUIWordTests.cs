using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class StrokeRevealUIWordTests
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
    public void ResetState_BuildsRuntimeStrokeChildrenOnceAndAppliesConfiguredSprites()
    {
        Material material = CreateStrokeMaterial();
        Sprite spriteOne = CreateSprite("StrokeOne");
        Sprite spriteTwo = CreateSprite("StrokeTwo");

        GameObject wordObject = CreateRectTransformObject("Word");
        StrokeRevealUIWord word = wordObject.AddComponent<StrokeRevealUIWord>();
        word.strokeBaseMaterial = material;
        word.softness = 0.05f;
        word.strokes = new List<StrokeRevealUIWord.StrokeItem>
        {
            new() { sprite = spriteOne, revealDirection = Vector2.right, duration = 1f, delayAfter = 0f },
            new() { sprite = spriteTwo, revealDirection = Vector2.down, duration = 1f, delayAfter = 0f }
        };

        word.ResetState(false);

        Transform runtimeRoot = wordObject.transform.Find("__RuntimeStrokes");
        Assert.That(runtimeRoot, Is.Not.Null);
        Assert.That(runtimeRoot.childCount, Is.EqualTo(2));

        StrokeRevealUIImage firstStroke = runtimeRoot.GetChild(0).GetComponent<StrokeRevealUIImage>();
        StrokeRevealUIImage secondStroke = runtimeRoot.GetChild(1).GetComponent<StrokeRevealUIImage>();
        Image firstImage = runtimeRoot.GetChild(0).GetComponent<Image>();
        Image secondImage = runtimeRoot.GetChild(1).GetComponent<Image>();

        Assert.That(firstImage.sprite, Is.SameAs(spriteOne));
        Assert.That(secondImage.sprite, Is.SameAs(spriteTwo));
        Assert.That(firstStroke.baseMaterial, Is.SameAs(material));
        Assert.That(secondStroke.baseMaterial, Is.SameAs(material));
        Assert.That(firstStroke.revealDirection, Is.EqualTo(Vector2.right));
        Assert.That(secondStroke.revealDirection, Is.EqualTo(Vector2.down));
        Assert.That(firstStroke.softness, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(secondStroke.softness, Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(firstStroke.progress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(secondStroke.progress, Is.EqualTo(0f).Within(0.0001f));

        word.ResetState(true);

        runtimeRoot = wordObject.transform.Find("__RuntimeStrokes");
        Assert.That(runtimeRoot.childCount, Is.EqualTo(2), "Runtime strokes should not be duplicated on repeated resets.");
        Assert.That(runtimeRoot.GetChild(0).GetComponent<StrokeRevealUIImage>().progress, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(runtimeRoot.GetChild(1).GetComponent<StrokeRevealUIImage>().progress, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void BuildPlayOrder_ReversesHideOrderOnlyWhenConfigured()
    {
        GameObject wordObject = CreateRectTransformObject("Word");
        StrokeRevealUIWord word = wordObject.AddComponent<StrokeRevealUIWord>();
        word.strokes = new List<StrokeRevealUIWord.StrokeItem>
        {
            new(),
            new(),
            new()
        };

        MethodInfo buildPlayOrder = typeof(StrokeRevealUIWord).GetMethod("BuildPlayOrder", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(buildPlayOrder, Is.Not.Null);

        List<int> revealOrder = (List<int>)buildPlayOrder.Invoke(word, new object[] { true });
        List<int> hideOrder = (List<int>)buildPlayOrder.Invoke(word, new object[] { false });

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, revealOrder);
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, hideOrder);

        word.hideInReverseStrokeOrder = false;
        hideOrder = (List<int>)buildPlayOrder.Invoke(word, new object[] { false });

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, hideOrder);
    }

    [Test]
    public void TrySetStrokeProgress_OnlyUpdatesTargetStroke()
    {
        StrokeRevealUIWord word = CreateWordWithTwoStrokes(out _, out _);
        word.ResetState(false);

        bool success = word.TrySetStrokeProgress(1, 0.65f);

        Assert.That(success, Is.True);
        Assert.That(word.TryGetStrokeProgress(0, out float firstProgress), Is.True);
        Assert.That(word.TryGetStrokeProgress(1, out float secondProgress), Is.True);
        Assert.That(firstProgress, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(secondProgress, Is.EqualTo(0.65f).Within(0.0001f));
        Assert.That(word.StrokeCount, Is.EqualTo(2));
    }

    [Test]
    public void SetStrokeProgresses_AppliesPerStrokeProgressValues()
    {
        StrokeRevealUIWord word = CreateWordWithTwoStrokes(out _, out _);
        word.ResetState(false);

        word.SetStrokeProgresses(new[] { 0.2f, 0.8f });

        Assert.That(word.TryGetStrokeProgress(0, out float firstProgress), Is.True);
        Assert.That(word.TryGetStrokeProgress(1, out float secondProgress), Is.True);
        Assert.That(firstProgress, Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(secondProgress, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void SetNormalizedProgress_DistributesProgressAcrossStrokesInOrder()
    {
        StrokeRevealUIWord word = CreateWordWithTwoStrokes(out _, out _);
        word.ResetState(false);

        word.SetNormalizedProgress(0.75f);

        Assert.That(word.TryGetStrokeProgress(0, out float firstProgress), Is.True);
        Assert.That(word.TryGetStrokeProgress(1, out float secondProgress), Is.True);
        Assert.That(word.TryGetNormalizedProgress(out float normalizedProgress), Is.True);
        Assert.That(firstProgress, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(secondProgress, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(normalizedProgress, Is.EqualTo(0.75f).Within(0.0001f));
    }

    private Material CreateStrokeMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shader/UIStrokeReveal.shader");
        Assert.That(shader, Is.Not.Null, "UIStrokeReveal shader must exist for the runtime material test.");

        Material material = new Material(shader);
        createdObjects.Add(material);
        return material;
    }

    private Sprite CreateSprite(string name)
    {
        Texture2D texture = new Texture2D(2, 2);
        texture.name = name + "_Texture";
        texture.SetPixels(new[]
        {
            Color.white,
            Color.white,
            Color.white,
            Color.white
        });
        texture.Apply();
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        createdObjects.Add(sprite);
        return sprite;
    }

    private StrokeRevealUIWord CreateWordWithTwoStrokes(out Sprite spriteOne, out Sprite spriteTwo)
    {
        Material material = CreateStrokeMaterial();
        spriteOne = CreateSprite("StrokeOne");
        spriteTwo = CreateSprite("StrokeTwo");

        GameObject wordObject = CreateRectTransformObject("Word");
        StrokeRevealUIWord word = wordObject.AddComponent<StrokeRevealUIWord>();
        word.strokeBaseMaterial = material;
        word.softness = 0.05f;
        word.strokes = new List<StrokeRevealUIWord.StrokeItem>
        {
            new() { sprite = spriteOne, revealDirection = Vector2.right, duration = 1f, delayAfter = 0f },
            new() { sprite = spriteTwo, revealDirection = Vector2.down, duration = 1f, delayAfter = 0f }
        };

        return word;
    }

    private GameObject CreateRectTransformObject(string name)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
