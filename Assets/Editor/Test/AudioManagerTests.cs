using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vocalith.Audio;

public sealed class AudioManagerTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyExistingAudioManagers();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyExistingAudioManagers();
    }

    [Test]
    public void SetVolumes_ClampsInvalidValues()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();

        audioManager.SetVolumes(-1f, 2f, float.NaN);

        Assert.That(audioManager.MasterVolume, Is.EqualTo(0f));
        Assert.That(audioManager.MusicVolume, Is.EqualTo(1f));
        Assert.That(audioManager.SfxVolume, Is.EqualTo(0f));
        Assert.That(float.IsNaN(audioManager.EffectiveMusicVolume), Is.False);
        Assert.That(float.IsNaN(audioManager.EffectiveSfxVolume), Is.False);
    }

    [Test]
    public void GetOrCreateInstance_ReusesSingleInstance()
    {
        AudioManager first = AudioManager.GetOrCreateInstance();
        AudioManager second = AudioManager.GetOrCreateInstance();

        Assert.That(second, Is.SameAs(first));
        Assert.That(FindAudioManagers().Length, Is.EqualTo(1));
    }

    [Test]
    public void EmptyClipAndAddressCalls_DoNotThrow()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioCue emptyCue = CreateCue((AudioClip)null);

        try
        {
            Assert.DoesNotThrow(() => audioManager.StopMusic());
            Assert.DoesNotThrow(() => audioManager.PlayCue(null));
            Assert.That(audioManager.PlayCue(emptyCue).IsValid, Is.False);
        }
        finally
        {
            DestroyAsset(emptyCue);
        }
    }

    [Test]
    public void PlayCue_ReturnsValidHandleAndStopInvalidatesIt()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("handle_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip);

        try
        {
            AudioPlaybackHandle handle = audioManager.PlayCue(cue);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(1));

            Assert.That(handle.Stop(0f), Is.True);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(0));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void OldHandleCannotStopReusedSource()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip firstClip = AudioClip.Create("old_handle_a", 32, 1, 44100, false);
        AudioClip secondClip = AudioClip.Create("old_handle_b", 32, 1, 44100, false);
        AudioCue firstCue = CreateCue(firstClip);
        AudioCue secondCue = CreateCue(secondClip);

        try
        {
            AudioPlaybackHandle oldHandle = audioManager.PlayCue(firstCue);
            oldHandle.Stop(0f);
            AudioPlaybackHandle newHandle = audioManager.PlayCue(secondCue);

            Assert.That(newHandle.IsValid, Is.True);
            Assert.That(oldHandle.Stop(0f), Is.False);
            Assert.That(CountSourcesWithClip(audioManager, secondClip), Is.EqualTo(1));
        }
        finally
        {
            DestroyAsset(firstCue);
            DestroyAsset(secondCue);
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }
    }

    [Test]
    public void HandlePauseAndResume_KeepsHandleValid()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("pause_handle", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip);

        try
        {
            AudioPlaybackHandle handle = audioManager.PlayCue(cue);

            Assert.That(handle.Pause(), Is.True);
            InvokePrivate(audioManager, "LateUpdate");
            Assert.That(handle.IsValid, Is.True);

            Assert.That(handle.Resume(), Is.True);
            Assert.That(handle.IsValid, Is.True);
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [UnityTest]
    public IEnumerator OneShotPlaybackEnd_CleansState()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("oneshot_cleanup", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip);

        AudioPlaybackHandle handle = audioManager.PlayCue(cue);
        AudioSource source = FindSourceWithClip(audioManager, clip);
        Assert.That(handle.IsValid, Is.True);
        Assert.That(source, Is.Not.Null);

        source.Stop();
        InvokePrivate(audioManager, "LateUpdate");
        yield return null;

        Assert.That(handle.IsValid, Is.False);
        Assert.That(audioManager.GetStats().ActiveVoiceCount, Is.EqualTo(0));

        DestroyAsset(cue);
        Object.DestroyImmediate(clip);
    }

    [Test]
    public void PlayCue_DoesNotGrowPoolPastDefaultSize()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip[] clips = new AudioClip[64];
        AudioCue[] cues = new AudioCue[64];

        try
        {
            for (int i = 0; i < 64; i++)
            {
                clips[i] = AudioClip.Create($"test_sfx_{i}", 32, 1, 44100, false);
                cues[i] = CreateCue(clips[i], category: CategoryForPoolIndex(i), maxSimultaneousSelf: 2);
                audioManager.PlayCue(cues[i], new AudioCuePlayRequest { PriorityOffset = 100 + i });
            }

            Assert.That(audioManager.SfxPoolCount, Is.EqualTo(32));
            Assert.That(audioManager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(34));
        }
        finally
        {
            for (int i = 0; i < 64; i++)
            {
                DestroyAsset(cues[i]);
                if (clips[i] != null)
                {
                    Object.DestroyImmediate(clips[i]);
                }
            }
        }
    }

    [Test]
    public void PlayCue_DirectClipPlaysWithoutAddressables()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("direct_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip);

        try
        {
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.True);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(1));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayCue_BlocksRepeatedCueInsideCooldown()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("cooldown_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip, cooldownSeconds: 10f, maxSimultaneousSelf: 8);

        try
        {
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.True);
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.False);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(1));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayCue_HighestPriorityBypassesCooldown()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("cooldown_priority_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip, cooldownSeconds: 10f, maxSimultaneousSelf: 8);

        try
        {
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.True);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = AudioManager.HighestPriority }).IsValid, Is.True);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(2));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayCue_SelfLimitRejectsEqualPriorityAndAllowsHigherPriority()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("self_limit_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip, priority: 1, maxSimultaneousSelf: 1);

        try
        {
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.True);
            Assert.That(audioManager.PlayCue(cue).IsValid, Is.False);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = 1 }).IsValid, Is.True);
            Assert.That(CountSourcesWithClip(audioManager, clip), Is.EqualTo(1));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayCue_CategoryLimitRejectsEqualPriorityAndAllowsHigherPriority()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip[] clips = new AudioClip[7];
        AudioCue[] cues = new AudioCue[7];

        try
        {
            for (int i = 0; i < 7; i++)
            {
                clips[i] = AudioClip.Create($"ui_cue_{i}", 32, 1, 44100, false);
                cues[i] = CreateCue(clips[i], category: AudioCueCategory.Ui, maxSimultaneousSelf: 2);
            }

            for (int i = 0; i < 6; i++)
            {
                Assert.That(audioManager.PlayCue(cues[i]).IsValid, Is.True);
            }

            Assert.That(audioManager.PlayCue(cues[6]).IsValid, Is.False);
            Assert.That(audioManager.PlayCue(cues[6], new AudioCuePlayRequest { PriorityOffset = 1 }).IsValid, Is.True);
            Assert.That(CountPlayingSources(audioManager), Is.EqualTo(6));
        }
        finally
        {
            for (int i = 0; i < cues.Length; i++)
            {
                DestroyAsset(cues[i]);
                if (clips[i] != null)
                {
                    Object.DestroyImmediate(clips[i]);
                }
            }
        }
    }

    [Test]
    public void PlayCue_FullPoolOnlyStealsLowerPrioritySource()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip[] fillerClips = new AudioClip[32];
        AudioCue[] fillerCues = new AudioCue[32];
        AudioClip cueClip = AudioClip.Create("pool_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(cueClip, maxSimultaneousSelf: 4);

        try
        {
            for (int i = 0; i < 32; i++)
            {
                fillerClips[i] = AudioClip.Create($"pool_filler_{i}", 32, 1, 44100, false);
                fillerCues[i] = CreateCue(fillerClips[i], category: CategoryForPoolIndex(i), maxSimultaneousSelf: 2);
                Assert.That(audioManager.PlayCue(fillerCues[i]).IsValid, Is.True);
            }

            Assert.That(audioManager.PlayCue(cue).IsValid, Is.False);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = 1 }).IsValid, Is.True);
            Assert.That(audioManager.SfxPoolCount, Is.EqualTo(32));
            Assert.That(audioManager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(34));
            Assert.That(CountSourcesWithClip(audioManager, cueClip), Is.EqualTo(1));
        }
        finally
        {
            DestroyAsset(cue);
            for (int i = 0; i < fillerCues.Length; i++)
            {
                DestroyAsset(fillerCues[i]);
                if (fillerClips[i] != null)
                {
                    Object.DestroyImmediate(fillerClips[i]);
                }
            }

            Object.DestroyImmediate(cueClip);
        }
    }

    [Test]
    public void PlayCueAt_ConfiguresWorldPositionAudioSource()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("world_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(
            clip,
            spatialMode: AudioCueSpatialMode.WorldPosition,
            minDistance: 2f,
            maxDistance: 14f);
        Vector3 position = new(3f, 4f, 5f);

        try
        {
            Assert.That(audioManager.PlayCueAt(cue, position).IsValid, Is.True);
            AudioSource source = FindSourceWithClip(audioManager, clip);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.minDistance, Is.EqualTo(2f));
            Assert.That(source.maxDistance, Is.EqualTo(14f));
            Assert.That(source.transform.position, Is.EqualTo(position));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void LoopCue_KeepsPlayingUntilStopped()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("loop_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip, playbackMode: AudioCuePlaybackMode.Loop);

        try
        {
            AudioPlaybackHandle handle = audioManager.PlayCue(cue);
            AudioSource source = FindSourceWithClip(audioManager, clip);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.loop, Is.True);
            Assert.That(handle.Stop(0f), Is.True);
            Assert.That(handle.IsValid, Is.False);
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayCueFollow_TracksTargetAndStopsWhenDestroyed()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("follow_cue", 32, 1, 44100, false);
        AudioCue cue = CreateCue(clip, playbackMode: AudioCuePlaybackMode.Loop, spatialMode: AudioCueSpatialMode.WorldPosition);
        GameObject target = new("AudioFollowTarget");
        target.transform.position = new Vector3(1f, 2f, 3f);

        try
        {
            AudioPlaybackHandle handle = audioManager.PlayCueFollow(cue, target.transform);
            AudioSource source = FindSourceWithClip(audioManager, clip);
            Assert.That(handle.IsValid, Is.True);
            Assert.That(source.transform.position, Is.EqualTo(target.transform.position));

            target.transform.position = new Vector3(4f, 5f, 6f);
            InvokePrivate(audioManager, "LateUpdate");
            Assert.That(source.transform.position, Is.EqualTo(target.transform.position));

            Object.DestroyImmediate(target);
            InvokePrivate(audioManager, "LateUpdate");
            Assert.That(handle.IsValid, Is.False);
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(clip);
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    [Test]
    public void VariantSelection_AvoidsImmediateRepeatWhenPossible()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip firstClip = AudioClip.Create("variant_a", 32, 1, 44100, false);
        AudioClip secondClip = AudioClip.Create("variant_b", 32, 1, 44100, false);
        AudioCue cue = CreateCue(new[] { CreateVariant(firstClip), CreateVariant(secondClip) }, maxSimultaneousSelf: 4);

        try
        {
            AudioPlaybackHandle firstHandle = audioManager.PlayCue(cue);
            AudioClip firstPlayed = FindAnyCueClip(audioManager, firstClip, secondClip);
            firstHandle.Stop(0f);

            AudioPlaybackHandle secondHandle = audioManager.PlayCue(cue);
            AudioClip secondPlayed = FindAnyCueClip(audioManager, firstClip, secondClip);
            secondHandle.Stop(0f);

            Assert.That(firstPlayed, Is.Not.Null);
            Assert.That(secondPlayed, Is.Not.Null);
            Assert.That(secondPlayed, Is.Not.SameAs(firstPlayed));
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }
    }

    [UnityTest]
    public IEnumerator MusicCue_WithIntroTransitionsToLoop()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip introClip = AudioClip.Create("music_intro", 32, 1, 44100, false);
        AudioClip loopClip = AudioClip.Create("music_loop", 44100, 1, 44100, false);
        AudioCue cue = CreateCue(
            new AudioCueVariant[0],
            kind: AudioCueKind.Music,
            playbackMode: AudioCuePlaybackMode.Loop,
            introVariants: new[] { CreateVariant(introClip) },
            loopVariants: new[] { CreateVariant(loopClip) });

        AudioPlaybackHandle handle = audioManager.PlayCue(cue);
        Assert.That(handle.IsValid, Is.True);

        AudioSource introSource = FindSourceWithClip(audioManager, introClip);
        if (introSource != null)
        {
            introSource.Stop();
        }

        yield return null;
        yield return null;

        AudioSource loopSource = FindSourceWithClip(audioManager, loopClip);
        Assert.That(loopSource, Is.Not.Null);
        Assert.That(loopSource.loop, Is.True);

        handle.Stop(0f);
        DestroyAsset(cue);
        Object.DestroyImmediate(introClip);
        Object.DestroyImmediate(loopClip);
    }

    [Test]
    public void MusicCue_WithoutIntroStartsLoopDirectly()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip loopClip = AudioClip.Create("music_direct_loop", 44100, 1, 44100, false);
        AudioCue cue = CreateCue(
            new[] { CreateVariant(loopClip) },
            kind: AudioCueKind.Music,
            playbackMode: AudioCuePlaybackMode.Loop);

        try
        {
            AudioPlaybackHandle handle = audioManager.PlayCue(cue);
            AudioSource loopSource = FindSourceWithClip(audioManager, loopClip);

            Assert.That(handle.IsValid, Is.True);
            Assert.That(loopSource, Is.Not.Null);
            Assert.That(loopSource.loop, Is.True);
        }
        finally
        {
            DestroyAsset(cue);
            Object.DestroyImmediate(loopClip);
        }
    }

    [Test]
    public void SetAudioPaused_RejectsPausableRequestsAndAllowsContinueRequests()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip combatClip = AudioClip.Create("pause_combat", 32, 1, 44100, false);
        AudioClip uiClip = AudioClip.Create("pause_ui", 32, 1, 44100, false);
        AudioCue combatCue = CreateCue(combatClip, category: AudioCueCategory.Combat, playbackMode: AudioCuePlaybackMode.Loop);
        AudioCue uiCue = CreateCue(uiClip, category: AudioCueCategory.Ui, playbackMode: AudioCuePlaybackMode.Loop);

        try
        {
            Assert.That(audioManager.PlayCue(combatCue).IsValid, Is.True);
            Assert.That(audioManager.PlayCue(uiCue).IsValid, Is.True);

            audioManager.SetAudioPaused(true);

            Assert.That(audioManager.PlayCue(combatCue, new AudioCuePlayRequest { PriorityOffset = 10 }).IsValid, Is.False);
            Assert.That(audioManager.PlayCue(uiCue, new AudioCuePlayRequest { PriorityOffset = 10 }).IsValid, Is.True);

            audioManager.SetAudioPaused(false);
            Assert.That(audioManager.PlayCue(combatCue, new AudioCuePlayRequest { PriorityOffset = 20 }).IsValid, Is.True);
        }
        finally
        {
            DestroyAsset(combatCue);
            DestroyAsset(uiCue);
            Object.DestroyImmediate(combatClip);
            Object.DestroyImmediate(uiClip);
        }
    }

    [UnityTest]
    public IEnumerator AudioCueBank_PreloadsAndReleasesDirectClipCues()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip firstClip = AudioClip.Create("bank_a", 32, 1, 44100, false);
        AudioClip secondClip = AudioClip.Create("bank_b", 32, 1, 44100, false);
        AudioCue firstCue = CreateCue(firstClip);
        AudioCue secondCue = CreateCue(secondClip);
        AudioCueBank bank = CreateBank(firstCue, secondCue);

        yield return audioManager.PreloadBank(bank);
        yield return audioManager.PreloadBank(bank);

        Assert.That(audioManager.PlayCue(firstCue).IsValid, Is.True);
        Assert.That(audioManager.PlayCue(secondCue).IsValid, Is.True);

        audioManager.ReleaseBank(bank);
        Assert.That(audioManager.PlayCue(firstCue, new AudioCuePlayRequest { PriorityOffset = 1 }).IsValid, Is.True);

        DestroyAsset(bank);
        DestroyAsset(firstCue);
        DestroyAsset(secondCue);
        Object.DestroyImmediate(firstClip);
        Object.DestroyImmediate(secondClip);
    }

    [Test]
    public void GetStatsAndResetStats_ReflectAcceptedRejectedAndStolenCounts()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip lowClip = AudioClip.Create("stats_low", 32, 1, 44100, false);
        AudioClip highClip = AudioClip.Create("stats_high", 32, 1, 44100, false);
        AudioCue lowCue = CreateCue(lowClip, priority: 0, maxSimultaneousSelf: 1);
        AudioCue highCue = CreateCue(highClip, priority: 1, maxSimultaneousSelf: 1);

        try
        {
            audioManager.PlayCue(lowCue);
            audioManager.PlayCue(lowCue);
            audioManager.PlayCue(highCue, new AudioCuePlayRequest { CategoryOverride = AudioCueCategory.Default, HasCategoryOverride = true });

            AudioManagerStats stats = audioManager.GetStats();
            Assert.That(stats.AcceptedCount, Is.EqualTo(2));
            Assert.That(stats.RejectedCount, Is.EqualTo(1));

            audioManager.ResetStats();
            stats = audioManager.GetStats();
            Assert.That(stats.AcceptedCount, Is.EqualTo(0));
            Assert.That(stats.RejectedCount, Is.EqualTo(0));
        }
        finally
        {
            DestroyAsset(lowCue);
            DestroyAsset(highCue);
            Object.DestroyImmediate(lowClip);
            Object.DestroyImmediate(highClip);
        }
    }

    private static AudioCue CreateCue(
        AudioClip clip,
        AudioCueKind kind = AudioCueKind.Sfx,
        AudioCueCategory category = AudioCueCategory.Default,
        AudioCuePlaybackMode playbackMode = AudioCuePlaybackMode.OneShot,
        AudioCuePausePolicy pausePolicy = AudioCuePausePolicy.UseCategoryDefault,
        int priority = 0,
        float cooldownSeconds = 0f,
        int maxSimultaneousSelf = 1,
        AudioCueSpatialMode spatialMode = AudioCueSpatialMode.TwoDimensional,
        float minDistance = 1f,
        float maxDistance = 25f)
    {
        return CreateCue(
            clip != null ? new[] { CreateVariant(clip) } : new AudioCueVariant[0],
            kind,
            category,
            playbackMode,
            pausePolicy,
            priority,
            cooldownSeconds,
            maxSimultaneousSelf,
            spatialMode,
            minDistance,
            maxDistance);
    }

    private static AudioCue CreateCue(
        IReadOnlyList<AudioCueVariant> variants,
        AudioCueKind kind = AudioCueKind.Sfx,
        AudioCueCategory category = AudioCueCategory.Default,
        AudioCuePlaybackMode playbackMode = AudioCuePlaybackMode.OneShot,
        AudioCuePausePolicy pausePolicy = AudioCuePausePolicy.UseCategoryDefault,
        int priority = 0,
        float cooldownSeconds = 0f,
        int maxSimultaneousSelf = 1,
        AudioCueSpatialMode spatialMode = AudioCueSpatialMode.TwoDimensional,
        float minDistance = 1f,
        float maxDistance = 25f,
        IReadOnlyList<AudioCueVariant> introVariants = null,
        IReadOnlyList<AudioCueVariant> loopVariants = null)
    {
        AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
        SetPrivateField(cue, "kind", kind);
        SetPrivateField(cue, "category", category);
        SetPrivateField(cue, "playbackMode", playbackMode);
        SetPrivateField(cue, "pausePolicy", pausePolicy);
        SetPrivateField(cue, "priority", priority);
        SetPrivateField(cue, "cooldownSeconds", cooldownSeconds);
        SetPrivateField(cue, "maxSimultaneousSelf", maxSimultaneousSelf);
        SetPrivateField(cue, "spatialMode", spatialMode);
        SetPrivateField(cue, "minDistance", minDistance);
        SetPrivateField(cue, "maxDistance", maxDistance);
        SetPrivateField(cue, "variants", new List<AudioCueVariant>(variants));
        SetPrivateField(cue, "introVariants", new List<AudioCueVariant>(introVariants ?? new AudioCueVariant[0]));
        SetPrivateField(cue, "loopVariants", new List<AudioCueVariant>(loopVariants ?? new AudioCueVariant[0]));
        return cue;
    }

    private static AudioCueVariant CreateVariant(AudioClip clip, float weight = 1f)
    {
        AudioCueVariant variant = new();
        SetPrivateField(variant, "clip", clip);
        SetPrivateField(variant, "weight", weight);
        return variant;
    }

    private static AudioCueBank CreateBank(params AudioCue[] cues)
    {
        AudioCueBank bank = ScriptableObject.CreateInstance<AudioCueBank>();
        SetPrivateField(bank, "cues", new List<AudioCue>(cues));
        return bank;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName} on {target.GetType().Name}");
        method.Invoke(target, null);
    }

    private static int CountSourcesWithClip(AudioManager audioManager, AudioClip clip)
    {
        int count = 0;
        AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].clip == clip)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountPlayingSources(AudioManager audioManager)
    {
        int count = 0;
        AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].isPlaying)
            {
                count++;
            }
        }

        return count;
    }

    private static AudioCueCategory CategoryForPoolIndex(int index)
    {
        if (index < 6)
        {
            return AudioCueCategory.Ui;
        }

        if (index < 24)
        {
            return AudioCueCategory.Combat;
        }

        if (index < 28)
        {
            return AudioCueCategory.Ambience;
        }

        if (index < 31)
        {
            return AudioCueCategory.Voice;
        }

        return AudioCueCategory.System;
    }

    private static AudioSource FindSourceWithClip(AudioManager audioManager, AudioClip clip)
    {
        AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].clip == clip)
            {
                return sources[i];
            }
        }

        return null;
    }

    private static AudioClip FindAnyCueClip(AudioManager audioManager, params AudioClip[] clips)
    {
        AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            for (int j = 0; j < clips.Length; j++)
            {
                if (sources[i].clip == clips[j])
                {
                    return clips[j];
                }
            }
        }

        return null;
    }

    private static AudioManager[] FindAudioManagers()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<AudioManager>(true);
#endif
    }

    private static void DestroyExistingAudioManagers()
    {
        AudioManager[] audioManagers = FindAudioManagers();
        for (int i = 0; i < audioManagers.Length; i++)
        {
            if (audioManagers[i] != null)
            {
                Object.DestroyImmediate(audioManagers[i].gameObject);
            }
        }
    }

    private static void DestroyAsset(Object asset)
    {
        if (asset != null)
        {
            Object.DestroyImmediate(asset);
        }
    }
}
