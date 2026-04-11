using System.Collections;
using System.Collections.Generic;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StorySequenceParserTests
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
    public void TryParseJson_WithValidEntries_ReturnsEntriesInOriginalOrder()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""text"": ""First line"" },
    { ""text"": ""Second line"" },
    { ""text"": ""Third line"" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.True);
        Assert.That(errorMessage, Is.Null.Or.Empty);
        CollectionAssert.AreEqual(new[]
        {
            "First line",
            "Second line",
            "Third line"
        }, GetTexts(data));
    }

    [Test]
    public void TryParseJson_WithSpeakerFields_PreservesSpeakerMetadata()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""speakerId"": ""narrator"", ""displayName"": ""旁白"", ""text"": ""First line"" },
    { ""speakerId"": ""hero"", ""displayName"": ""主角"", ""text"": ""Second line"" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.True);
        Assert.That(errorMessage, Is.Null.Or.Empty);
        Assert.That(data.Entries.Count, Is.EqualTo(2));
        Assert.That(data.Entries[0].SpeakerId, Is.EqualTo("narrator"));
        Assert.That(data.Entries[0].DisplayName, Is.EqualTo("旁白"));
        Assert.That(data.Entries[1].SpeakerId, Is.EqualTo("hero"));
        Assert.That(data.Entries[1].DisplayName, Is.EqualTo("主角"));
    }

    [Test]
    public void TryParseJson_WithDisplayMode_PreservesDisplayBehavior()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""text"": ""First line"", ""displayMode"": ""replace"" },
    { ""text"": ""Second line"", ""displayMode"": ""append"" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.True);
        Assert.That(errorMessage, Is.Null.Or.Empty);
        Assert.That(data.Entries[0].DisplayMode, Is.EqualTo(StorySequenceDisplayMode.Replace));
        Assert.That(data.Entries[1].DisplayMode, Is.EqualTo(StorySequenceDisplayMode.Append));
    }

    [Test]
    public void TryParseJson_WithWhitespaceEntries_FiltersEmptyItems()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""text"": ""First line"" },
    { ""text"": ""   "" },
    { ""text"": ""Second line"" },
    { ""text"": """" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.True);
        Assert.That(errorMessage, Is.Null.Or.Empty);
        CollectionAssert.AreEqual(new[]
        {
            "First line",
            "Second line"
        }, GetTexts(data));
    }

    [Test]
    public void TryParseJson_WithEmptyObject_ReturnsFailure()
    {
        bool success = StorySequenceParser.TryParseJson("{}", out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(data, Is.Null);
        Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TryParseJson_WithInvalidJson_ReturnsFailure()
    {
        bool success = StorySequenceParser.TryParseJson("{", out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(data, Is.Null);
        Assert.That(errorMessage, Does.Contain("invalid"));
    }

    [Test]
    public void TryParseJson_WithInvalidDisplayMode_ReturnsFailure()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""text"": ""First line"", ""displayMode"": ""unknown"" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(data, Is.Null);
        Assert.That(errorMessage, Does.Contain("invalid"));
    }

    [Test]
    public void TryParseJson_WithAllWhitespaceEntries_ReturnsFailure()
    {
        const string jsonText = @"{
  ""entries"": [
    { ""text"": ""   "" },
    { ""text"": """" }
  ]
}";

        bool success = StorySequenceParser.TryParseJson(jsonText, out StorySequenceData data, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(data, Is.Null);
        Assert.That(errorMessage, Is.Not.Null.And.Not.Empty);
    }

    [UnityTest]
    public IEnumerator Service_PlaySequence_PublishesSnapshotsAndCompletion()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(
            new StorySequenceEntry { SpeakerId = "narrator", DisplayName = "旁白", Text = "ABC" },
            new StorySequenceEntry { SpeakerId = "hero", DisplayName = "主角", Text = "DE" });
        parser.DeltaTimeOverride = 1f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        parser.StartCoroutine(parser.RunSequenceForTest(request));

        while (!result.HasValue)
        {
            yield return null;
        }

        Assert.That(result.HasValue, Is.True);
        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
        Assert.That(snapshots.Count, Is.GreaterThan(0));
        Assert.That(snapshots[0].VisibleCharacterCount, Is.EqualTo(0));
        Assert.That(snapshots[0].SpeakerId, Is.EqualTo("narrator"));
        Assert.That(snapshots[snapshots.Count - 1].FullText, Is.EqualTo("DE"));
        Assert.That(snapshots[snapshots.Count - 1].IsEntryFullyRevealed, Is.True);
        Assert.That(parser.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator Service_RequestSkipCurrentEntry_RevealsCurrentLineImmediately()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(new StorySequenceEntry { Text = "ABCD" });
        parser.DeltaTimeOverride = 1f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        parser.StartCoroutine(parser.RunSequenceForTest(request));

        yield return null;
        yield return null;
        yield return null;
        yield return null;

        parser.RequestSkipCurrentEntry();

        while (!result.HasValue)
        {
            yield return null;
        }

        Assert.That(snapshots.Exists(snapshot => snapshot.VisibleCharacterCount < 4), Is.True);
        Assert.That(snapshots[snapshots.Count - 1].VisibleCharacterCount, Is.EqualTo(4));
        Assert.That(snapshots[snapshots.Count - 1].IsEntryFullyRevealed, Is.True);
        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
    }

    [Test]
    public void Service_AdvanceShortcutUsage_ShowsSkipButtonInSnapshots()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(new StorySequenceEntry { Text = "ABCD" });
        parser.DeltaTimeOverride = 0f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 10f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        parser.SnapshotChanged += snapshots.Add;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        IEnumerator routine = parser.RunSequenceForTest(request);
        ManualCoroutineRunner runner = new(routine);
        runner.StepUntil(() => snapshots.Count > 0, 20, "Initial story snapshot was not published.");

        parser.TriggerAdvanceShortcutForTest();

        Assert.That(snapshots.Exists(snapshot => snapshot.ShouldShowSkipButton), Is.True);
    }

    [UnityTest]
    public IEnumerator Service_WithMaxCharactersPerEntry_SplitsLongDialogIntoMultiplePages()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(new StorySequenceEntry
        {
            SpeakerId = "lilith",
            DisplayName = "莉莉丝",
            Text = new string('测', 261)
        });
        parser.DeltaTimeOverride = 1000f;

        StorySequenceRequest request = new()
        {
            Address = "mock://dialog",
            CharactersPerSecond = 1000f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false,
            MaxCharactersPerEntry = 260
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        parser.StartCoroutine(parser.RunSequenceForTest(request));

        while (!result.HasValue)
        {
            yield return null;
        }

        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
        Assert.That(snapshots.Exists(snapshot => snapshot.FullText.Length == 260), Is.True);
        Assert.That(snapshots[snapshots.Count - 1].FullText.Length, Is.EqualTo(1));
        Assert.That(snapshots.TrueForAll(snapshot => snapshot.FullText.Length <= 260), Is.True);
    }

    [UnityTest]
    public IEnumerator Service_AppendDisplayMode_KeepsPreviousLineAndAddsNewline()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(
            new StorySequenceEntry
            {
                Text = "First line",
                DisplayMode = StorySequenceDisplayMode.Replace
            },
            new StorySequenceEntry
            {
                Text = "Second line",
                DisplayMode = StorySequenceDisplayMode.Append
            });
        parser.DeltaTimeOverride = 100f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 100f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        parser.StartCoroutine(parser.RunSequenceForTest(request));

        while (!result.HasValue)
        {
            yield return null;
        }

        StorySequenceSnapshot appendedSnapshot = snapshots.Find(snapshot => snapshot.FullText == "First line\nSecond line");
        Assert.That(appendedSnapshot.FullText, Is.EqualTo("First line\nSecond line"));
        Assert.That(appendedSnapshot.VisibleCharacterCount, Is.GreaterThanOrEqualTo("First line\n".Length));
        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
    }

    [Test]
    public void Service_RequestAdvanceToNextEntryOrFinish_InManualAdvanceMode_RevealsThenMovesToNextEntry()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(
            new StorySequenceEntry { DisplayName = "莉莉丝", Text = "AB" },
            new StorySequenceEntry { DisplayName = "观测者", Text = "CD" });
        parser.DeltaTimeOverride = 0f;

        StorySequenceRequest request = new()
        {
            Address = "mock://dialog",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false,
            WaitForAdvanceInputAfterEntryReveal = true
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        ManualCoroutineRunner runner = new(parser.RunSequenceForTest(request));
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "AB" && snapshot.VisibleCharacterCount == 0),
            20,
            "Initial dialog snapshot was not published.");

        parser.RequestAdvanceToNextEntryOrFinish();
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "AB" && snapshot.IsEntryFullyRevealed),
            20,
            "Advance should first fully reveal the current entry.");

        parser.RequestAdvanceToNextEntryOrFinish();
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "CD" && snapshot.VisibleCharacterCount == 0),
            20,
            "Second advance should move to the next entry instead of finishing.");

        parser.RequestAdvanceToNextEntryOrFinish();
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "CD" && snapshot.IsEntryFullyRevealed),
            20,
            "Third advance should reveal the final entry.");

        Assert.That(result.HasValue, Is.False);

        parser.RequestAdvanceToNextEntryOrFinish();
        runner.StepUntil(() => result.HasValue, 10, "Final advance should finish the dialog sequence.");

        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
        Assert.That(parser.IsPlaying, Is.False);
    }

    [Test]
    public void Service_RequestSkipToNextReplaceOrFinish_RevealsOneDisplayBlockPerClick()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(
            new StorySequenceEntry
            {
                Text = "First line",
                DisplayMode = StorySequenceDisplayMode.Replace
            },
            new StorySequenceEntry
            {
                Text = "Second line",
                DisplayMode = StorySequenceDisplayMode.Append
            },
            new StorySequenceEntry
            {
                Text = "Third line",
                DisplayMode = StorySequenceDisplayMode.Replace
            },
            new StorySequenceEntry
            {
                Text = "Fourth line",
                DisplayMode = StorySequenceDisplayMode.Append
            },
            new StorySequenceEntry
            {
                Text = "Fifth line",
                DisplayMode = StorySequenceDisplayMode.Replace
            });
        parser.DeltaTimeOverride = 0f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 10f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        IEnumerator routine = parser.RunSequenceForTest(request);
        ManualCoroutineRunner runner = new(routine);
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "First line"),
            20,
            "Initial replace snapshot was not published.");

        parser.RequestSkipToNextReplaceOrFinish();

        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "First line\nSecond line" && snapshot.IsEntryFullyRevealed),
            50,
            "First skip should reveal only the current display block.");

        Assert.That(snapshots.Exists(snapshot => snapshot.FullText == "First line\nSecond line" && snapshot.IsEntryFullyRevealed), Is.True);
        Assert.That(snapshots.Exists(snapshot => snapshot.FullText == "Third line"), Is.False);
        Assert.That(result.HasValue, Is.False);

        parser.RequestSkipToNextReplaceOrFinish();
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "Third line\nFourth line" && snapshot.IsEntryFullyRevealed),
            50,
            "Second skip should reveal only the next display block.");

        Assert.That(snapshots.Exists(snapshot => snapshot.FullText == "Fifth line"), Is.False);
        parser.StopCurrentSequence();
        Assert.That(result.HasValue, Is.True);

        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Cancelled));
    }

    [Test]
    public void Service_RequestSkipToNextReplaceOrFinish_OnFinalDisplayBlock_RevealsAllTextFirstThenFinishesOnSecondClick()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadedData = CreateData(
            new StorySequenceEntry
            {
                Text = "First line",
                DisplayMode = StorySequenceDisplayMode.Replace
            },
            new StorySequenceEntry
            {
                Text = "Second line",
                DisplayMode = StorySequenceDisplayMode.Append
            },
            new StorySequenceEntry
            {
                Text = "Third line",
                DisplayMode = StorySequenceDisplayMode.Append
            });
        parser.DeltaTimeOverride = 0f;

        StorySequenceRequest request = new()
        {
            Address = "mock://intro",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 10f,
            AllowDefaultSkipInput = false
        };

        List<StorySequenceSnapshot> snapshots = new();
        StorySequenceResult? result = null;
        parser.SnapshotChanged += snapshots.Add;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        ManualCoroutineRunner runner = new(parser.RunSequenceForTest(request));
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "First line"),
            20,
            "Initial replace snapshot was not published.");

        parser.RequestSkipToNextReplaceOrFinish();
        runner.StepUntil(
            () => snapshots.Exists(snapshot => snapshot.FullText == "First line\nSecond line\nThird line" && snapshot.IsEntryFullyRevealed),
            50,
            "First skip should reveal the entire final display block.");

        Assert.That(result.HasValue, Is.False);
        Assert.That(parser.IsPlaying, Is.True);

        parser.RequestSkipToNextReplaceOrFinish();
        runner.StepUntil(() => result.HasValue, 10, "Second skip should finish the sequence.");

        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Completed));
        Assert.That(parser.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator Service_LoadFailure_RaisesFailedCompletion()
    {
        TestStorySequenceParser parser = CreateTestParser();
        parser.NextLoadError = "Addressables failed to load TextAsset at 'missing'.";

        StorySequenceRequest request = new()
        {
            Address = "missing",
            CharactersPerSecond = 1f,
            LineHoldSeconds = 0f,
            AllowDefaultSkipInput = false
        };

        StorySequenceResult? result = null;
        parser.SequenceCompleted += sequenceResult => result = sequenceResult;

        Assert.That(parser.BeginSequenceForTest(request, out string errorMessage), Is.True, errorMessage);
        parser.StartCoroutine(parser.RunSequenceForTest(request));

        while (!result.HasValue)
        {
            yield return null;
        }

        Assert.That(result.HasValue, Is.True);
        Assert.That(result.Value.Status, Is.EqualTo(StorySequenceCompletionStatus.Failed));
        Assert.That(result.Value.ErrorMessage, Does.Contain("Addressables failed"));
        Assert.That(parser.IsPlaying, Is.False);
    }

    [Test]
    public void Service_RejectsSecondPlayRequestWhileActive()
    {
        TestStorySequenceParser parser = CreateTestParser();
        StorySequenceRequest request = new()
        {
            Address = "mock://intro"
        };

        Assert.That(parser.BeginSequenceForTest(request, out string firstError), Is.True, firstError);
        Assert.That(parser.BeginSequenceForTest(request, out string secondError), Is.False);
        Assert.That(secondError, Does.Contain("already playing"));
    }

    private TestStorySequenceParser CreateTestParser()
    {
        GameObject gameObject = new(nameof(TestStorySequenceParser));
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<TestStorySequenceParser>();
    }

    private StorySequenceData CreateData(params StorySequenceEntry[] entries)
    {
        return new StorySequenceData
        {
            Entries = new List<StorySequenceEntry>(entries)
        };
    }

    private static List<string> GetTexts(StorySequenceData data)
    {
        List<string> texts = new();
        for (int index = 0; index < data.Entries.Count; index++)
        {
            texts.Add(data.Entries[index].Text);
        }

        return texts;
    }

    private sealed class ManualCoroutineRunner
    {
        private readonly Stack<IEnumerator> stack = new();

        public ManualCoroutineRunner(IEnumerator routine)
        {
            stack.Push(routine);
        }

        public void StepUntil(System.Func<bool> predicate, int maxSteps, string failureMessage)
        {
            int steps = 0;
            while (!predicate())
            {
                Assert.That(steps++, Is.LessThan(maxSteps), failureMessage);
                Assert.That(stack.Count, Is.GreaterThan(0), "Coroutine finished before the expected condition was met.");

                IEnumerator currentRoutine = stack.Peek();
                if (!currentRoutine.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (currentRoutine.Current is IEnumerator nestedRoutine)
                {
                    stack.Push(nestedRoutine);
                }
            }
        }
    }

    private sealed class TestStorySequenceParser : StorySequenceParser
    {
        public StorySequenceData NextLoadedData { get; set; }
        public string NextLoadError { get; set; }
        public float DeltaTimeOverride { get; set; } = 0.25f;

        public bool BeginSequenceForTest(StorySequenceRequest request, out string errorMessage)
        {
            return TryBeginSequence(request, out errorMessage);
        }

        public IEnumerator RunSequenceForTest(StorySequenceRequest request)
        {
            return PlaySequenceFromRequestCo(request);
        }

        public void TriggerAdvanceShortcutForTest()
        {
            NotifyAdvanceShortcutUsed();
        }

        protected override IEnumerator LoadSequenceDataCo(
            StorySequenceRequest request,
            System.Action<StorySequenceData> onLoaded,
            System.Action<string> onError)
        {
            yield return null;

            if (NextLoadedData != null)
            {
                onLoaded?.Invoke(NextLoadedData);
                yield break;
            }

            onError?.Invoke(NextLoadError ?? "Mock load failed.");
        }

        protected override float GetPlaybackDeltaTime()
        {
            return DeltaTimeOverride;
        }
    }
}
