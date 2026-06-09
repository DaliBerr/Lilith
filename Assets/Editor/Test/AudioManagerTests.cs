using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
        AudioCue emptyCue = CreateCue(null);

        try
        {
            Assert.DoesNotThrow(() => audioManager.StopMusic());
            Assert.DoesNotThrow(() => audioManager.PlayCue(null));
            Assert.That(audioManager.PlayCue(emptyCue), Is.False);
        }
        finally
        {
            DestroyAsset(emptyCue);
        }
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
            Assert.That(audioManager.PlayCue(cue), Is.True);
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
            Assert.That(audioManager.PlayCue(cue), Is.True);
            Assert.That(audioManager.PlayCue(cue), Is.False);
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
            Assert.That(audioManager.PlayCue(cue), Is.True);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = AudioManager.HighestPriority }), Is.True);
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
            Assert.That(audioManager.PlayCue(cue), Is.True);
            Assert.That(audioManager.PlayCue(cue), Is.False);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = 1 }), Is.True);
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
                Assert.That(audioManager.PlayCue(cues[i]), Is.True);
            }

            Assert.That(audioManager.PlayCue(cues[6]), Is.False);
            Assert.That(audioManager.PlayCue(cues[6], new AudioCuePlayRequest { PriorityOffset = 1 }), Is.True);
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
                Assert.That(audioManager.PlayCue(fillerCues[i]), Is.True);
            }

            Assert.That(audioManager.PlayCue(cue), Is.False);
            Assert.That(audioManager.PlayCue(cue, new AudioCuePlayRequest { PriorityOffset = 1 }), Is.True);
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
            Assert.That(audioManager.PlayCueAt(cue, position), Is.True);
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

    private static AudioCue CreateCue(
        AudioClip clip,
        AudioCueCategory category = AudioCueCategory.Default,
        int priority = 0,
        float cooldownSeconds = 0f,
        int maxSimultaneousSelf = 1,
        AudioCueSpatialMode spatialMode = AudioCueSpatialMode.TwoDimensional,
        float minDistance = 1f,
        float maxDistance = 25f)
    {
        AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
        SetPrivateField(cue, "clip", clip);
        SetPrivateField(cue, "category", category);
        SetPrivateField(cue, "priority", priority);
        SetPrivateField(cue, "cooldownSeconds", cooldownSeconds);
        SetPrivateField(cue, "maxSimultaneousSelf", maxSimultaneousSelf);
        SetPrivateField(cue, "spatialMode", spatialMode);
        SetPrivateField(cue, "minDistance", minDistance);
        SetPrivateField(cue, "maxDistance", maxDistance);
        return cue;
    }

    private static void SetPrivateField<T>(AudioCue cue, string fieldName, T value)
    {
        FieldInfo field = typeof(AudioCue).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing AudioCue field {fieldName}");
        field.SetValue(cue, value);
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
