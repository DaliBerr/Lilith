using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class HintUIScreenTests
{
    private const string HintPrefabPath = "Assets/Prefabs/UI/Hint/Hint UI.prefab";
    private const string HintEntryPrefabPath = "Assets/Prefabs/UI/Hint/Hint Entry.prefab";
    private const string HintCatalogEntryPrefabPath = "Assets/Prefabs/UI/Hint/Hint Catalog Entry.prefab";

    [Test]
    public void Prefabs_UseLocalNativeLayoutWithoutResponsiveFitter()
    {
        GameObject hintPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintPrefabPath);
        GameObject entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintEntryPrefabPath);
        GameObject catalogEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintCatalogEntryPrefabPath);

        Assert.That(hintPrefab, Is.Not.Null, $"{HintPrefabPath} should exist.");
        Assert.That(entryPrefab, Is.Not.Null, $"{HintEntryPrefabPath} should exist.");
        Assert.That(catalogEntryPrefab, Is.Not.Null, $"{HintCatalogEntryPrefabPath} should exist.");
        Assert.That(hintPrefab.GetComponentInChildren<ResponsiveLayoutGroupFitter>(includeInactive: true), Is.Null);
        Assert.That(entryPrefab.GetComponentInChildren<ResponsiveLayoutGroupFitter>(includeInactive: true), Is.Null);
        Assert.That(catalogEntryPrefab.GetComponentInChildren<ResponsiveLayoutGroupFitter>(includeInactive: true), Is.Null);

        Assert.That(hintPrefab.GetComponent<HintUIScreen>(), Is.Not.Null);

        RectTransform mainPanel = hintPrefab.transform.Find("Main Panel") as RectTransform;
        RectTransform mainContent = hintPrefab.transform.Find("Main Panel/Main Content") as RectTransform;
        RectTransform leftPanel = hintPrefab.transform.Find("Main Panel/Left Panel") as RectTransform;
        RectTransform catalog = hintPrefab.transform.Find("Main Panel/Catalog") as RectTransform;
        TMP_Text contentText = hintPrefab.transform.Find("Main Panel/Main Content/Scroll View/Viewport/Content/Text (TMP)")?.GetComponent<TMP_Text>();

        Assert.That(mainPanel, Is.Not.Null);
        Assert.That(mainContent, Is.Not.Null);
        Assert.That(leftPanel, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(contentText, Is.Not.Null);
        Assert.That(mainPanel.anchorMax.x, Is.GreaterThan(0.9f));
        Assert.That(leftPanel.anchorMax.x, Is.LessThan(mainContent.anchorMin.x + 0.01f));
        Assert.That(catalog.GetComponent<HorizontalLayoutGroup>().spacing, Is.EqualTo(8f).Within(0.01f));
        Assert.That(contentText.enableAutoSizing, Is.True);
        Assert.That(contentText.fontSizeMax, Is.LessThanOrEqualTo(34f));

        AssertEntryText(entryPrefab.GetComponentInChildren<TMP_Text>(includeInactive: true));
        AssertEntryText(catalogEntryPrefab.GetComponentInChildren<TMP_Text>(includeInactive: true));
    }

    private static void AssertEntryText(TMP_Text text)
    {
        Assert.That(text, Is.Not.Null);
        Assert.That(text.enableAutoSizing, Is.True);
        Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
    }
}
