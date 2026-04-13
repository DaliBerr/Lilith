using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryTellerUIScreenTests
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
    public void StoryTellerUIScreen_UpdatesTextFromParserSnapshot()
    {
        GameObject parserObject = new(nameof(StorySequenceParser));
        createdObjects.Add(parserObject);
        TestStorySequenceParser parser = parserObject.AddComponent<TestStorySequenceParser>();
        parser.InitializeForTest();

        StoryTellerUIScreen screen = CreateStoryTellerScreen(out TMP_Text storyText, out GameObject skipButtonRoot, out Button skipButton);
        InvokeNonPublic(screen, "OnInit");
        Assert.That(skipButtonRoot.activeSelf, Is.False);
        InvokeNonPublic(screen, "OnAfterShow");

        parser.EmitSnapshot(new StorySequenceSnapshot(
            "narrator",
            "旁白",
            "Hello World",
            5,
            0,
            1,
            false,
            true));

        Assert.That(storyText.text, Is.EqualTo("Hello World"));
        Assert.That(storyText.maxVisibleCharacters, Is.EqualTo(5));
        Assert.That(skipButtonRoot.activeSelf, Is.True);

        skipButton.onClick.Invoke();
        Assert.That(parser.SkipToNextReplaceRequested, Is.True);

        InvokeNonPublic(screen, "OnAfterHide");
        parser.EmitSnapshot(new StorySequenceSnapshot(
            "narrator",
            "旁白",
            "Updated",
            7,
            0,
            1,
            true,
            true));

        Assert.That(storyText.text, Is.EqualTo(string.Empty));
        Assert.That(skipButtonRoot.activeSelf, Is.False);
    }

    [Test]
    public void StoryTellerUIScreen_PointerClickRequestsAdvance()
    {
        GameObject parserObject = new(nameof(StorySequenceParser));
        createdObjects.Add(parserObject);
        TestStorySequenceParser parser = parserObject.AddComponent<TestStorySequenceParser>();
        parser.InitializeForTest();

        StoryTellerUIScreen screen = CreateStoryTellerScreen(out TMP_Text storyText, out GameObject skipButtonRoot, out Button skipButton);
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnAfterShow");

        parser.EmitSnapshot(new StorySequenceSnapshot(
            "narrator",
            "旁白",
            "Hello World",
            11,
            0,
            1,
            true,
            true));

        screen.OnPointerClick(null);

        Assert.That(parser.AdvanceToNextEntryRequested, Is.True);
        Assert.That(storyText.text, Is.EqualTo("Hello World"));
        Assert.That(skipButtonRoot.activeSelf, Is.True);
        Assert.That(skipButton, Is.Not.Null);
    }

    private StoryTellerUIScreen CreateStoryTellerScreen(out TMP_Text storyText, out GameObject skipButtonRoot, out Button skipButton)
    {
        GameObject root = new("Storyteller Panel", typeof(RectTransform));
        createdObjects.Add(root);
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        StoryTellerUIScreen screen = root.AddComponent<StoryTellerUIScreen>();

        GameObject textObject = new("Text", typeof(RectTransform));
        createdObjects.Add(textObject);
        textObject.transform.SetParent(root.transform, false);
        storyText = textObject.AddComponent<TextMeshProUGUI>();

        skipButtonRoot = new GameObject("Skip Button", typeof(RectTransform), typeof(Image));
        createdObjects.Add(skipButtonRoot);
        skipButtonRoot.transform.SetParent(root.transform, false);

        GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        createdObjects.Add(buttonObject);
        buttonObject.transform.SetParent(skipButtonRoot.transform, false);
        skipButton = buttonObject.GetComponent<Button>();
        return screen;
    }

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }

    private sealed class TestStorySequenceParser : StorySequenceParser
    {
        public bool SkipToNextReplaceRequested { get; private set; }
        public bool AdvanceToNextEntryRequested { get; private set; }

        public void InitializeForTest()
        {
            Awake();
        }

        public void EmitSnapshot(StorySequenceSnapshot snapshot)
        {
            PublishSnapshot(snapshot);
        }

        public override void RequestSkipToNextReplaceOrFinish()
        {
            SkipToNextReplaceRequested = true;
        }

        public override void RequestAdvanceToNextEntryOrFinish()
        {
            AdvanceToNextEntryRequested = true;
        }
    }
}
