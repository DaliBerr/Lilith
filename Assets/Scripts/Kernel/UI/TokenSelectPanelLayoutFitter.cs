using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class TokenSelectPanelLayoutFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private HorizontalLayoutGroup layoutGroup;
        [SerializeField] private Vector2 referenceCardSize = new(460f, 500f);
        [SerializeField] private float extraVerticalPadding;
        [SerializeField] private float minimumHeight = 120f;

        private RectTransform cachedRectTransform;
        private bool hasCapturedLayoutBase;
        private float baseSpacing;
        private int basePaddingLeft;
        private int basePaddingRight;
        private int basePaddingTop;
        private int basePaddingBottom;
        private bool isFitting;

        public void FitNow()
        {
            RectTransform target = ResolveContentRoot();
            if (isFitting || target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            HorizontalLayoutGroup horizontalLayoutGroup = ResolveLayoutGroup();
            if (horizontalLayoutGroup == null)
            {
                return;
            }

            int childCount = CountLayoutChildren(target);
            if (childCount <= 0 || target.rect.width <= 0f)
            {
                return;
            }

            CaptureLayoutBase(horizontalLayoutGroup);
            float maxHeight = ResolveAnchoredHeight(target);
            if (maxHeight <= 0f)
            {
                maxHeight = target.rect.height;
            }

            float baseRequiredWidth = basePaddingLeft + basePaddingRight
                + (referenceCardSize.x * childCount)
                + (baseSpacing * Mathf.Max(0, childCount - 1));
            float widthScale = baseRequiredWidth > 0f ? target.rect.width / baseRequiredWidth : 1f;
            float availableCardHeight = maxHeight - basePaddingTop - basePaddingBottom - Mathf.Max(0f, extraVerticalPadding);
            float referenceCardHeight = ResolveReferenceCardHeight(target);
            float heightScale = referenceCardHeight > 0f ? availableCardHeight / referenceCardHeight : 1f;
            float scale = Mathf.Max(0.01f, Mathf.Min(widthScale, heightScale));
            float cardWidth = Mathf.Max(1f, referenceCardSize.x * scale);
            float cardHeight = ResolveScaledCardHeight(target, cardWidth, scale);
            float requiredHeight = basePaddingTop + basePaddingBottom + cardHeight + Mathf.Max(0f, extraVerticalPadding);
            float targetHeight = Mathf.Clamp(requiredHeight, Mathf.Max(1f, minimumHeight), Mathf.Max(1f, maxHeight));

            isFitting = true;
            try
            {
                ApplyLayoutScale(horizontalLayoutGroup, scale);
                ApplyCardWidth(target, cardWidth);
                if (Mathf.Abs(target.rect.height - targetHeight) > 0.5f)
                {
                    target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
                }

                LayoutRebuilder.MarkLayoutForRebuild(target);
            }
            finally
            {
                isFitting = false;
            }
        }

        private void OnEnable()
        {
            FitNow();
        }

        private void LateUpdate()
        {
            FitNow();
        }

        private void OnRectTransformDimensionsChange()
        {
            FitNow();
        }

        private void OnTransformChildrenChanged()
        {
            FitNow();
        }

        private RectTransform ResolveContentRoot()
        {
            if (contentRoot != null)
            {
                return contentRoot;
            }

            cachedRectTransform ??= transform as RectTransform;
            return cachedRectTransform;
        }

        private HorizontalLayoutGroup ResolveLayoutGroup()
        {
            if (layoutGroup != null)
            {
                return layoutGroup;
            }

            RectTransform target = ResolveContentRoot();
            layoutGroup = target != null ? target.GetComponent<HorizontalLayoutGroup>() : null;
            return layoutGroup;
        }

        private void CaptureLayoutBase(HorizontalLayoutGroup horizontalLayoutGroup)
        {
            if (hasCapturedLayoutBase)
            {
                return;
            }

            baseSpacing = Mathf.Max(0f, horizontalLayoutGroup.spacing);
            basePaddingLeft = Mathf.Max(0, horizontalLayoutGroup.padding.left);
            basePaddingRight = Mathf.Max(0, horizontalLayoutGroup.padding.right);
            basePaddingTop = Mathf.Max(0, horizontalLayoutGroup.padding.top);
            basePaddingBottom = Mathf.Max(0, horizontalLayoutGroup.padding.bottom);
            hasCapturedLayoutBase = true;
        }

        private float ResolveReferenceCardHeight(RectTransform parent)
        {
            AspectRatioFitter aspectRatioFitter = ResolveFirstAspectRatioFitter(parent);
            if (aspectRatioFitter != null &&
                aspectRatioFitter.aspectMode == AspectRatioFitter.AspectMode.WidthControlsHeight &&
                aspectRatioFitter.aspectRatio > 0f)
            {
                return referenceCardSize.x / aspectRatioFitter.aspectRatio;
            }

            return Mathf.Max(1f, referenceCardSize.y);
        }

        private float ResolveScaledCardHeight(RectTransform parent, float cardWidth, float scale)
        {
            AspectRatioFitter aspectRatioFitter = ResolveFirstAspectRatioFitter(parent);
            if (aspectRatioFitter != null &&
                aspectRatioFitter.aspectMode == AspectRatioFitter.AspectMode.WidthControlsHeight &&
                aspectRatioFitter.aspectRatio > 0f)
            {
                return cardWidth / aspectRatioFitter.aspectRatio;
            }

            return Mathf.Max(1f, referenceCardSize.y * scale);
        }

        private static AspectRatioFitter ResolveFirstAspectRatioFitter(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (!IsLayoutChild(child))
                {
                    continue;
                }

                AspectRatioFitter aspectRatioFitter = child.GetComponent<AspectRatioFitter>();
                if (aspectRatioFitter != null)
                {
                    return aspectRatioFitter;
                }
            }

            return null;
        }

        private static int CountLayoutChildren(RectTransform parent)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (IsLayoutChild(parent.GetChild(i) as RectTransform))
                {
                    count++;
                }
            }

            return count;
        }

        private void ApplyLayoutScale(HorizontalLayoutGroup horizontalLayoutGroup, float scale)
        {
            horizontalLayoutGroup.spacing = Mathf.Max(0f, baseSpacing * scale);
            horizontalLayoutGroup.padding.left = Mathf.Max(0, Mathf.FloorToInt(basePaddingLeft * scale));
            horizontalLayoutGroup.padding.right = Mathf.Max(0, Mathf.FloorToInt(basePaddingRight * scale));
            horizontalLayoutGroup.padding.top = basePaddingTop;
            horizontalLayoutGroup.padding.bottom = basePaddingBottom;
        }

        private static void ApplyCardWidth(RectTransform parent, float cardWidth)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (!IsLayoutChild(child))
                {
                    continue;
                }

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = cardWidth;
                }
            }
        }

        private static float ResolveAnchoredHeight(RectTransform target)
        {
            RectTransform parent = target.parent as RectTransform;
            if (parent == null)
            {
                return target.rect.height;
            }

            return parent.rect.height * Mathf.Abs(target.anchorMax.y - target.anchorMin.y);
        }

        private static bool IsLayoutChild(RectTransform child)
        {
            if (child == null || !child.gameObject.activeSelf)
            {
                return false;
            }

            LayoutElement layoutElement = child.GetComponent<LayoutElement>();
            return layoutElement == null || !layoutElement.ignoreLayout;
        }
    }
}
