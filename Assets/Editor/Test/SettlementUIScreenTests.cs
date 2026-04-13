using System.Collections.Generic;
using System.Reflection;
using Kernel.GameState;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettlementUIScreenTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        StatusController.ClearStatus();

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
    public void SettlementUIScreen_AutoBindsPrefabStyleFields()
    {
        SettlementUIScreen screen = CreateSettlementScreen(includeCloseButton: false, out RectTransform panelRoot, out TMP_Text titleText, out TMP_Text resultText, out TMP_Text harvestText, out TMP_Text summaryText, out _);

        InvokeMethod(screen, "__Init", new object[] { null });

        Assert.That(screen.PanelRoot, Is.SameAs(panelRoot));
        Assert.That(screen.TitleText, Is.SameAs(titleText));
        Assert.That(screen.ResultText, Is.SameAs(resultText));
        Assert.That(screen.HarvestText, Is.SameAs(harvestText));
        Assert.That(screen.SummaryText, Is.SameAs(summaryText));
    }

    [Test]
    public void SettlementUIScreen_FormatsVictorySnapshotUsingVictoryTitlePool()
    {
        SettlementUIScreen screen = CreateSettlementScreen(includeCloseButton: false, out _, out TMP_Text titleText, out TMP_Text resultText, out TMP_Text harvestText, out TMP_Text summaryText, out _);
        InvokeMethod(screen, "__Init", new object[] { null });

        SettlementPresentationCatalogData catalog = new()
        {
            VictoryTitles = new List<string> { "VictoryTitle" },
            DefeatTitles = new List<string> { "DefeatTitle" },
            VictoryResultTemplate = "你击败了{waves}波敌人，{bosses}个boss。",
            DefeatResultTemplate = "失败模板",
            HarvestHeader = "你获得了：",
            HarvestEmptyText = "空收益",
            SummaryHeader = "你击败了：",
            SummaryEmptyText = "空总结",
        };
        SettlementSnapshot snapshot = new(
            SettlementOutcome.Victory,
            3,
            1,
            new[]
            {
                new SettlementCountEntry("残卷", 2),
            },
            new[]
            {
                new SettlementCountEntry("野狗", 1),
            });

        InvokeMethod(screen, "ApplyPresentation", snapshot, catalog);

        Assert.That(titleText.text, Is.EqualTo("VictoryTitle"));
        Assert.That(resultText.text, Is.EqualTo("你击败了3波敌人，1个boss。"));
        Assert.That(harvestText.text, Is.EqualTo("你获得了：\n\t残卷 * 2"));
        Assert.That(summaryText.text, Is.EqualTo("你击败了：\n\t野狗 * 1"));
    }

    [Test]
    public void SettlementUIScreen_FormatsDefeatSnapshotUsingDefeatTitlePool()
    {
        SettlementUIScreen screen = CreateSettlementScreen(includeCloseButton: false, out _, out TMP_Text titleText, out TMP_Text resultText, out _, out _, out _);
        InvokeMethod(screen, "__Init", new object[] { null });

        SettlementPresentationCatalogData catalog = new()
        {
            VictoryTitles = new List<string> { "VictoryTitle" },
            DefeatTitles = new List<string> { "DefeatTitle" },
            VictoryResultTemplate = "胜利模板",
            DefeatResultTemplate = "你击败了{waves}波敌人，{bosses}个boss。",
            HarvestHeader = "你获得了：",
            HarvestEmptyText = "空收益",
            SummaryHeader = "你击败了：",
            SummaryEmptyText = "空总结",
        };
        SettlementSnapshot snapshot = new(
            SettlementOutcome.Defeat,
            1,
            0,
            System.Array.Empty<SettlementCountEntry>(),
            System.Array.Empty<SettlementCountEntry>());

        InvokeMethod(screen, "ApplyPresentation", snapshot, catalog);

        Assert.That(titleText.text, Is.EqualTo("DefeatTitle"));
        Assert.That(resultText.text, Is.EqualTo("你击败了1波敌人，0个boss。"));
    }

    [Test]
    public void SettlementUIScreen_UsesEmptyStateTextWhenEntriesAreMissing()
    {
        SettlementUIScreen screen = CreateSettlementScreen(includeCloseButton: false, out _, out _, out _, out TMP_Text harvestText, out TMP_Text summaryText, out _);
        InvokeMethod(screen, "__Init", new object[] { null });

        SettlementPresentationCatalogData catalog = new()
        {
            VictoryTitles = new List<string> { "VictoryTitle" },
            DefeatTitles = new List<string> { "DefeatTitle" },
            VictoryResultTemplate = "胜利模板",
            DefeatResultTemplate = "失败模板",
            HarvestHeader = "你获得了：",
            HarvestEmptyText = "本轮没有获得长期收益",
            SummaryHeader = "你击败了：",
            SummaryEmptyText = "本轮尚未击败任何敌人",
        };

        InvokeMethod(screen, "ApplyPresentation", SettlementSnapshot.Empty, catalog);

        Assert.That(harvestText.text, Is.EqualTo("你获得了：\n\t本轮没有获得长期收益"));
        Assert.That(summaryText.text, Is.EqualTo("你击败了：\n\t本轮尚未击败任何敌人"));
    }

    [Test]
    public void SettlementUIScreen_InitDoesNotRequireCloseButton()
    {
        SettlementUIScreen screen = CreateSettlementScreen(includeCloseButton: false, out _, out _, out _, out _, out _, out _);

        Assert.DoesNotThrow(() => InvokeMethod(screen, "__Init", new object[] { null }));
        Assert.That(screen.CloseButton, Is.Null);
    }

    private SettlementUIScreen CreateSettlementScreen(
        bool includeCloseButton,
        out RectTransform panelRoot,
        out TMP_Text titleText,
        out TMP_Text resultText,
        out TMP_Text harvestText,
        out TMP_Text summaryText,
        out Button closeButton)
    {
        GameObject root = CreateUiObject("Settlement UI Screen");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        SettlementUIScreen screen = root.AddComponent<SettlementUIScreen>();

        GameObject panel = CreateUiObject("Settlement Panel", root.transform);
        panel.AddComponent<Image>();
        panelRoot = panel.GetComponent<RectTransform>();

        GameObject topPanel = CreateUiObject("Top Panel", panel.transform);
        topPanel.AddComponent<Image>();
        titleText = CreateTextObject("Tittle", topPanel.transform);

        GameObject mainContentPanel = CreateUiObject("Main Content Panel", panel.transform);
        mainContentPanel.AddComponent<Image>();

        GameObject resultContent = CreateUiObject("Result Content", mainContentPanel.transform);
        resultContent.AddComponent<Image>();
        resultText = CreateTextObject("Result", resultContent.transform);

        GameObject permanentThing = CreateUiObject("Permanent Thing", mainContentPanel.transform);
        permanentThing.AddComponent<Image>();
        harvestText = CreateTextObject("Harvest", permanentThing.transform);
        summaryText = CreateTextObject("Summarize", permanentThing.transform);

        closeButton = null;
        if (includeCloseButton)
        {
            GameObject closeObject = CreateUiObject("Close Button", topPanel.transform);
            closeObject.AddComponent<Image>();
            closeButton = closeObject.AddComponent<Button>();
        }

        return screen;
    }

    private TMP_Text CreateTextObject(string name, Transform parent)
    {
        GameObject textObject = CreateUiObject(name, parent);
        return textObject.AddComponent<TextMeshProUGUI>();
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

    private static void InvokeMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        method.Invoke(target, args);
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
