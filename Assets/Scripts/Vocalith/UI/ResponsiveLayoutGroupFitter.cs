using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vocalith.UI
{
    [DisallowMultipleComponent]
    public sealed class ResponsiveLayoutGroupFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform root;

        private readonly List<LayoutGroup> layoutGroups = new();
        private readonly Dictionary<GridLayoutGroup, GridLayoutBase> gridBases = new();
        private readonly Dictionary<HorizontalOrVerticalLayoutGroup, LinearLayoutBase> linearBases = new();
        private readonly Dictionary<LayoutElement, LayoutElementBase> layoutElementBases = new();
        private bool isFitting;

        public void SetRoot(RectTransform value)
        {
            root = value;
        }

        public void FitNow()
        {
            if (isFitting || root == null)
            {
                return;
            }

            isFitting = true;
            try
            {
                root.GetComponentsInChildren(false, layoutGroups);
                layoutGroups.Sort((left, right) => GetTransformDepth(right.transform).CompareTo(GetTransformDepth(left.transform)));
                for (int i = 0; i < layoutGroups.Count; i++)
                {
                    FitLayoutGroup(layoutGroups[i]);
                }
            }
            finally
            {
                layoutGroups.Clear();
                isFitting = false;
            }
        }

        private void LateUpdate()
        {
            FitNow();
        }

        private void FitLayoutGroup(LayoutGroup layoutGroup)
        {
            if (ShouldSkip(layoutGroup))
            {
                return;
            }

            if (layoutGroup is GridLayoutGroup gridLayoutGroup)
            {
                FitGridLayout(gridLayoutGroup);
                return;
            }

            if (layoutGroup is HorizontalOrVerticalLayoutGroup linearLayoutGroup)
            {
                FitLinearLayout(linearLayoutGroup);
            }
        }

        private void FitGridLayout(GridLayoutGroup gridLayoutGroup)
        {
            GridLayoutBase baseLayout = CaptureGridBase(gridLayoutGroup);
            RestoreGridLayout(gridLayoutGroup, baseLayout);

            RectTransform rectTransform = gridLayoutGroup.transform as RectTransform;
            Vector2 availableSize = rectTransform.rect.size;
            int childCount = CountLayoutChildren(rectTransform);
            if (childCount <= 0 || availableSize.x <= 0f || availableSize.y <= 0f)
            {
                return;
            }

            ResolveGridShape(gridLayoutGroup, childCount, availableSize.x, baseLayout, out int columnCount, out int rowCount);
            float requiredWidth = baseLayout.Padding.Horizontal
                + (baseLayout.CellSize.x * columnCount)
                + (baseLayout.Spacing.x * Mathf.Max(0, columnCount - 1));
            float requiredHeight = baseLayout.Padding.Vertical
                + (baseLayout.CellSize.y * rowCount)
                + (baseLayout.Spacing.y * Mathf.Max(0, rowCount - 1));
            float scale = ResolveFitScale(availableSize.x, availableSize.y, requiredWidth, requiredHeight);

            ApplyGridScale(gridLayoutGroup, baseLayout, scale);
        }

        private void FitLinearLayout(HorizontalOrVerticalLayoutGroup layoutGroup)
        {
            LinearLayoutBase baseLayout = CaptureLinearBase(layoutGroup);
            RestoreLinearLayout(layoutGroup, baseLayout);

            RectTransform rectTransform = layoutGroup.transform as RectTransform;
            Vector2 availableSize = rectTransform.rect.size;
            if (availableSize.x <= 0f || availableSize.y <= 0f)
            {
                return;
            }

            bool isHorizontal = layoutGroup is HorizontalLayoutGroup;
            int childCount = 0;
            float requiredMain = isHorizontal ? baseLayout.Padding.Horizontal : baseLayout.Padding.Vertical;
            float requiredCross = isHorizontal ? baseLayout.Padding.Vertical : baseLayout.Padding.Horizontal;
            RestoreDirectChildLayoutElements(rectTransform);
            for (int i = 0; i < rectTransform.childCount; i++)
            {
                RectTransform child = rectTransform.GetChild(i) as RectTransform;
                if (!IsLayoutChild(child))
                {
                    continue;
                }

                childCount++;
                requiredMain += LayoutUtility.GetPreferredSize(child, isHorizontal ? 0 : 1);
                requiredCross = Mathf.Max(requiredCross,
                    (isHorizontal ? baseLayout.Padding.Vertical : baseLayout.Padding.Horizontal)
                    + LayoutUtility.GetPreferredSize(child, isHorizontal ? 1 : 0));
            }

            if (childCount <= 0)
            {
                return;
            }

            requiredMain += baseLayout.Spacing * Mathf.Max(0, childCount - 1);
            float availableMain = isHorizontal ? availableSize.x : availableSize.y;
            float availableCross = isHorizontal ? availableSize.y : availableSize.x;
            float scale = ResolveFitScale(availableMain, availableCross, requiredMain, requiredCross);

            ApplyLinearScale(layoutGroup, baseLayout, scale);
            ApplyDirectChildLayoutElementScale(rectTransform, scale);
        }

        private static bool ShouldSkip(LayoutGroup layoutGroup)
        {
            if (layoutGroup == null || !layoutGroup.isActiveAndEnabled)
            {
                return true;
            }

            if (layoutGroup is not GridLayoutGroup && layoutGroup.GetComponent<ContentSizeFitter>() != null)
            {
                return true;
            }

            RectTransform rectTransform = layoutGroup.transform as RectTransform;
            return rectTransform == null;
        }

        private GridLayoutBase CaptureGridBase(GridLayoutGroup gridLayoutGroup)
        {
            if (gridBases.TryGetValue(gridLayoutGroup, out GridLayoutBase baseLayout))
            {
                return baseLayout;
            }

            baseLayout = new GridLayoutBase
            {
                CellSize = new Vector2(Mathf.Max(1f, gridLayoutGroup.cellSize.x), Mathf.Max(1f, gridLayoutGroup.cellSize.y)),
                Spacing = new Vector2(Mathf.Max(0f, gridLayoutGroup.spacing.x), Mathf.Max(0f, gridLayoutGroup.spacing.y)),
                Padding = PaddingValues.From(gridLayoutGroup.padding),
            };
            gridBases[gridLayoutGroup] = baseLayout;
            return baseLayout;
        }

        private LinearLayoutBase CaptureLinearBase(HorizontalOrVerticalLayoutGroup layoutGroup)
        {
            if (linearBases.TryGetValue(layoutGroup, out LinearLayoutBase baseLayout))
            {
                return baseLayout;
            }

            baseLayout = new LinearLayoutBase
            {
                Spacing = Mathf.Max(0f, layoutGroup.spacing),
                Padding = PaddingValues.From(layoutGroup.padding),
            };
            linearBases[layoutGroup] = baseLayout;
            return baseLayout;
        }

        private LayoutElementBase CaptureLayoutElementBase(LayoutElement layoutElement)
        {
            if (layoutElementBases.TryGetValue(layoutElement, out LayoutElementBase baseLayoutElement))
            {
                return baseLayoutElement;
            }

            baseLayoutElement = LayoutElementBase.From(layoutElement);
            layoutElementBases[layoutElement] = baseLayoutElement;
            return baseLayoutElement;
        }

        private static void RestoreGridLayout(GridLayoutGroup gridLayoutGroup, GridLayoutBase baseLayout)
        {
            gridLayoutGroup.cellSize = baseLayout.CellSize;
            gridLayoutGroup.spacing = baseLayout.Spacing;
            ApplyPadding(gridLayoutGroup.padding, baseLayout.Padding, 1f);
        }

        private static void RestoreLinearLayout(HorizontalOrVerticalLayoutGroup layoutGroup, LinearLayoutBase baseLayout)
        {
            layoutGroup.spacing = baseLayout.Spacing;
            ApplyPadding(layoutGroup.padding, baseLayout.Padding, 1f);
        }

        private void ApplyDirectChildLayoutElementScale(RectTransform parent, float scale)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (!IsLayoutChild(child))
                {
                    continue;
                }

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    continue;
                }

                LayoutElementBase baseLayoutElement = CaptureLayoutElementBase(layoutElement);
                baseLayoutElement.ApplyTo(layoutElement, scale);
            }
        }

        private void RestoreDirectChildLayoutElements(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (!IsLayoutChild(child))
                {
                    continue;
                }

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    continue;
                }

                LayoutElementBase baseLayoutElement = CaptureLayoutElementBase(layoutElement);
                baseLayoutElement.ApplyTo(layoutElement, 1f);
            }
        }

        private static void ResolveGridShape(
            GridLayoutGroup gridLayoutGroup,
            int childCount,
            float availableWidth,
            GridLayoutBase baseLayout,
            out int columnCount,
            out int rowCount)
        {
            switch (gridLayoutGroup.constraint)
            {
                case GridLayoutGroup.Constraint.FixedColumnCount:
                    columnCount = Mathf.Max(1, gridLayoutGroup.constraintCount);
                    rowCount = Mathf.Max(1, Mathf.CeilToInt(childCount / (float)columnCount));
                    return;
                case GridLayoutGroup.Constraint.FixedRowCount:
                    rowCount = Mathf.Max(1, gridLayoutGroup.constraintCount);
                    columnCount = Mathf.Max(1, Mathf.CeilToInt(childCount / (float)rowCount));
                    return;
                default:
                    float availableContentWidth = Mathf.Max(1f, availableWidth - baseLayout.Padding.Horizontal);
                    float cellStride = Mathf.Max(1f, baseLayout.CellSize.x + baseLayout.Spacing.x);
                    columnCount = Mathf.Max(1, Mathf.FloorToInt((availableContentWidth + baseLayout.Spacing.x) / cellStride));
                    rowCount = Mathf.Max(1, Mathf.CeilToInt(childCount / (float)columnCount));
                    return;
            }
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

        private static bool IsLayoutChild(RectTransform child)
        {
            if (child == null || !child.gameObject.activeSelf)
            {
                return false;
            }

            LayoutElement layoutElement = child.GetComponent<LayoutElement>();
            return layoutElement == null || !layoutElement.ignoreLayout;
        }

        private static float ResolveFitScale(float availableWidth, float availableHeight, float requiredWidth, float requiredHeight)
        {
            if (requiredWidth <= 0f || requiredHeight <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(
                Mathf.Min(1f, availableWidth / requiredWidth, availableHeight / requiredHeight),
                0.01f,
                1f);
        }

        private static void ApplyGridScale(GridLayoutGroup gridLayoutGroup, GridLayoutBase baseLayout, float scale)
        {
            gridLayoutGroup.cellSize = new Vector2(
                Mathf.Max(1f, baseLayout.CellSize.x * scale),
                Mathf.Max(1f, baseLayout.CellSize.y * scale));
            gridLayoutGroup.spacing = new Vector2(
                Mathf.Max(0f, baseLayout.Spacing.x * scale),
                Mathf.Max(0f, baseLayout.Spacing.y * scale));
            ApplyPadding(gridLayoutGroup.padding, baseLayout.Padding, scale);
            LayoutRebuilder.MarkLayoutForRebuild(gridLayoutGroup.transform as RectTransform);
        }

        private static void ApplyLinearScale(HorizontalOrVerticalLayoutGroup layoutGroup, LinearLayoutBase baseLayout, float scale)
        {
            layoutGroup.spacing = Mathf.Max(0f, baseLayout.Spacing * scale);
            ApplyPadding(layoutGroup.padding, baseLayout.Padding, scale);
            LayoutRebuilder.MarkLayoutForRebuild(layoutGroup.transform as RectTransform);
        }

        private static void ApplyPadding(RectOffset target, PaddingValues source, float scale)
        {
            target.left = Mathf.Max(0, Mathf.FloorToInt(source.Left * scale));
            target.right = Mathf.Max(0, Mathf.FloorToInt(source.Right * scale));
            target.top = Mathf.Max(0, Mathf.FloorToInt(source.Top * scale));
            target.bottom = Mathf.Max(0, Mathf.FloorToInt(source.Bottom * scale));
        }

        private static int GetTransformDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private struct GridLayoutBase
        {
            public Vector2 CellSize;
            public Vector2 Spacing;
            public PaddingValues Padding;
        }

        private struct LinearLayoutBase
        {
            public float Spacing;
            public PaddingValues Padding;
        }

        private struct PaddingValues
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;

            public int Horizontal => Left + Right;
            public int Vertical => Top + Bottom;

            public static PaddingValues From(RectOffset padding)
            {
                return new PaddingValues
                {
                    Left = Mathf.Max(0, padding.left),
                    Right = Mathf.Max(0, padding.right),
                    Top = Mathf.Max(0, padding.top),
                    Bottom = Mathf.Max(0, padding.bottom),
                };
            }
        }

        private struct LayoutElementBase
        {
            public float MinWidth;
            public float MinHeight;
            public float PreferredWidth;
            public float PreferredHeight;

            public static LayoutElementBase From(LayoutElement layoutElement)
            {
                return new LayoutElementBase
                {
                    MinWidth = layoutElement.minWidth,
                    MinHeight = layoutElement.minHeight,
                    PreferredWidth = layoutElement.preferredWidth,
                    PreferredHeight = layoutElement.preferredHeight,
                };
            }

            public void ApplyTo(LayoutElement layoutElement, float scale)
            {
                layoutElement.minWidth = ScaleOptional(MinWidth, scale);
                layoutElement.minHeight = ScaleOptional(MinHeight, scale);
                layoutElement.preferredWidth = ScaleOptional(PreferredWidth, scale);
                layoutElement.preferredHeight = ScaleOptional(PreferredHeight, scale);
            }

            private static float ScaleOptional(float value, float scale)
            {
                return value >= 0f ? Mathf.Max(1f, value * scale) : value;
            }
        }
    }
}
