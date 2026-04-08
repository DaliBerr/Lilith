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

        StoryTellerUIScreen screen = CreateStoryTellerScreen(out TMP_Text storyText);
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnAfterShow");

        parser.EmitSnapshot(new StorySequenceSnapshot(
            "narrator",
            "旁白",
            "Hello World",
            5,
            0,
            1,
            false));

        Assert.That(storyText.text, Is.EqualTo("Hello World"));
        Assert.That(storyText.maxVisibleCharacters, Is.EqualTo(5));

        InvokeNonPublic(screen, "OnAfterHide");
        parser.EmitSnapshot(new StorySequenceSnapshot(
            "narrator",
            "旁白",
            "Updated",
            7,
            0,
            1,
            true));

        Assert.That(storyText.text, Is.EqualTo(string.Empty));
    }

    private StoryTellerUIScreen CreateStoryTellerScreen(out TMP_Text storyText)
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
        public void InitializeForTest()
        {
            Awake();
        }

        public void EmitSnapshot(StorySequenceSnapshot snapshot)
        {
            PublishSnapshot(snapshot);
        }
    }
}
