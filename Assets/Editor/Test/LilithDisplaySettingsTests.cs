using Kernel.Display;
using NUnit.Framework;
using UnityEngine;

public sealed class LilithDisplaySettingsTests
{
    [Test]
    public void TryParseResolutionValue_AcceptsStoredResolution()
    {
        bool parsed = LilithDisplaySettings.TryParseResolutionValue("1920x1080", out int width, out int height);

        Assert.That(parsed, Is.True);
        Assert.That(width, Is.EqualTo(1920));
        Assert.That(height, Is.EqualTo(1080));
    }

    [Test]
    public void TryParseResolutionValue_RejectsInvalidResolution()
    {
        bool parsed = LilithDisplaySettings.TryParseResolutionValue("1920-by-1080", out int width, out int height);

        Assert.That(parsed, Is.False);
        Assert.That(width, Is.EqualTo(0));
        Assert.That(height, Is.EqualTo(0));
    }

    [Test]
    public void FormatResolutionValue_ClampsToPositiveDimensions()
    {
        Assert.That(LilithDisplaySettings.FormatResolutionValue(0, -10), Is.EqualTo("1x1"));
    }

    [Test]
    public void ResolveTargetFrameRateLimit_UsesBaseLimitWhenRefreshRateIsLower()
    {
        Assert.That(LilithDisplaySettings.ResolveTargetFrameRateLimit(144d), Is.EqualTo(360));
    }

    [Test]
    public void ResolveTargetFrameRateLimit_UsesDisplayRefreshWhenAboveBaseLimit()
    {
        Assert.That(LilithDisplaySettings.ResolveTargetFrameRateLimit(480d), Is.EqualTo(480));
    }

    [Test]
    public void ApplyStoredVSyncAndFrameRateLimit_DefaultsToVSyncOnAndResolvedCap()
    {
        bool hadStoredVSync = PlayerPrefs.HasKey(LilithDisplaySettings.VSyncPrefsKey);
        int storedVSync = PlayerPrefs.GetInt(LilithDisplaySettings.VSyncPrefsKey, 0);
        int previousVSyncCount = QualitySettings.vSyncCount;
        int previousTargetFrameRate = Application.targetFrameRate;

        try
        {
            PlayerPrefs.DeleteKey(LilithDisplaySettings.VSyncPrefsKey);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            LilithDisplaySettings.ApplyStoredVSyncAndFrameRateLimit();

            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            Assert.That(Application.targetFrameRate, Is.EqualTo(LilithDisplaySettings.ResolveTargetFrameRateLimit()));
        }
        finally
        {
            RestoreVSyncPrefs(hadStoredVSync, storedVSync);
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }
    }

    [Test]
    public void ApplyStoredVSyncAndFrameRateLimit_UsesStoredVSyncOffAndResolvedCap()
    {
        bool hadStoredVSync = PlayerPrefs.HasKey(LilithDisplaySettings.VSyncPrefsKey);
        int storedVSync = PlayerPrefs.GetInt(LilithDisplaySettings.VSyncPrefsKey, 0);
        int previousVSyncCount = QualitySettings.vSyncCount;
        int previousTargetFrameRate = Application.targetFrameRate;

        try
        {
            PlayerPrefs.SetInt(LilithDisplaySettings.VSyncPrefsKey, 0);
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;

            LilithDisplaySettings.ApplyStoredVSyncAndFrameRateLimit();

            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(LilithDisplaySettings.ResolveTargetFrameRateLimit()));
        }
        finally
        {
            RestoreVSyncPrefs(hadStoredVSync, storedVSync);
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
        }
    }

    private static void RestoreVSyncPrefs(bool hadStoredVSync, int storedVSync)
    {
        if (hadStoredVSync)
        {
            PlayerPrefs.SetInt(LilithDisplaySettings.VSyncPrefsKey, storedVSync);
        }
        else
        {
            PlayerPrefs.DeleteKey(LilithDisplaySettings.VSyncPrefsKey);
        }
    }
}
