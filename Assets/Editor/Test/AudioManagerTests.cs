using System.Collections;
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

        Assert.DoesNotThrow(() => audioManager.PlayMusic(null));
        Assert.DoesNotThrow(() => audioManager.StopMusic());
        Assert.DoesNotThrow(() => audioManager.PlaySfx(null));
        Assert.DoesNotThrow(() => Exhaust(audioManager.PlayMusicByAddress(string.Empty)));
        Assert.DoesNotThrow(() => Exhaust(audioManager.PlaySfxByAddress(null)));
    }

    [Test]
    public void PlaySfx_DoesNotGrowPoolPastDefaultSize()
    {
        AudioManager audioManager = AudioManager.GetOrCreateInstance();
        AudioClip clip = AudioClip.Create("test_sfx", 32, 1, 44100, false);

        try
        {
            for (int i = 0; i < 32; i++)
            {
                audioManager.PlaySfx(clip);
            }

            Assert.That(audioManager.SfxPoolCount, Is.EqualTo(16));
            Assert.That(audioManager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(18));
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    private static void Exhaust(IEnumerator routine)
    {
        while (routine.MoveNext())
        {
        }
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
}
