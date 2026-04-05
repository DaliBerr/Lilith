using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kernel.Bullet;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class CharBulletVisualAssetGenerator
{
    private const int TextureSize = 64;
    private const string SpriteFolder = "Assets/Art/BulletRunes";
    private const string LibraryFolder = "Assets/Data/BulletVisuals";
    private const string LibraryPath = LibraryFolder + "/CharBulletVisualLibrary.asset";
    private const string CharBulletPrefabPath = "Assets/Prefabs/Bullet/CharBullet.prefab";
    private const float TextHeightOffset = 2.5f;
    private const float CoreBaseHeightOffset = -1.0f;
    private const float ResultBaseHeightOffset = -0.5f;

    [MenuItem("Tools/Lilith/Bullet/Generate Char Bullet Visual Assets")]
    private static void GenerateAssets()
    {
        Directory.CreateDirectory(SpriteFolder);
        Directory.CreateDirectory(LibraryFolder);

        Vector2 center = new(TextureSize * 0.5f, TextureSize * 0.5f);

        SaveSprite("RuneCore_Fire", pixels =>
        {
            DrawRing(pixels, center, 20f, 4f, 0.9f);
            DrawLine(pixels, new Vector2(20f, 18f), new Vector2(32f, 48f), 4f, 1f);
            DrawLine(pixels, new Vector2(32f, 48f), new Vector2(44f, 18f), 4f, 1f);
            DrawDisc(pixels, new Vector2(32f, 28f), 4f, 1f);
        });

        SaveSprite("RuneCore_Ice", pixels =>
        {
            DrawPolygon(pixels, center, 19f, 6, 30f, 3.5f, 1f);
            DrawRadials(pixels, center, 6f, 24f, 6, 30f, 2.5f, 0.9f);
        });

        SaveSprite("RuneCore_Thunder", pixels =>
        {
            DrawRing(pixels, center, 21f, 3.5f, 0.7f);
            DrawLine(pixels, new Vector2(24f, 46f), new Vector2(34f, 34f), 4f, 1f);
            DrawLine(pixels, new Vector2(34f, 34f), new Vector2(29f, 34f), 4f, 1f);
            DrawLine(pixels, new Vector2(29f, 34f), new Vector2(39f, 18f), 4f, 1f);
        });

        SaveSprite("RuneCore_Edge", pixels =>
        {
            DrawPolygon(pixels, center, 22f, 4, 45f, 3.5f, 1f);
            DrawLine(pixels, new Vector2(18f, 18f), new Vector2(46f, 46f), 2.5f, 0.8f);
            DrawLine(pixels, new Vector2(46f, 18f), new Vector2(18f, 46f), 2.5f, 0.8f);
        });

        SaveSprite("RuneCore_Light", pixels =>
        {
            DrawRing(pixels, center, 16f, 4f, 1f);
            DrawRadials(pixels, center, 20f, 28f, 8, 0f, 2.5f, 1f);
            DrawDisc(pixels, center, 3f, 1f);
        });

        SaveSprite("RuneCore_Shadow", pixels =>
        {
            DrawRing(pixels, new Vector2(28f, 32f), 16f, 5f, 1f);
            ClearDisc(pixels, new Vector2(35f, 32f), 15f);
            DrawDisc(pixels, new Vector2(24f, 32f), 3f, 0.9f);
            DrawDisc(pixels, new Vector2(38f, 44f), 2f, 0.75f);
        });

        SaveSprite("RuneCore_Toxin", pixels =>
        {
            DrawRing(pixels, center, 18f, 3f, 0.7f);
            DrawDisc(pixels, new Vector2(24f, 38f), 5f, 1f);
            DrawDisc(pixels, new Vector2(40f, 38f), 5f, 1f);
            DrawDisc(pixels, new Vector2(32f, 22f), 5f, 1f);
        });

        SaveSprite("RuneResult_DirectDamage", pixels =>
        {
            DrawLine(pixels, new Vector2(16f, 32f), new Vector2(48f, 32f), 3f, 1f);
            DrawLine(pixels, new Vector2(32f, 16f), new Vector2(32f, 48f), 3f, 1f);
            DrawRing(pixels, center, 10f, 2.5f, 0.8f);
        });

        SaveSprite("RuneResult_Explosion", pixels =>
        {
            DrawRadials(pixels, center, 6f, 24f, 8, 0f, 3f, 1f);
            DrawRing(pixels, center, 12f, 2.5f, 0.8f);
        });

        SaveSprite("RuneResult_StatusEffect", pixels =>
        {
            DrawRing(pixels, center, 14f, 2.5f, 0.9f);
            DrawDisc(pixels, new Vector2(32f, 48f), 3f, 1f);
            DrawDisc(pixels, new Vector2(18f, 26f), 3f, 1f);
            DrawDisc(pixels, new Vector2(46f, 26f), 3f, 1f);
        });

        SaveSprite("RuneResult_SpawnChild", pixels =>
        {
            DrawDisc(pixels, center, 5f, 1f);
            DrawLine(pixels, center, new Vector2(18f, 46f), 2.5f, 0.9f);
            DrawLine(pixels, center, new Vector2(46f, 46f), 2.5f, 0.9f);
            DrawLine(pixels, center, new Vector2(32f, 14f), 2.5f, 0.9f);
            DrawDisc(pixels, new Vector2(18f, 46f), 3f, 1f);
            DrawDisc(pixels, new Vector2(46f, 46f), 3f, 1f);
            DrawDisc(pixels, new Vector2(32f, 14f), 3f, 1f);
        });

        SaveSprite("RuneResult_Split", pixels =>
        {
            DrawLine(pixels, new Vector2(32f, 16f), new Vector2(32f, 34f), 3.5f, 1f);
            DrawLine(pixels, new Vector2(32f, 34f), new Vector2(18f, 48f), 3.5f, 1f);
            DrawLine(pixels, new Vector2(32f, 34f), new Vector2(46f, 48f), 3.5f, 1f);
        });

        CreateOrUpdateVisualLibrary();
        ConfigureCharBulletPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CharBulletVisualAssetGenerator] Generated CharBullet rune sprites, visual library, and prefab visuals.");
    }

    private static void CreateOrUpdateVisualLibrary()
    {
        CharBulletVisualLibrary library = AssetDatabase.LoadAssetAtPath<CharBulletVisualLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<CharBulletVisualLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        List<CharBulletVisualLibrary.CoreVisualEntry> coreEntries = new()
        {
            CreateCoreEntry(AttackCoreType.Fire, "RuneCore_Fire", ParseHtml("#FF5A36"), 1.4f),
            CreateCoreEntry(AttackCoreType.Ice, "RuneCore_Ice", ParseHtml("#69D7FF"), 1.4f),
            CreateCoreEntry(AttackCoreType.Thunder, "RuneCore_Thunder", ParseHtml("#FFD84D"), 1.4f),
            CreateCoreEntry(AttackCoreType.Edge, "RuneCore_Edge", ParseHtml("#E15A68"), 1.3f),
            CreateCoreEntry(AttackCoreType.Light, "RuneCore_Light", ParseHtml("#FFF1A6"), 1.5f),
            CreateCoreEntry(AttackCoreType.Shadow, "RuneCore_Shadow", ParseHtml("#56607A"), 1.35f),
            CreateCoreEntry(AttackCoreType.Toxin, "RuneCore_Toxin", ParseHtml("#79E15E"), 1.35f),
        };

        List<CharBulletVisualLibrary.ResultVisualEntry> resultEntries = new()
        {
            CreateResultEntry(AttackResultType.DirectDamage, "RuneResult_DirectDamage", 0.85f, 0.55f, 15f, 0.02f),
            CreateResultEntry(AttackResultType.Explosion, "RuneResult_Explosion", 1.02f, 0.65f, 28f, 0.05f),
            CreateResultEntry(AttackResultType.StatusEffect, "RuneResult_StatusEffect", 0.92f, 0.5f, 12f, 0.035f),
            CreateResultEntry(AttackResultType.SpawnChild, "RuneResult_SpawnChild", 0.9f, 0.58f, 18f, 0.025f),
            CreateResultEntry(AttackResultType.Split, "RuneResult_Split", 0.95f, 0.6f, -18f, 0.03f),
        };

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CharBulletVisualLibrary).GetField("coreVisuals", flags)?.SetValue(library, coreEntries);
        typeof(CharBulletVisualLibrary).GetField("resultVisuals", flags)?.SetValue(library, resultEntries);
        EditorUtility.SetDirty(library);
    }

    private static CharBulletVisualLibrary.CoreVisualEntry CreateCoreEntry(AttackCoreType coreType, string spriteName, Color tint, float scale)
    {
        return new CharBulletVisualLibrary.CoreVisualEntry
        {
            coreType = coreType,
            baseSprite = LoadSpriteAsset($"{SpriteFolder}/{spriteName}.png"),
            fallbackTint = tint,
            baseScale = scale,
            trailGradient = CreateGradient(tint, Color.Lerp(tint, Color.white, 0.45f)),
        };
    }

    private static CharBulletVisualLibrary.ResultVisualEntry CreateResultEntry(AttackResultType resultType, string spriteName, float scale, float alpha, float rotationSpeed, float pulseAmplitude)
    {
        return new CharBulletVisualLibrary.ResultVisualEntry
        {
            resultType = resultType,
            overlaySprite = LoadSpriteAsset($"{SpriteFolder}/{spriteName}.png"),
            overlayScale = scale,
            overlayAlpha = alpha,
            rotationSpeed = rotationSpeed,
            pulseAmplitude = pulseAmplitude,
        };
    }

    private static void ConfigureCharBulletPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CharBulletPrefabPath);
        try
        {
            Transform root = prefabRoot.transform;
            Transform textContainer = root.Find("Text");
            Transform glyph = root.Find("Text/Glyph");
            Transform collider = root.Find("Collider");
            if (textContainer == null || glyph == null || collider == null)
            {
                throw new InvalidDataException("CharBullet prefab is missing Text/Glyph/Collider and cannot be configured.");
            }

            GameObject shadowObject = EnsureShadowGlyph(textContainer, glyph.gameObject, root.gameObject.layer);
            GameObject coreBaseObject = EnsureSpriteChild(root, "RuneBaseCore", root.gameObject.layer);
            GameObject resultBaseObject = EnsureSpriteChild(root, "RuneBaseResult", root.gameObject.layer);
            GameObject trailObject = EnsureTrailChild(root, root.gameObject.layer);

            ConfigureTextContainer(textContainer.GetComponent<RectTransform>());
            ConfigureGlyph(glyph.GetComponent<RectTransform>(), glyph.GetComponent<TextMeshPro>(), glyph.GetComponent<MeshRenderer>(), sortingOrder: 20, localY: 0f);
            ConfigureGlyph(shadowObject.GetComponent<RectTransform>(), shadowObject.GetComponent<TextMeshPro>(), shadowObject.GetComponent<MeshRenderer>(), sortingOrder: 10, localY: 0f);
            ConfigureRuneBase(coreBaseObject.transform, coreBaseObject.GetComponent<SpriteRenderer>(), sortingOrder: -10, localY: CoreBaseHeightOffset);
            ConfigureRuneBase(resultBaseObject.transform, resultBaseObject.GetComponent<SpriteRenderer>(), sortingOrder: -5, localY: ResultBaseHeightOffset);
            ConfigureTrail(trailObject.GetComponent<TrailRenderer>());

            CharBullet bullet = prefabRoot.GetComponent<CharBullet>();
            CharBulletVisualPresenter presenter = prefabRoot.GetComponent<CharBulletVisualPresenter>();
            if (presenter == null)
            {
                presenter = prefabRoot.AddComponent<CharBulletVisualPresenter>();
            }

            AssignSerializedReference(bullet, "glyphText", glyph.GetComponent<TextMeshPro>());
            AssignSerializedReference(bullet, "sizeTarget", textContainer);
            AssignSerializedReference(bullet, "movementTarget", root);
            AssignSerializedReference(bullet, "impactCollider", collider.GetComponent<SphereCollider>());
            AssignSerializedReference(bullet, "movementRigidbody", prefabRoot.GetComponent<Rigidbody>());
            AssignSerializedReference(bullet, "visualPresenter", presenter);

            AssignSerializedReference(presenter, "mainGlyph", glyph.GetComponent<TextMeshPro>());
            AssignSerializedReference(presenter, "shadowGlyph", shadowObject.GetComponent<TextMeshPro>());
            AssignSerializedReference(presenter, "coreBaseRenderer", coreBaseObject.GetComponent<SpriteRenderer>());
            AssignSerializedReference(presenter, "resultBaseRenderer", resultBaseObject.GetComponent<SpriteRenderer>());
            AssignSerializedReference(presenter, "trailRenderer", trailObject.GetComponent<TrailRenderer>());
            AssignSerializedReference(presenter, "visualLibrary", AssetDatabase.LoadAssetAtPath<CharBulletVisualLibrary>(LibraryPath));

            EditorUtility.SetDirty(prefabRoot);
            EditorUtility.SetDirty(bullet);
            EditorUtility.SetDirty(presenter);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CharBulletPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject EnsureShadowGlyph(Transform textContainer, GameObject sourceGlyph, int layer)
    {
        Transform existing = textContainer.Find("GlyphShadow");
        GameObject shadowObject = existing != null ? existing.gameObject : Object.Instantiate(sourceGlyph, textContainer);
        shadowObject.name = "GlyphShadow";
        shadowObject.layer = layer;
        return shadowObject;
    }

    private static GameObject EnsureSpriteChild(Transform root, string childName, int layer)
    {
        Transform existing = root.Find(childName);
        GameObject childObject = existing != null ? existing.gameObject : new GameObject(childName);
        childObject.layer = layer;
        if (childObject.transform.parent != root)
        {
            childObject.transform.SetParent(root, false);
        }

        if (childObject.GetComponent<SpriteRenderer>() == null)
        {
            childObject.AddComponent<SpriteRenderer>();
        }

        return childObject;
    }

    private static GameObject EnsureTrailChild(Transform root, int layer)
    {
        Transform existing = root.Find("Trail");
        GameObject trailObject = existing != null ? existing.gameObject : new GameObject("Trail");
        trailObject.layer = layer;
        if (trailObject.transform.parent != root)
        {
            trailObject.transform.SetParent(root, false);
        }

        if (trailObject.GetComponent<TrailRenderer>() == null)
        {
            trailObject.AddComponent<TrailRenderer>();
        }

        return trailObject;
    }

    private static void ConfigureTextContainer(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.up * TextHeightOffset;
        rectTransform.anchoredPosition3D = new Vector3(0f, TextHeightOffset, 0f);
        rectTransform.sizeDelta = Vector2.one * 16f;
    }

    private static void ConfigureGlyph(RectTransform rectTransform, TextMeshPro textMeshPro, MeshRenderer meshRenderer, int sortingOrder, float localY)
    {
        if (rectTransform != null)
        {
            rectTransform.localPosition = new Vector3(0f, localY, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.one * 16f;
        }

        if (textMeshPro != null)
        {
            textMeshPro.enableAutoSizing = false;
        }

        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = sortingOrder;
        }
    }

    private static void ConfigureRuneBase(Transform target, SpriteRenderer spriteRenderer, int sortingOrder, float localY)
    {
        if (target != null)
        {
            target.localPosition = new Vector3(0f, localY, 0f);
            target.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.localScale = Vector3.one;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.25f);
        }
    }

    private static void ConfigureTrail(TrailRenderer trailRenderer)
    {
        if (trailRenderer == null)
        {
            return;
        }

        trailRenderer.time = 0.08f;
        trailRenderer.minVertexDistance = 0.01f;
        trailRenderer.widthMultiplier = 0.18f;
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.numCapVertices = 2;
        trailRenderer.sortingOrder = -20;
        trailRenderer.startColor = new Color(1f, 0.35f, 0.2f, 0.8f);
        trailRenderer.endColor = new Color(1f, 0.35f, 0.2f, 0f);
    }

    private static void AssignSerializedReference(Object target, string fieldName, Object value)
    {
        if (target == null)
        {
            return;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        FieldInfo field = target.GetType().GetField(fieldName, flags);
        field?.SetValue(target, value);
    }

    private static Gradient CreateGradient(Color start, Color middle)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.45f),
                new GradientColorKey(start, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.25f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static Color ParseHtml(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color value);
        return value;
    }

    private static Sprite LoadSpriteAsset(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
    }

    private static void SaveSprite(string fileName, System.Action<Color[]> drawer)
    {
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1f, 1f, 1f, 0f);
        }

        drawer(pixels);

        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();

        string path = Path.Combine(SpriteFolder, fileName + ".png").Replace('\\', '/');
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TextureSize;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void Plot(Color[] pixels, int x, int y, float alpha)
    {
        if (x < 0 || x >= TextureSize || y < 0 || y >= TextureSize)
        {
            return;
        }

        int index = y * TextureSize + x;
        float nextAlpha = Mathf.Clamp01(Mathf.Max(pixels[index].a, alpha));
        pixels[index] = new Color(1f, 1f, 1f, nextAlpha);
    }

    private static void DrawDisc(Color[] pixels, Vector2 center, float radius, float alpha)
    {
        float radiusSqr = radius * radius;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                if ((point - center).sqrMagnitude <= radiusSqr)
                {
                    Plot(pixels, x, y, alpha);
                }
            }
        }
    }

    private static void ClearDisc(Color[] pixels, Vector2 center, float radius)
    {
        float radiusSqr = radius * radius;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                if ((point - center).sqrMagnitude <= radiusSqr)
                {
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, 0f);
                }
            }
        }
    }

    private static void DrawRing(Color[] pixels, Vector2 center, float radius, float thickness, float alpha)
    {
        float outer = radius + thickness * 0.5f;
        float inner = Mathf.Max(0f, radius - thickness * 0.5f);
        float outerSqr = outer * outer;
        float innerSqr = inner * inner;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                float distance = (point - center).sqrMagnitude;
                if (distance <= outerSqr && distance >= innerSqr)
                {
                    Plot(pixels, x, y, alpha);
                }
            }
        }
    }

    private static void DrawLine(Color[] pixels, Vector2 a, Vector2 b, float thickness, float alpha)
    {
        float minX = Mathf.Min(a.x, b.x) - thickness;
        float minY = Mathf.Min(a.y, b.y) - thickness;
        float maxX = Mathf.Max(a.x, b.x) + thickness;
        float maxY = Mathf.Max(a.y, b.y) + thickness;

        for (int y = Mathf.Max(0, Mathf.FloorToInt(minY)); y < Mathf.Min(TextureSize, Mathf.CeilToInt(maxY)); y++)
        {
            for (int x = Mathf.Max(0, Mathf.FloorToInt(minX)); x < Mathf.Min(TextureSize, Mathf.CeilToInt(maxX)); x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                if (DistancePointToSegment(point, a, b) <= thickness * 0.5f)
                {
                    Plot(pixels, x, y, alpha);
                }
            }
        }
    }

    private static void DrawPolygon(Color[] pixels, Vector2 center, float radius, int sides, float rotationDegrees, float thickness, float alpha)
    {
        Vector2[] points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = (rotationDegrees + (360f / sides) * i) * Mathf.Deg2Rad;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        for (int i = 0; i < sides; i++)
        {
            DrawLine(pixels, points[i], points[(i + 1) % sides], thickness, alpha);
        }
    }

    private static void DrawRadials(Color[] pixels, Vector2 center, float innerRadius, float outerRadius, int count, float rotationDegrees, float thickness, float alpha)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (rotationDegrees + (360f / count) * i) * Mathf.Deg2Rad;
            Vector2 from = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius;
            Vector2 to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
            DrawLine(pixels, from, to, thickness, alpha);
        }
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(point, projection);
    }
}
