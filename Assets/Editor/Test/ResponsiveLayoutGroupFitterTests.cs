using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class ResponsiveLayoutGroupFitterTests
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
    public void FitNow_ShrinksGridLayoutToFitOwnRect()
    {
        RectTransform root = CreateRect("Root", null, new Vector2(800f, 600f));
        RectTransform grid = CreateRect("Grid", root, new Vector2(480f, 360f));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(100f, 100f);
        layout.spacing = new Vector2(20f, 20f);
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;

        for (int i = 0; i < 48; i++)
        {
            CreateRect($"Cell {i + 1:D2}", grid, new Vector2(100f, 100f));
        }

        ResponsiveLayoutGroupFitter fitter = root.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
        fitter.SetRoot(root);
        fitter.FitNow();

        Assert.That(layout.cellSize.x, Is.LessThan(100f));
        Assert.That(ResolveGridRequiredWidth(layout, 8), Is.LessThanOrEqualTo(grid.rect.width + 0.01f));
        Assert.That(ResolveGridRequiredHeight(layout, 8, 48), Is.LessThanOrEqualTo(grid.rect.height + 0.01f));
    }

    [Test]
    public void FitNow_ShrinksHorizontalLayoutElementsToFitOwnRect()
    {
        RectTransform root = CreateRect("Root", null, new Vector2(800f, 600f));
        RectTransform panel = CreateRect("Panel", root, new Vector2(200f, 80f));
        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        for (int i = 0; i < 3; i++)
        {
            LayoutElement element = CreateRect($"Item {i + 1:D2}", panel, new Vector2(100f, 50f)).gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 100f;
            element.preferredHeight = 50f;
        }

        ResponsiveLayoutGroupFitter fitter = root.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
        fitter.SetRoot(root);
        fitter.FitNow();

        LayoutElement firstElement = panel.GetChild(0).GetComponent<LayoutElement>();
        Assert.That(firstElement.preferredWidth, Is.LessThan(100f));
        Assert.That(ResolveHorizontalRequiredWidth(layout), Is.LessThanOrEqualTo(panel.rect.width + 0.01f));
    }

    [Test]
    public void FitNow_ShrinksVerticalLayoutElementsToFitOwnRect()
    {
        RectTransform root = CreateRect("Root", null, new Vector2(800f, 600f));
        RectTransform panel = CreateRect("Panel", root, new Vector2(140f, 180f));
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        for (int i = 0; i < 3; i++)
        {
            LayoutElement element = CreateRect($"Item {i + 1:D2}", panel, new Vector2(120f, 80f)).gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 120f;
            element.preferredHeight = 80f;
        }

        ResponsiveLayoutGroupFitter fitter = root.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
        fitter.SetRoot(root);
        fitter.FitNow();

        LayoutElement firstElement = panel.GetChild(0).GetComponent<LayoutElement>();
        Assert.That(firstElement.preferredHeight, Is.LessThan(80f));
        Assert.That(ResolveVerticalRequiredHeight(layout), Is.LessThanOrEqualTo(panel.rect.height + 0.01f));
    }

    [Test]
    public void FitNow_SkipsContentSizeFitterDrivenLayouts()
    {
        RectTransform root = CreateRect("Root", null, new Vector2(800f, 600f));
        RectTransform panel = CreateRect("Panel", root, new Vector2(200f, 80f));
        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        panel.gameObject.AddComponent<ContentSizeFitter>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        for (int i = 0; i < 3; i++)
        {
            LayoutElement element = CreateRect($"Item {i + 1:D2}", panel, new Vector2(100f, 50f)).gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 100f;
            element.preferredHeight = 50f;
        }

        ResponsiveLayoutGroupFitter fitter = root.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
        fitter.SetRoot(root);
        fitter.FitNow();

        LayoutElement firstElement = panel.GetChild(0).GetComponent<LayoutElement>();
        Assert.That(layout.spacing, Is.EqualTo(20f));
        Assert.That(firstElement.preferredWidth, Is.EqualTo(100f));
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        RectTransform rectTransform = gameObject.transform as RectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        if (parent != null)
        {
            rectTransform.SetParent(parent, false);
        }

        return rectTransform;
    }

    private static float ResolveGridRequiredWidth(GridLayoutGroup layout, int columns)
    {
        return layout.padding.horizontal
            + (layout.cellSize.x * columns)
            + (layout.spacing.x * Mathf.Max(0, columns - 1));
    }

    private static float ResolveGridRequiredHeight(GridLayoutGroup layout, int columns, int childCount)
    {
        int rowCount = Mathf.CeilToInt(childCount / (float)columns);
        return layout.padding.vertical
            + (layout.cellSize.y * rowCount)
            + (layout.spacing.y * Mathf.Max(0, rowCount - 1));
    }

    private static float ResolveHorizontalRequiredWidth(HorizontalLayoutGroup layout)
    {
        float requiredWidth = layout.padding.horizontal;
        for (int i = 0; i < layout.transform.childCount; i++)
        {
            RectTransform child = layout.transform.GetChild(i) as RectTransform;
            requiredWidth += LayoutUtility.GetPreferredSize(child, 0);
        }

        requiredWidth += layout.spacing * Mathf.Max(0, layout.transform.childCount - 1);
        return requiredWidth;
    }

    private static float ResolveVerticalRequiredHeight(VerticalLayoutGroup layout)
    {
        float requiredHeight = layout.padding.vertical;
        for (int i = 0; i < layout.transform.childCount; i++)
        {
            RectTransform child = layout.transform.GetChild(i) as RectTransform;
            requiredHeight += LayoutUtility.GetPreferredSize(child, 1);
        }

        requiredHeight += layout.spacing * Mathf.Max(0, layout.transform.childCount - 1);
        return requiredHeight;
    }
}
