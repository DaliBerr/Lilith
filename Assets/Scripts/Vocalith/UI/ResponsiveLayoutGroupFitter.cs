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
        private bool isFitting;

        public void SetRoot(RectTransform value)
        {
            root = value;
        }

        public void FitNow()
        {
            RectTransform resolvedRoot = ResolveRoot();
            if (isFitting || resolvedRoot == null)
            {
                return;
            }

            isFitting = true;
            try
            {
                resolvedRoot.GetComponentsInChildren(false, layoutGroups);
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

        private RectTransform ResolveRoot()
        {
            return root != null ? root : transform as RectTransform;
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
            }
        }

        private void FitGridLayout(GridLayoutGroup gridLayoutGroup)
        {
            GridLayoutBase baseLayout = CaptureGridBase(gridLayoutGroup);
            RestoreGridLayout(gridLayoutGroup, baseLayout);

            RectTransform rectTransform = gridLayoutGroup.transform as RectTransform;
            GridFitSpace fitSpace = ResolveGridFitSpace(gridLayoutGroup, rectTransform);
            Vector2 availableSize = fitSpace.AvailableSize;
            int childCount = CountLayoutChildren(rectTransform);
            if (childCount <= 0 ||
                (fitSpace.ConstrainWidth && availableSize.x <= 0f) ||
                (fitSpace.ConstrainHeight && availableSize.y <= 0f))
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
            float scale = ResolveFitScale(
                availableSize.x,
                availableSize.y,
                requiredWidth,
                requiredHeight,
                fitSpace.ConstrainWidth,
                fitSpace.ConstrainHeight);

            ApplyGridScale(gridLayoutGroup, baseLayout, scale);
        }

        private static GridFitSpace ResolveGridFitSpace(GridLayoutGroup gridLayoutGroup, RectTransform rectTransform)
        {
            GridFitSpace fitSpace = new()
            {
                AvailableSize = rectTransform.rect.size,
                ConstrainWidth = true,
                ConstrainHeight = true,
            };

            ScrollRect scrollRect = gridLayoutGroup.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content != rectTransform)
            {
                return fitSpace;
            }

            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.transform as RectTransform;
            if (viewport == null || viewport.rect.width <= 0f || viewport.rect.height <= 0f)
            {
                return fitSpace;
            }

            fitSpace.AvailableSize = viewport.rect.size;
            if (scrollRect.vertical && !scrollRect.horizontal)
            {
                fitSpace.ConstrainHeight = false;
            }
            else if (scrollRect.horizontal && !scrollRect.vertical)
            {
                fitSpace.ConstrainWidth = false;
            }

            return fitSpace;
        }

        private static bool ShouldSkip(LayoutGroup layoutGroup)
        {
            if (layoutGroup == null || !layoutGroup.isActiveAndEnabled)
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

        private static void RestoreGridLayout(GridLayoutGroup gridLayoutGroup, GridLayoutBase baseLayout)
        {
            gridLayoutGroup.cellSize = baseLayout.CellSize;
            gridLayoutGroup.spacing = baseLayout.Spacing;
            ApplyPadding(gridLayoutGroup.padding, baseLayout.Padding, 1f);
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

        private static float ResolveFitScale(
            float availableWidth,
            float availableHeight,
            float requiredWidth,
            float requiredHeight,
            bool constrainWidth = true,
            bool constrainHeight = true)
        {
            if (requiredWidth <= 0f || requiredHeight <= 0f)
            {
                return 1f;
            }

            float scale = float.PositiveInfinity;
            if (constrainWidth)
            {
                scale = Mathf.Min(scale, availableWidth / requiredWidth);
            }

            if (constrainHeight)
            {
                scale = Mathf.Min(scale, availableHeight / requiredHeight);
            }

            return float.IsPositiveInfinity(scale) ? 1f : Mathf.Max(0.01f, scale);
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

        private struct GridFitSpace
        {
            public Vector2 AvailableSize;
            public bool ConstrainWidth;
            public bool ConstrainHeight;
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

    }
}
