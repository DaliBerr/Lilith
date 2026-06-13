using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.UI;

public sealed class BossInfoUIScreenTests
{
    private const string BossInfoPrefabPath = "Assets/Prefabs/UI/MainHUD/Boss Info UI.prefab";
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] is GameObject gameObject && gameObject.TryGetComponent(out BossInfoUIScreen screen))
            {
                InvokeMethod(screen, "OnDisable");
                InvokeMethod(screen, "OnDestroy");
            }

            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void BossLifecycleEvents_ShowRefreshAndHideOverlay()
    {
        GameObject cellPrefab = CreateHealthCellPrefab();
        BossInfoUIScreen screen = CreateBossInfoScreen(cellPrefab, out RectTransform barRoot, out TMP_Text bossNameText);
        BaseCharEnemyNorm1 boss = CreateBossEnemy(100f, 100f);

        EventManager.eventBus.Publish(new BossEncounterStartedEvent(boss, "Test Boss", boss.CurrentHealth, boss.MaxHealth));

        Assert.That(screen.getAlpha(), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(bossNameText.text, Is.EqualTo("Test Boss"));
        Assert.That(barRoot.childCount, Is.EqualTo(5));
        AssertCellProgress(barRoot.GetChild(0).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(barRoot.GetChild(4).GetComponent<StrokeRevealUIWord>(), 1f);

        bool damageApplied = boss.TryApplyDamage(35f, out _, out _);

        Assert.That(damageApplied, Is.True);
        AssertCellProgress(barRoot.GetChild(0).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(barRoot.GetChild(1).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(barRoot.GetChild(2).GetComponent<StrokeRevealUIWord>(), 1f);
        AssertCellProgress(barRoot.GetChild(3).GetComponent<StrokeRevealUIWord>(), 0.25f);
        AssertCellProgress(barRoot.GetChild(4).GetComponent<StrokeRevealUIWord>(), 0f);

        EventManager.eventBus.Publish(new BossEncounterEndedEvent(boss, "Test Boss", endedByDeath: true));

        Assert.That(screen.getAlpha(), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(bossNameText.text, Is.Empty);
        AssertCellProgress(barRoot.GetChild(0).GetComponent<StrokeRevealUIWord>(), 0f);
        AssertCellProgress(barRoot.GetChild(4).GetComponent<StrokeRevealUIWord>(), 0f);
    }

    [Test]
    public void BossPhaseChangedEvent_UpdatesDisplayedBossName()
    {
        GameObject cellPrefab = CreateHealthCellPrefab();
        BossInfoUIScreen screen = CreateBossInfoScreen(cellPrefab, out _, out TMP_Text bossNameText);
        BaseCharEnemyNorm1 boss = CreateBossEnemy(100f, 100f);

        EventManager.eventBus.Publish(new BossEncounterStartedEvent(boss, "Test Boss", boss.CurrentHealth, boss.MaxHealth));
        EventManager.eventBus.Publish(new BossPhaseChangedEvent(boss, 2, "\u971c\u950b\u00b7\u8d30"));

        Assert.That(screen.getAlpha(), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(bossNameText.text, Is.EqualTo("\u971c\u950b\u00b7\u8d30"));
    }

    [Test]
    public void Prefab_UsesBoundedNativeOverlayLayoutWithoutResponsiveFitter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossInfoPrefabPath);
        Assert.That(prefab, Is.Not.Null, $"{BossInfoPrefabPath} should exist.");
        Assert.That(prefab.GetComponentInChildren<ResponsiveLayoutGroupFitter>(includeInactive: true), Is.Null);

        RectTransform hpBar = prefab.transform.Find("Hp Bar") as RectTransform;
        RectTransform bossInfo = prefab.transform.Find("Boss Info") as RectTransform;
        TMP_Text bossName = prefab.transform.Find("Boss Info/Text (TMP)")?.GetComponent<TMP_Text>();

        Assert.That(hpBar, Is.Not.Null);
        Assert.That(bossInfo, Is.Not.Null);
        Assert.That(bossName, Is.Not.Null);
        Assert.That(hpBar.anchorMin.x, Is.EqualTo(0.10f).Within(0.001f));
        Assert.That(hpBar.anchorMax.x, Is.EqualTo(0.90f).Within(0.001f));

        HorizontalLayoutGroup hpLayout = hpBar.GetComponent<HorizontalLayoutGroup>();
        Assert.That(hpLayout, Is.Not.Null);
        Assert.That(hpLayout.spacing, Is.EqualTo(12f).Within(0.01f));
        Assert.That(hpLayout.childForceExpandWidth, Is.False);

        Assert.That(bossName.enableAutoSizing, Is.True);
        Assert.That(bossName.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(bossName.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
    }

    private BossInfoUIScreen CreateBossInfoScreen(GameObject healthCellPrefab, out RectTransform barRoot, out TMP_Text bossNameText)
    {
        GameObject root = CreateUiObject("Boss Info UI");
        root.AddComponent<Image>();
        BossInfoUIScreen screen = root.AddComponent<BossInfoUIScreen>();
        SetPrivateField(screen, "transitionDuration", 0f);
        SetPrivateField(screen, "phaseTransitionPulseDuration", 0f);

        GameObject bar = CreateUiObject("Hp Bar", root.transform);
        bar.AddComponent<HorizontalLayoutGroup>();
        StrokeHealthBarController barController = bar.AddComponent<StrokeHealthBarController>();
        SetPrivateField(barController, "healthCellPrefab", healthCellPrefab);
        SetPrivateField(barController, "hpPerCell", 20f);
        SetPrivateField(barController, "changeAnimationDuration", 0f);

        GameObject bossInfo = CreateUiObject("Boss Info", root.transform);
        bossInfo.AddComponent<Image>();
        GameObject textObject = CreateUiObject("Text (TMP)", bossInfo.transform);
        bossNameText = textObject.AddComponent<TextMeshProUGUI>();

        barRoot = bar.GetComponent<RectTransform>();
        InvokeMethod(screen, "__Init", new object[] { null });
        return screen;
    }

    private BaseCharEnemyNorm1 CreateBossEnemy(float maxHealth, float currentHealth)
    {
        GameObject bossObject = CreateGameObject("Boss");
        BaseCharEnemyNorm1 boss = bossObject.AddComponent<BaseCharEnemyNorm1>();
        SetPrivateField(boss, "health", maxHealth);
        SetPrivateField(boss, "currentHealth", Mathf.Clamp(currentHealth, 0f, maxHealth));
        SetPrivateField(boss, "hasInitializedHealth", true);
        return boss;
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

    private GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private void AssertCellProgress(StrokeRevealUIWord cell, float expectedProgress)
    {
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.TryGetNormalizedProgress(out float progress), Is.True);
        Assert.That(progress, Is.EqualTo(expectedProgress).Within(0.0001f));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokeMethod(object target, string methodName, object[] args = null)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static FieldInfo FindInstanceField(System.Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static MethodInfo FindInstanceMethod(System.Type type, string methodName)
    {
        while (type != null)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }
}
