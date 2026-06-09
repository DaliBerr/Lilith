using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.Localization;
using Vocalith.UI;

namespace Kernel.UI
{
    [Serializable]
    public sealed class UIGuideStep
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private string titleKey;
        [SerializeField] private string titleFallback;
        [SerializeField] private string bodyKey;
        [SerializeField] private string bodyFallback;
        [SerializeField, Min(0f)] private float padding = 8f;

        public UIGuideStep()
        {
        }

        public UIGuideStep(RectTransform target, string titleKey, string bodyKey, float padding = 8f)
            : this(target, titleKey, string.Empty, bodyKey, string.Empty, padding)
        {
        }

        public UIGuideStep(
            RectTransform target,
            string titleKey,
            string titleFallback,
            string bodyKey,
            string bodyFallback,
            float padding = 8f)
        {
            this.target = target;
            this.titleKey = titleKey;
            this.titleFallback = titleFallback;
            this.bodyKey = bodyKey;
            this.bodyFallback = bodyFallback;
            this.padding = Mathf.Max(0f, padding);
        }

        public RectTransform Target => target;
        public string TitleKey => titleKey ?? string.Empty;
        public string TitleFallback => titleFallback ?? string.Empty;
        public string BodyKey => bodyKey ?? string.Empty;
        public string BodyFallback => bodyFallback ?? string.Empty;
        public float Padding => Mathf.Max(0f, padding);
    }

    [DisallowMultipleComponent]
    public sealed class UIGuideOverlayController : MonoBehaviour
    {
        private const string DefaultTitleFallback = "Guide";
        private const float DefaultPopupGap = 18f;
        private const float DefaultScreenMargin = 16f;

        [SerializeField] private GameObject guidePopupPrefab;
        [SerializeField] private List<UIGuideStep> steps = new();
        [SerializeField] private Color dimColor = new(0f, 0f, 0f, 0.68f);
        [SerializeField] private Color highlightColor = new(1f, 0.94f, 0.32f, 0.22f);
        [SerializeField] private Color highlightOutlineColor = new(1f, 0.88f, 0.18f, 0.92f);
        [SerializeField, Min(1f)] private float highlightOutlineDistance = 3f;
        [SerializeField, Min(1f)] private float popupMaxWidth = 440f;
        [SerializeField, Min(1f)] private float popupMaxHeight = 180f;
        [SerializeField, Min(0f)] private float popupGap = DefaultPopupGap;
        [SerializeField, Min(0f)] private float screenMargin = DefaultScreenMargin;

        private static bool hasPlayedAutomaticGuide;

        private Coroutine guideCoroutine;
        private RectTransform overlayRoot;
        private RectTransform[] dimRects;
        private RectTransform highlightRect;
        private RectTransform popupRect;
        private TMP_Text popupText;
        private Button clickBlockerButton;
        private Button popupButton;
        private int currentStepIndex = -1;
        private List<UIGuideStep> runtimeStepsOverride;

        public bool IsRunning => guideCoroutine != null;
        public IReadOnlyList<UIGuideStep> Steps => steps;
        public GameObject GuidePopupPrefab => guidePopupPrefab;

#if UNITY_INCLUDE_TESTS
        public RectTransform OverlayRootForTests => overlayRoot;
        public RectTransform HighlightRectForTests => highlightRect;
        public RectTransform PopupRectForTests => popupRect;
        public IReadOnlyList<RectTransform> DimRectsForTests => dimRects;

        public void ConfigureForTests(GameObject popupPrefab, IEnumerable<UIGuideStep> guideSteps)
        {
            guidePopupPrefab = popupPrefab;
            steps.Clear();
            if (guideSteps == null)
            {
                return;
            }

            steps.AddRange(guideSteps);
        }
#endif

        public static void ResetAutomaticGuideSessionForTests()
        {
            hasPlayedAutomaticGuide = false;
        }

        public bool TryStartAutomaticGuide()
        {
            if (hasPlayedAutomaticGuide || IsRunning || !isActiveAndEnabled || !HasRunnableSteps())
            {
                return false;
            }

            hasPlayedAutomaticGuide = true;
            StartGuide();
            return true;
        }

        public Coroutine StartGuide()
        {
            if (guideCoroutine != null)
            {
                return guideCoroutine;
            }

            if (!HasRunnableSteps())
            {
                return null;
            }

            guideCoroutine = StartCoroutine(RunGuide());
            return guideCoroutine;
        }

        public Coroutine StartSingleStepGuide(RectTransform target, string titleKey, string bodyKey, float padding = 8f)
        {
            return StartSingleStepGuide(target, titleKey, string.Empty, bodyKey, string.Empty, padding);
        }

        public Coroutine StartSingleStepGuide(
            RectTransform target,
            string titleKey,
            string titleFallback,
            string bodyKey,
            string bodyFallback,
            float padding = 8f)
        {
            if (target == null || guidePopupPrefab == null || !isActiveAndEnabled)
            {
                return null;
            }

            StopGuide();
            runtimeStepsOverride = new List<UIGuideStep>
            {
                new(target, titleKey, titleFallback, bodyKey, bodyFallback, padding),
            };
            guideCoroutine = StartCoroutine(RunGuide());
            return guideCoroutine;
        }

        public void StopGuide()
        {
            if (guideCoroutine != null)
            {
                StopCoroutine(guideCoroutine);
                guideCoroutine = null;
            }

            DestroyOverlay();
            runtimeStepsOverride = null;
        }

        public static Rect CalculateTargetRect(RectTransform target, RectTransform overlayRoot, float padding)
        {
            if (target == null || overlayRoot == null)
            {
                return Rect.zero;
            }

            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = overlayRoot.InverseTransformPoint(corners[i]);
                minX = Mathf.Min(minX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxX = Mathf.Max(maxX, local.x);
                maxY = Mathf.Max(maxY, local.y);
            }

            Rect bounds = overlayRoot.rect;
            float safePadding = Mathf.Max(0f, padding);
            minX = Mathf.Clamp(minX - safePadding, bounds.xMin, bounds.xMax);
            minY = Mathf.Clamp(minY - safePadding, bounds.yMin, bounds.yMax);
            maxX = Mathf.Clamp(maxX + safePadding, bounds.xMin, bounds.xMax);
            maxY = Mathf.Clamp(maxY + safePadding, bounds.yMin, bounds.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public static Vector2 ResolvePopupAnchoredPosition(
            Rect targetRect,
            Rect overlayRect,
            Vector2 popupSize,
            float gap = DefaultPopupGap,
            float margin = DefaultScreenMargin)
        {
            float safeGap = Mathf.Max(0f, gap);
            float safeMargin = Mathf.Max(0f, margin);
            Vector2 half = popupSize * 0.5f;
            Vector2[] candidates =
            {
                new(targetRect.xMax + safeGap + half.x, targetRect.center.y),
                new(targetRect.xMin - safeGap - half.x, targetRect.center.y),
                new(targetRect.center.x, targetRect.yMax + safeGap + half.y),
                new(targetRect.center.x, targetRect.yMin - safeGap - half.y),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (FitsInside(candidates[i], half, overlayRect, safeMargin))
                {
                    return candidates[i];
                }
            }

            Vector2 first = candidates[0];
            return new Vector2(
                Mathf.Clamp(first.x, overlayRect.xMin + safeMargin + half.x, overlayRect.xMax - safeMargin - half.x),
                Mathf.Clamp(first.y, overlayRect.yMin + safeMargin + half.y, overlayRect.yMax - safeMargin - half.y));
        }

        private void OnDisable()
        {
            StopGuide();
        }

        private bool HasRunnableSteps()
        {
            IReadOnlyList<UIGuideStep> activeSteps = ResolveActiveSteps();
            if (guidePopupPrefab == null || activeSteps == null || activeSteps.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < activeSteps.Count; i++)
            {
                if (activeSteps[i]?.Target != null)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<UIGuideStep> ResolveActiveSteps()
        {
            return runtimeStepsOverride ?? (IReadOnlyList<UIGuideStep>)steps;
        }

        private IEnumerator RunGuide()
        {
            if (!CreateOverlay())
            {
                guideCoroutine = null;
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            try
            {
                currentStepIndex = -1;
                if (!ShowNextRunnableStep())
                {
                    yield break;
                }

                while (overlayRoot != null)
                {
                    yield return null;
                }
            }
            finally
            {
                DestroyOverlay();
                currentStepIndex = -1;
                guideCoroutine = null;
            }
        }

        private bool CreateOverlay()
        {
            RectTransform parent = ResolveOverlayParent();
            if (parent == null)
            {
                return false;
            }

            GameObject overlayObject = new("UI Guide Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            overlayObject.transform.SetParent(parent, false);
            overlayRoot = (RectTransform)overlayObject.transform;
            StretchToParent(overlayRoot);
            overlayRoot.SetAsLastSibling();

            Image blockerImage = overlayObject.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;
            clickBlockerButton = overlayObject.GetComponent<Button>();
            clickBlockerButton.targetGraphic = blockerImage;
            clickBlockerButton.transition = Selectable.Transition.None;
            clickBlockerButton.onClick.AddListener(RequestAdvance);

            dimRects = new[]
            {
                CreateDimRect("Dim Top"),
                CreateDimRect("Dim Bottom"),
                CreateDimRect("Dim Left"),
                CreateDimRect("Dim Right"),
            };
            highlightRect = CreateHighlightRect();
            CreatePopup();
            return popupRect != null && popupText != null;
        }

        private RectTransform ResolveOverlayParent()
        {
            if (UIManager.Instance != null && UIManager.Instance.layerOverlay != null)
            {
                return UIManager.Instance.layerOverlay;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
        }

        private RectTransform CreateDimRect(string name)
        {
            GameObject dim = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(overlayRoot, false);
            RectTransform rect = (RectTransform)dim.transform;
            Image image = dim.GetComponent<Image>();
            image.color = dimColor;
            image.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateHighlightRect()
        {
            GameObject highlight = new("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            highlight.transform.SetParent(overlayRoot, false);
            RectTransform rect = (RectTransform)highlight.transform;
            Image image = highlight.GetComponent<Image>();
            image.color = highlightColor;
            image.raycastTarget = false;

            Outline outline = highlight.GetComponent<Outline>();
            outline.effectColor = highlightOutlineColor;
            outline.effectDistance = new Vector2(highlightOutlineDistance, -highlightOutlineDistance);
            return rect;
        }

        private void CreatePopup()
        {
            GameObject popup = Instantiate(guidePopupPrefab, overlayRoot);
            popup.name = guidePopupPrefab.name;
            popupRect = popup.transform as RectTransform;
            if (popupRect == null)
            {
                DestroyRuntimeObject(popup);
                return;
            }

            popupText = popup.GetComponentInChildren<TMP_Text>(true);
            ContentSizeFitter fitter = popup.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (popupText != null)
            {
                popupText.raycastTarget = false;
                popupText.fontSize = Mathf.Min(popupText.fontSize, 24f);
                popupText.alignment = TextAlignmentOptions.TopLeft;
                popupText.textWrappingMode = TextWrappingModes.Normal;
                popupText.overflowMode = TextOverflowModes.Ellipsis;
            }

            Graphic rootGraphic = popup.GetComponent<Graphic>();
            if (rootGraphic != null)
            {
                rootGraphic.raycastTarget = true;
            }

            popupButton = popup.GetComponent<Button>() ?? popup.AddComponent<Button>();
            popupButton.targetGraphic = rootGraphic;
            popupButton.transition = Selectable.Transition.None;
            popupButton.onClick.AddListener(RequestAdvance);
        }

        private void ShowStep(UIGuideStep step)
        {
            Canvas.ForceUpdateCanvases();
            Rect targetRect = CalculateTargetRect(step.Target, overlayRoot, step.Padding);
            Rect overlayRect = overlayRoot.rect;
            targetRect = ClampRectToBounds(targetRect, overlayRect);

            ApplyDimLayout(targetRect, overlayRect);
            SetRect(highlightRect, targetRect);
            ConfigurePopupText(step);
            PositionPopup(targetRect, overlayRect);
            overlayRoot.SetAsLastSibling();
        }

        private void ApplyDimLayout(Rect targetRect, Rect overlayRect)
        {
            SetRect(dimRects[0], Rect.MinMaxRect(overlayRect.xMin, targetRect.yMax, overlayRect.xMax, overlayRect.yMax));
            SetRect(dimRects[1], Rect.MinMaxRect(overlayRect.xMin, overlayRect.yMin, overlayRect.xMax, targetRect.yMin));
            SetRect(dimRects[2], Rect.MinMaxRect(overlayRect.xMin, targetRect.yMin, targetRect.xMin, targetRect.yMax));
            SetRect(dimRects[3], Rect.MinMaxRect(targetRect.xMax, targetRect.yMin, overlayRect.xMax, targetRect.yMax));
        }

        private void ConfigurePopupText(UIGuideStep step)
        {
            if (popupText == null || popupRect == null)
            {
                return;
            }

            string title = Translate(
                step.TitleKey,
                string.IsNullOrWhiteSpace(step.TitleFallback) ? DefaultTitleFallback : step.TitleFallback);
            string body = Translate(step.BodyKey, step.BodyFallback);
            popupText.text = string.IsNullOrWhiteSpace(body)
                ? $"<b>{title}</b>"
                : $"<b>{title}</b>\n{body}";

            float width = Mathf.Max(160f, popupMaxWidth);
            float textWidth = Mathf.Max(120f, width - 30f);
            Vector2 preferred = popupText.GetPreferredValues(popupText.text, textWidth, 0f);
            float height = Mathf.Clamp(preferred.y + 22f, 75f, Mathf.Max(75f, popupMaxHeight));
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            LayoutElement textLayout = popupText.GetComponent<LayoutElement>() ?? popupText.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredWidth = textWidth;
            LayoutRebuilder.ForceRebuildLayoutImmediate(popupRect);
        }

        private void PositionPopup(Rect targetRect, Rect overlayRect)
        {
            Vector2 size = popupRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = new Vector2(popupMaxWidth, Mathf.Max(75f, Mathf.Min(popupMaxHeight, 120f)));
            }

            popupRect.anchoredPosition = ResolvePopupAnchoredPosition(targetRect, overlayRect, size, popupGap, screenMargin);
        }

        private void RequestAdvance()
        {
            if (!IsRunning || overlayRoot == null)
            {
                return;
            }

            if (!ShowNextRunnableStep())
            {
                StopGuide();
            }
        }

        private bool ShowNextRunnableStep()
        {
            IReadOnlyList<UIGuideStep> activeSteps = ResolveActiveSteps();
            if (activeSteps == null)
            {
                return false;
            }

            for (int i = currentStepIndex + 1; i < activeSteps.Count; i++)
            {
                UIGuideStep step = activeSteps[i];
                if (step?.Target == null || !step.Target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                currentStepIndex = i;
                ShowStep(step);
                return true;
            }

            return false;
        }

        private void DestroyOverlay()
        {
            if (clickBlockerButton != null)
            {
                clickBlockerButton.onClick.RemoveListener(RequestAdvance);
                clickBlockerButton = null;
            }

            if (popupButton != null)
            {
                popupButton.onClick.RemoveListener(RequestAdvance);
                popupButton = null;
            }

            if (overlayRoot != null)
            {
                DestroyRuntimeObject(overlayRoot.gameObject);
                overlayRoot = null;
            }

            dimRects = null;
            highlightRect = null;
            popupRect = null;
            popupText = null;
            currentStepIndex = -1;
            runtimeStepsOverride = null;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetRect(RectTransform rect, Rect value)
        {
            if (rect == null)
            {
                return;
            }

            float width = Mathf.Max(0f, value.width);
            float height = Mathf.Max(0f, value.height);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = value.center;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            rect.gameObject.SetActive(width > 0.01f && height > 0.01f);
        }

        private static bool FitsInside(Vector2 center, Vector2 half, Rect bounds, float margin)
        {
            return center.x - half.x >= bounds.xMin + margin
                && center.x + half.x <= bounds.xMax - margin
                && center.y - half.y >= bounds.yMin + margin
                && center.y + half.y <= bounds.yMax - margin;
        }

        private static Rect ClampRectToBounds(Rect rect, Rect bounds)
        {
            return Rect.MinMaxRect(
                Mathf.Clamp(rect.xMin, bounds.xMin, bounds.xMax),
                Mathf.Clamp(rect.yMin, bounds.yMin, bounds.yMax),
                Mathf.Clamp(rect.xMax, bounds.xMin, bounds.xMax),
                Mathf.Clamp(rect.yMax, bounds.yMin, bounds.yMax));
        }

        private static string Translate(string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(key)
                ? fallback
                : LocalizationManager.TranslateOrDefault(key, fallback);
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
