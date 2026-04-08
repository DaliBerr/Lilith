using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class CharBulletVisualPresenterTests
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
    public void ApplyCompiledAppearance_UsesCompiledTextColorAndSyncsSecondaryVisuals()
    {
        CharBullet bullet = CreateVisualBullet(out CharBulletVisualPresenter presenter, out TMP_Text mainGlyph, out TMP_Text shadowGlyph, out SpriteRenderer coreRenderer, out SpriteRenderer resultRenderer, out TrailRenderer trailRenderer);
        CharBulletVisualLibrary library = CreateVisualLibrary(Color.cyan, Color.red, overlayAlpha: 0.6f);
        AssignVisualLibrary(presenter, library);

        Assert.That(bullet.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);

        bullet.TrySetText("Ignis");
        bullet.TrySetFontSize(32f);

        CompiledAttack compiledAttack = new()
        {
            CoreType = AttackCoreType.Fire,
            ResultType = AttackResultType.Explosion,
            HasTextColorOverride = true,
            TextColor = Color.red,
        };

        Assert.That(presenter.ApplyCompiledAppearance(compiledAttack, bullet), Is.True);
        Assert.That(mainGlyph.color, Is.EqualTo(Color.red));
        Assert.That(shadowGlyph.text, Is.EqualTo("Ignis"));
        Assert.That(shadowGlyph.rectTransform.sizeDelta.x, Is.EqualTo(mainGlyph.rectTransform.sizeDelta.x).Within(0.0001f));
        Assert.That(coreRenderer.enabled, Is.True);
        Assert.That(resultRenderer.enabled, Is.True);
        Assert.That(coreRenderer.sprite, Is.Not.Null);
        Assert.That(resultRenderer.sprite, Is.Not.Null);
        Assert.That(trailRenderer.time, Is.GreaterThan(0f));
    }

    [Test]
    public void ApplyCompiledAppearance_WithoutTextOverride_UsesCoreFallbackTint()
    {
        CharBullet bullet = CreateVisualBullet(out CharBulletVisualPresenter presenter, out TMP_Text mainGlyph, out _, out SpriteRenderer coreRenderer, out _, out _);
        CharBulletVisualLibrary library = CreateVisualLibrary(new Color(0.3f, 0.7f, 1f, 1f), Color.white, overlayAlpha: 0.5f);
        AssignVisualLibrary(presenter, library);

        Assert.That(bullet.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);

        CompiledAttack compiledAttack = new()
        {
            CoreType = AttackCoreType.Fire,
            ResultType = AttackResultType.DirectDamage,
            HasTextColorOverride = false,
        };

        Assert.That(presenter.ApplyCompiledAppearance(compiledAttack, bullet), Is.True);
        Assert.That(mainGlyph.color, Is.EqualTo(new Color(0.3f, 0.7f, 1f, 1f)));
        Assert.That(coreRenderer.color.r, Is.EqualTo(mainGlyph.color.r).Within(0.25f));
        Assert.That(coreRenderer.enabled, Is.True);
    }

    [Test]
    public void ApplyCompiledAppearance_WithoutLibrary_DoesNotBreakBulletVisuals()
    {
        CharBullet bullet = CreateVisualBullet(out CharBulletVisualPresenter presenter, out TMP_Text mainGlyph, out TMP_Text shadowGlyph, out _, out _, out _);

        Assert.That(bullet.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(presenter.TryCacheBindings(overwriteExisting: true), Is.True);

        bullet.TrySetText("Rune");
        bullet.TrySetTextColor(Color.green);

        CompiledAttack compiledAttack = new()
        {
            CoreType = AttackCoreType.Fire,
            ResultType = AttackResultType.DirectDamage,
        };

        Assert.That(() => presenter.ApplyCompiledAppearance(compiledAttack, bullet), Throws.Nothing);
        Assert.That(mainGlyph.text, Is.EqualTo("Rune"));
        Assert.That(shadowGlyph.text, Is.EqualTo("Rune"));
    }

    private CharBullet CreateVisualBullet(out CharBulletVisualPresenter presenter, out TMP_Text mainGlyph, out TMP_Text shadowGlyph, out SpriteRenderer coreRenderer, out SpriteRenderer resultRenderer, out TrailRenderer trailRenderer)
    {
        GameObject root = CreateGameObject("CharBullet");
        root.AddComponent<Rigidbody>();

        GameObject textObject = CreateGameObject("Text");
        RectTransform textTransform = textObject.AddComponent<RectTransform>();
        textTransform.SetParent(root.transform, false);
        textTransform.localEulerAngles = new Vector3(90f, 0f, 0f);
        textTransform.sizeDelta = Vector2.one * 16f;

        GameObject glyphObject = CreateGameObject("Glyph");
        glyphObject.transform.SetParent(textObject.transform, false);
        mainGlyph = glyphObject.AddComponent<TextMeshPro>();
        mainGlyph.rectTransform.sizeDelta = Vector2.one * 20f;

        GameObject shadowObject = CreateGameObject("GlyphShadow");
        shadowObject.transform.SetParent(textObject.transform, false);
        shadowGlyph = shadowObject.AddComponent<TextMeshPro>();
        shadowGlyph.rectTransform.sizeDelta = Vector2.one * 20f;

        GameObject coreObject = CreateGameObject("RuneBaseCore");
        coreObject.transform.SetParent(root.transform, false);
        coreObject.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        coreRenderer = coreObject.AddComponent<SpriteRenderer>();

        GameObject resultObject = CreateGameObject("RuneBaseResult");
        resultObject.transform.SetParent(root.transform, false);
        resultObject.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        resultRenderer = resultObject.AddComponent<SpriteRenderer>();

        GameObject trailObject = CreateGameObject("Trail");
        trailObject.transform.SetParent(root.transform, false);
        trailRenderer = trailObject.AddComponent<TrailRenderer>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.transform.SetParent(root.transform, false);
        SphereCollider impactCollider = colliderObject.AddComponent<SphereCollider>();
        impactCollider.isTrigger = true;
        impactCollider.radius = 0.5f;

        root.AddComponent<CharGlyphPresenter>();
        presenter = root.AddComponent<CharBulletVisualPresenter>();
        CharBullet bullet = root.AddComponent<CharBullet>();
        return bullet;
    }

    private CharBulletVisualLibrary CreateVisualLibrary(Color coreTint, Color resultTint, float overlayAlpha)
    {
        CharBulletVisualLibrary library = ScriptableObject.CreateInstance<CharBulletVisualLibrary>();
        createdObjects.Add(library);

        Sprite coreSprite = CreateSprite("CoreSprite");
        Sprite resultSprite = CreateSprite("ResultSprite");
        Gradient trailGradient = new();
        trailGradient.SetKeys(
            new[]
            {
                new GradientColorKey(coreTint, 0f),
                new GradientColorKey(resultTint, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f),
            });

        SetLibraryEntries(
            library,
            new List<CharBulletVisualLibrary.CoreVisualEntry>
            {
                new()
                {
                    coreType = AttackCoreType.Fire,
                    baseSprite = coreSprite,
                    fallbackTint = coreTint,
                    baseScale = 1.25f,
                    trailGradient = trailGradient,
                },
            },
            new List<CharBulletVisualLibrary.ResultVisualEntry>
            {
                new()
                {
                    resultType = AttackResultType.DirectDamage,
                    overlaySprite = resultSprite,
                    overlayScale = 0.8f,
                    overlayAlpha = overlayAlpha,
                    rotationSpeed = 25f,
                    pulseAmplitude = 0.03f,
                },
                new()
                {
                    resultType = AttackResultType.Explosion,
                    overlaySprite = resultSprite,
                    overlayScale = 0.95f,
                    overlayAlpha = overlayAlpha,
                    rotationSpeed = 40f,
                    pulseAmplitude = 0.05f,
                },
            });

        return library;
    }

    private void AssignVisualLibrary(CharBulletVisualPresenter presenter, CharBulletVisualLibrary library)
    {
        FieldInfo field = typeof(CharBulletVisualPresenter).GetField("visualLibrary", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(presenter, library);
    }

    private void SetLibraryEntries(CharBulletVisualLibrary library, List<CharBulletVisualLibrary.CoreVisualEntry> coreEntries, List<CharBulletVisualLibrary.ResultVisualEntry> resultEntries)
    {
        FieldInfo coreField = typeof(CharBulletVisualLibrary).GetField("coreVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo resultField = typeof(CharBulletVisualLibrary).GetField("resultVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(coreField, Is.Not.Null);
        Assert.That(resultField, Is.Not.Null);
        coreField.SetValue(library, coreEntries);
        resultField.SetValue(library, resultEntries);
    }

    private Sprite CreateSprite(string name)
    {
        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false);
        texture.name = name;
        Color[] pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 8f);
        sprite.name = name;
        createdObjects.Add(sprite);
        return sprite;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
