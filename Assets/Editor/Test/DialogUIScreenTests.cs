using System.Collections.Generic;
using System.Reflection;
using Kernel.GameState;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DialogUIScreenTests
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
    public void DialogUIScreen_UpdatesSpeakerAndTextFromParserSnapshot()
    {
        GameObject parserObject = new(nameof(StorySequenceParser));
        createdObjects.Add(parserObject);
        TestStorySequenceParser parser = parserObject.AddComponent<TestStorySequenceParser>();
        parser.InitializeForTest();

        DialogUIScreen screen = CreateDialogScreen(
            out TMP_Text dialogText,
            out GameObject speakerInfoRoot,
            out TMP_Text speakerNameText);
        InvokeNonPublic(screen, "OnInit");
        Assert.That(speakerInfoRoot.activeSelf, Is.False);
        InvokeNonPublic(screen, "OnAfterShow");

        parser.EmitSnapshot(new StorySequenceSnapshot(
            "lilith",
            "莉莉丝",
            "你好，新的 Dialog UI。",
            4,
            0,
            2,
            false,
            false));

        Assert.That(dialogText.text, Is.EqualTo("你好，新的 Dialog UI。"));
        Assert.That(dialogText.maxVisibleCharacters, Is.EqualTo(4));
        Assert.That(speakerInfoRoot.activeSelf, Is.True);
        Assert.That(speakerNameText.text, Is.EqualTo("莉莉丝"));

        screen.OnPointerClick(new PointerEventData(EventSystem.current));
        Assert.That(parser.AdvanceRequested, Is.True);

        InvokeNonPublic(screen, "OnAfterHide");
        parser.EmitSnapshot(new StorySequenceSnapshot(
            "watcher",
            "观测者",
            "这条快照不应该再更新到隐藏后的界面。",
            10,
            1,
            2,
            true,
            false));

        Assert.That(dialogText.text, Is.EqualTo(string.Empty));
        Assert.That(speakerInfoRoot.activeSelf, Is.False);
    }

    [Test]
    public void DialogUIScreen_UsesInDialogStatus()
    {
        DialogUIScreen screen = CreateDialogScreen(
            out _,
            out _,
            out _);

        Assert.That(screen.currentStatus.StatusName, Is.EqualTo(StatusList.InDialogStatus.StatusName));
    }

    private DialogUIScreen CreateDialogScreen(out TMP_Text dialogText, out GameObject speakerInfoRoot, out TMP_Text speakerNameText)
    {
        GameObject root = new("Dialog UI", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        createdObjects.Add(root);
        DialogUIScreen screen = root.AddComponent<DialogUIScreen>();

        GameObject mainContent = new("Main Content", typeof(RectTransform), typeof(Image));
        createdObjects.Add(mainContent);
        mainContent.transform.SetParent(root.transform, false);

        GameObject dialogTextObject = new("Text (TMP)", typeof(RectTransform));
        createdObjects.Add(dialogTextObject);
        dialogTextObject.transform.SetParent(mainContent.transform, false);
        dialogText = dialogTextObject.AddComponent<TextMeshProUGUI>();

        speakerInfoRoot = new GameObject("Info Panel", typeof(RectTransform), typeof(Image));
        createdObjects.Add(speakerInfoRoot);
        speakerInfoRoot.transform.SetParent(root.transform, false);

        GameObject speakerTextObject = new("Text (TMP)", typeof(RectTransform));
        createdObjects.Add(speakerTextObject);
        speakerTextObject.transform.SetParent(speakerInfoRoot.transform, false);
        speakerNameText = speakerTextObject.AddComponent<TextMeshProUGUI>();

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        createdObjects.Add(eventSystemObject);
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
        public bool AdvanceRequested { get; private set; }

        public void InitializeForTest()
        {
            Awake();
        }

        public void EmitSnapshot(StorySequenceSnapshot snapshot)
        {
            PublishSnapshot(snapshot);
        }

        public override void RequestAdvanceToNextEntryOrFinish()
        {
            AdvanceRequested = true;
        }
    }
}
