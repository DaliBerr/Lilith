using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class CoverImageTests
{
    private const string StartUpPrefabPath = "Assets/Prefabs/UI/StartUp/StartUp UI Prefab.prefab";
    private const string StartUpBackgroundGuid = "20700ce27c4fd65478ad10c1d2ef52e8";

    [Test]
    public void CalculateCoverUv_TenBySixteenScreen_CropsHorizontalEdges()
    {
        Vector4 uv = CoverImage.CalculateCoverUv(new Vector4(0f, 0f, 1f, 1f), 16f / 9f, 16f / 10f);

        Assert.That(uv.x, Is.GreaterThan(0f));
        Assert.That(uv.z, Is.LessThan(1f));
        Assert.That(uv.y, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(uv.w, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(uv.z - uv.x, Is.EqualTo(0.9f).Within(0.0001f));
    }

    [Test]
    public void CalculateCoverUv_UltrawideScreen_CropsVerticalEdges()
    {
        Vector4 uv = CoverImage.CalculateCoverUv(new Vector4(0f, 0f, 1f, 1f), 16f / 9f, 21f / 9f);

        Assert.That(uv.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(uv.z, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(uv.y, Is.GreaterThan(0f));
        Assert.That(uv.w, Is.LessThan(1f));
    }

    [Test]
    public void StartUpBackground_UsesCoverImage()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartUpPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        CoverImage coverImage = prefab.GetComponent<CoverImage>();
        Assert.That(coverImage, Is.Not.Null);
        Assert.That(coverImage.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(coverImage.sprite)), Is.EqualTo(StartUpBackgroundGuid));
        Assert.That(coverImage.type, Is.EqualTo(Image.Type.Simple));
    }
}
