using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class FixedPixelSlicedImageTests
{
    private const string DialogBoxGuid = "4813fa42d21ed84468a9fd35b6659d4f";

    [Test]
    public void CalculateLocalBorder_KeepsScreenPixelThicknessStableAcrossCanvasScale()
    {
        Rect rect = new(0f, 0f, 200f, 80f);
        Vector4 spriteBorder = new(4f, 5f, 5f, 5f);

        Vector4 scaleOneBorder = FixedPixelSlicedImage.CalculateLocalBorder(
            spriteBorder,
            1f,
            rect,
            1f,
            Vector3.one,
            Vector3.one);
        Vector4 scaleOnePointFiveBorder = FixedPixelSlicedImage.CalculateLocalBorder(
            spriteBorder,
            1f,
            rect,
            1.5f,
            Vector3.one,
            Vector3.one);

        Assert.That(scaleOneBorder.z * 1f, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(scaleOnePointFiveBorder.z * 1.5f, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(scaleOnePointFiveBorder.z, Is.LessThan(scaleOneBorder.z));
    }

    [Test]
    public void CalculateLocalBorder_ClampsBordersInsideSmallRects()
    {
        Rect rect = new(0f, 0f, 60f, 30f);
        Vector4 spriteBorder = new(50f, 40f, 50f, 40f);

        Vector4 clampedBorder = FixedPixelSlicedImage.CalculateLocalBorder(
            spriteBorder,
            1f,
            rect,
            1f,
            Vector3.one,
            Vector3.one);

        Assert.That(clampedBorder.x + clampedBorder.z, Is.EqualTo(rect.width).Within(0.0001f));
        Assert.That(clampedBorder.y + clampedBorder.w, Is.EqualTo(rect.height).Within(0.0001f));
        Assert.That(clampedBorder.x, Is.GreaterThanOrEqualTo(0f));
        Assert.That(clampedBorder.y, Is.GreaterThanOrEqualTo(0f));
        Assert.That(clampedBorder.z, Is.GreaterThanOrEqualTo(0f));
        Assert.That(clampedBorder.w, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void UIPrefabs_UseFixedPixelImageForDialogBoxSlicedSprites()
    {
        List<string> nativeDialogBoxImages = new();
        List<string> missingTargetGraphics = new();
        string dialogBoxPath = AssetDatabase.GUIDToAssetPath(DialogBoxGuid);

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image.sprite == null || image.type != Image.Type.Sliced)
                {
                    continue;
                }

                string spritePath = AssetDatabase.GetAssetPath(image.sprite);
                if (!string.Equals(spritePath, dialogBoxPath, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (image.GetType() == typeof(Image))
                {
                    nativeDialogBoxImages.Add($"{path}/{GetHierarchyPath(image.transform, prefab.transform)}");
                }
            }

            foreach (Selectable selectable in prefab.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable != null
                    && selectable.targetGraphic == null
                    && selectable.GetComponent<FixedPixelSlicedImage>() != null)
                {
                    missingTargetGraphics.Add($"{path}/{GetHierarchyPath(selectable.transform, prefab.transform)}");
                }
            }
        }

        Assert.That(nativeDialogBoxImages, Is.Empty, "Native sliced Image components still use dialog-box.png.");
        Assert.That(missingTargetGraphics, Is.Empty, "Selectables with FixedPixelSlicedImage must keep a targetGraphic.");
    }

    private static string GetHierarchyPath(Transform transform, Transform root)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        Stack<string> names = new();
        Transform current = transform;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        names.Push(root.name);
        return string.Join("/", names);
    }
}
