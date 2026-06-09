using System.Collections;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class UIGuideOverlayControllerTests
{
    private const string GuidePopupPath = "Assets/Prefabs/UI/Guide/Guide Popup.prefab";

    [SetUp]
    public void SetUp()
    {
        UIGuideOverlayController.ResetAutomaticGuideSessionForTests();
    }

    [TearDown]
    public void TearDown()
    {
        UIGuideOverlayController.ResetAutomaticGuideSessionForTests();
    }

    [Test]
    public void GuidePopupPrefabHasLocalizedAttentionHintWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuidePopupPath);
        Assert.That(prefab, Is.Not.Null);

        UIGuidePopupAttentionController attention = prefab.GetComponent<UIGuidePopupAttentionController>();
        Assert.That(attention, Is.Not.Null);
        Assert.That(attention.ClickNotePanelForTests, Is.Not.Null);
        Assert.That(attention.ClickNoteTextForTests, Is.Not.Null);
        Assert.That(attention.ClickNotePanelForTests.name, Is.EqualTo("Click Note Panel"));
        Assert.That(attention.ClickNoteTextForTests.name, Is.EqualTo("Text (TMP)"));
    }

    [Test]
    public void ResolvePopupAnchoredPositionPrefersLeftWhenRightWouldOverflow()
    {
        Rect overlay = Rect.MinMaxRect(-400f, -300f, 400f, 300f);
        Rect target = Rect.MinMaxRect(280f, -40f, 380f, 40f);
        Vector2 popupSize = new(220f, 90f);

        Vector2 position = UIGuideOverlayController.ResolvePopupAnchoredPosition(target, overlay, popupSize, 18f, 16f);

        Assert.That(position.x, Is.LessThan(target.xMin));
        Assert.That(position.x - popupSize.x * 0.5f, Is.GreaterThanOrEqualTo(overlay.xMin + 16f));
        Assert.That(position.x + popupSize.x * 0.5f, Is.LessThanOrEqualTo(overlay.xMax - 16f));
    }

    [Test]
    public void CalculateTargetRectAddsPaddingAndClampsToOverlay()
    {
        CanvasFixture fixture = new();
        try
        {
            RectTransform target = CreateRect("Target", fixture.CanvasRoot);
            target.sizeDelta = new Vector2(120f, 80f);
            target.anchoredPosition = new Vector2(20f, 30f);
            Canvas.ForceUpdateCanvases();

            Rect rect = UIGuideOverlayController.CalculateTargetRect(target, fixture.CanvasRoot, 10f);

            Assert.That(rect.width, Is.EqualTo(140f).Within(0.01f));
            Assert.That(rect.height, Is.EqualTo(100f).Within(0.01f));
            Assert.That(rect.center.x, Is.EqualTo(20f).Within(0.01f));
            Assert.That(rect.center.y, Is.EqualTo(30f).Within(0.01f));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void AutomaticGuideStartsOnlyOncePerSession()
    {
        CanvasFixture fixture = new();
        try
        {
            UIGuideOverlayController guide = CreateConfiguredGuide(fixture, out _);

            Assert.That(guide.TryStartAutomaticGuide(), Is.True);
            Assert.That(guide.IsRunning, Is.True);

            guide.StopGuide();

            Assert.That(guide.TryStartAutomaticGuide(), Is.False);
            Assert.That(guide.IsRunning, Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [UnityTest]
    public IEnumerator OverlayBlocksInputShowsHighlightAndClickAdvancesToEnd()
    {
        CanvasFixture fixture = new();
        try
        {
            UIGuideOverlayController guide = CreateConfiguredGuide(fixture, out _);

            guide.StartGuide();
            yield return null;

            RectTransform overlay = guide.OverlayRootForTests;
            Assert.That(overlay, Is.Not.Null);
            Assert.That(guide.IsRunning, Is.True);
            Assert.That(overlay.parent, Is.SameAs(fixture.CanvasRoot));

            Image blocker = overlay.GetComponent<Image>();
            Button blockerButton = overlay.GetComponent<Button>();
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blockerButton, Is.Not.Null);
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(guide.HighlightRectForTests, Is.Not.Null);
            Assert.That(guide.PopupRectForTests, Is.Not.Null);
            Assert.That(guide.DimRectsForTests.Count, Is.EqualTo(4));

            blockerButton.onClick.Invoke();
            for (int i = 0; i < 5 && guide.IsRunning; i++)
            {
                yield return null;
            }

            Assert.That(guide.IsRunning, Is.False);
            Assert.That(guide.OverlayRootForTests == null, Is.True);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [UnityTest]
    public IEnumerator PopupIsClampedInsideCanvas()
    {
        CanvasFixture fixture = new();
        try
        {
            RectTransform target = CreateRect("Edge Target", fixture.CanvasRoot);
            target.sizeDelta = new Vector2(80f, 80f);
            target.anchoredPosition = new Vector2(350f, 240f);
            UIGuideOverlayController guide = CreateConfiguredGuide(fixture, target);

            guide.StartGuide();
            yield return null;

            RectTransform popup = guide.PopupRectForTests;
            Assert.That(popup, Is.Not.Null);

            Rect bounds = fixture.CanvasRoot.rect;
            Vector2 half = popup.rect.size * 0.5f;
            Vector2 position = popup.anchoredPosition;
            Assert.That(position.x - half.x, Is.GreaterThanOrEqualTo(bounds.xMin + 16f - 0.01f));
            Assert.That(position.x + half.x, Is.LessThanOrEqualTo(bounds.xMax - 16f + 0.01f));
            Assert.That(position.y - half.y, Is.GreaterThanOrEqualTo(bounds.yMin + 16f - 0.01f));
            Assert.That(position.y + half.y, Is.LessThanOrEqualTo(bounds.yMax - 16f + 0.01f));

            guide.StopGuide();
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static UIGuideOverlayController CreateConfiguredGuide(CanvasFixture fixture, out RectTransform target)
    {
        target = CreateRect("Target", fixture.CanvasRoot);
        target.sizeDelta = new Vector2(160f, 100f);
        target.anchoredPosition = Vector2.zero;
        return CreateConfiguredGuide(fixture, target);
    }

    private static UIGuideOverlayController CreateConfiguredGuide(CanvasFixture fixture, RectTransform target)
    {
        GameObject owner = new("Guide Owner", typeof(RectTransform), typeof(UIGuideOverlayController));
        owner.transform.SetParent(fixture.CanvasRoot, false);
        UIGuideOverlayController guide = owner.GetComponent<UIGuideOverlayController>();
        guide.ConfigureForTests(
            CreatePopupPrefab(fixture.CanvasRoot),
            new[] { new UIGuideStep(target, "test.title", "test.body", 8f) });
        return guide;
    }

    private static GameObject CreatePopupPrefab(Transform parent)
    {
        GameObject popup = new("Guide Popup", typeof(RectTransform), typeof(Image));
        popup.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)popup.transform;
        rect.sizeDelta = new Vector2(320f, 90f);

        GameObject textObject = new("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(popup.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "Guide";
        text.fontSize = 20f;
        return popup;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        return rect;
    }

    private sealed class CanvasFixture
    {
        private readonly GameObject rootObject;

        public CanvasFixture()
        {
            rootObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            CanvasRoot = (RectTransform)rootObject.transform;
            CanvasRoot.sizeDelta = new Vector2(800f, 600f);
            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        public RectTransform CanvasRoot { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(rootObject);
        }
    }
}
