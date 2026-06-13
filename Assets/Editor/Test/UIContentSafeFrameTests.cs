using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Vocalith.UI;

public sealed class UIContentSafeFrameTests
{
    private static readonly string[] SafeFramePrefabPaths =
    {
        "Assets/Prefabs/UI/Backpack/BackPackUI.prefab",
        "Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab",
        "Assets/Prefabs/UI/MainHUD/MainUI.prefab",
        "Assets/Prefabs/UI/MainHUD/Boss Info UI.prefab",
        "Assets/Prefabs/UI/Options/Options.prefab",
        "Assets/Prefabs/UI/Options/Setting Panel.prefab",
        "Assets/Prefabs/UI/System/Pause/PauseUI.prefab",
        "Assets/Prefabs/UI/System/Info Popup.prefab",
        "Assets/Prefabs/UI/System/Loading Panel.prefab",
        "Assets/Prefabs/UI/System/Settlement UI Screen.prefab",
        "Assets/Prefabs/UI/Profile/Profile Popup.prefab",
        "Assets/Prefabs/UI/Hint/Hint UI.prefab",
        "Assets/Prefabs/UI/Narrative/Dialog UI.prefab",
        "Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab",
        "Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab",
        "Assets/Prefabs/UI/Narrative/Storyteller Panel.prefab",
        "Assets/Prefabs/UI/Upgrade/Upgrade UI Screen.prefab",
        "Assets/Prefabs/UI/StartUp/StartUp UI Prefab.prefab",
    };

    [Test]
    public void CalculateConstrainedSize_StandardSixteenByNine_ReturnsParentSize()
    {
        Vector2 size = UIContentSafeFrame.CalculateConstrainedSize(new Vector2(1920f, 1080f), UIContentSafeFrame.DefaultMaxAspect);

        Assert.That(size.x, Is.EqualTo(1920f).Within(0.0001f));
        Assert.That(size.y, Is.EqualTo(1080f).Within(0.0001f));
    }

    [Test]
    public void CalculateConstrainedSize_Ultrawide_ReturnsSixteenByNineWidth()
    {
        Vector2 size = UIContentSafeFrame.CalculateConstrainedSize(new Vector2(2560f, 1080f), UIContentSafeFrame.DefaultMaxAspect);

        Assert.That(size.x, Is.EqualTo(1920f).Within(0.0001f));
        Assert.That(size.y, Is.EqualTo(1080f).Within(0.0001f));
    }

    [TestCase(2880f, 1800f)]
    [TestCase(1280f, 1024f)]
    public void CalculateConstrainedSize_NotUltrawide_ReturnsParentSize(float width, float height)
    {
        Vector2 size = UIContentSafeFrame.CalculateConstrainedSize(new Vector2(width, height), UIContentSafeFrame.DefaultMaxAspect);

        Assert.That(size.x, Is.EqualTo(width).Within(0.0001f));
        Assert.That(size.y, Is.EqualTo(height).Within(0.0001f));
    }

    [TestCase(0f, 1080f, 16f / 9f)]
    [TestCase(1920f, 0f, 16f / 9f)]
    [TestCase(float.NaN, 1080f, 16f / 9f)]
    [TestCase(1920f, float.PositiveInfinity, 16f / 9f)]
    [TestCase(1920f, 1080f, 0f)]
    [TestCase(1920f, 1080f, float.NaN)]
    public void CalculateConstrainedSize_InvalidInputs_ReturnsFiniteZero(float width, float height, float maxAspect)
    {
        Vector2 size = UIContentSafeFrame.CalculateConstrainedSize(new Vector2(width, height), maxAspect);

        Assert.That(size.x, Is.EqualTo(0f));
        Assert.That(size.y, Is.EqualTo(0f));
        Assert.That(float.IsNaN(size.x) || float.IsInfinity(size.x), Is.False);
        Assert.That(float.IsNaN(size.y) || float.IsInfinity(size.y), Is.False);
    }

    [Test]
    public void ScreenAndModalPrefabs_HaveContentSafeFrame()
    {
        foreach (string path in SafeFramePrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponentInChildren<UIContentSafeFrame>(includeInactive: true), Is.Not.Null, path);
        }
    }

    [Test]
    public void StartUpPrefab_KeepsBackgroundOutsideContentSafeFrame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StartUp/StartUp UI Prefab.prefab");
        Assert.That(prefab, Is.Not.Null);

        UIContentSafeFrame safeFrame = prefab.GetComponentInChildren<UIContentSafeFrame>(includeInactive: true);
        Assert.That(safeFrame, Is.Not.Null);
        Assert.That(prefab.GetComponent<CoverImage>(), Is.Not.Null);
        Assert.That(safeFrame.GetComponent<CoverImage>(), Is.Null);

        Transform buttonPanel = prefab.transform.Find("Content Safe Frame/Button Panel");
        Transform sealPanel = prefab.transform.Find("Content Safe Frame/Seal Panel");
        Assert.That(buttonPanel, Is.Not.Null);
        Assert.That(sealPanel, Is.Not.Null);
    }

    [Test]
    public void MainUIPrefab_KeepsDangerEdgeOutsideContentSafeFrame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MainHUD/MainUI.prefab");
        Assert.That(prefab, Is.Not.Null);

        Assert.That(prefab.transform.Find("Danger Edge"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Danger Edge"), Is.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Pause Btn"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Player Info Panel"), Is.Not.Null);
    }

    [Test]
    public void LoadingPrefab_KeepsBackgroundOutsideContentSafeFrame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/System/Loading Panel.prefab");
        Assert.That(prefab, Is.Not.Null);

        Assert.That(prefab.transform.Find("Background"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Background"), Is.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Text"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Progress"), Is.Not.Null);
    }

    [Test]
    public void StorytellerPrefab_KeepsBackgroundOutsideContentSafeFrame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Storyteller Panel.prefab");
        Assert.That(prefab, Is.Not.Null);

        Assert.That(prefab.transform.Find("BackGround"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/BackGround"), Is.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Text"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Content Safe Frame/Skip Button"), Is.Not.Null);
    }

    [Test]
    public void GuidePopup_DoesNotUseContentSafeFrame()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Guide/Guide Popup.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<UIContentSafeFrame>(includeInactive: true), Is.Null);
    }
}
