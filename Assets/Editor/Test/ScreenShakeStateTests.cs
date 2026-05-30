using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class ScreenShakeStateTests
{
    private readonly List<ScreenShakeState> enabledStates = new();

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(ScreenShakeSettings.PlayerPrefsKey);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = enabledStates.Count - 1; i >= 0; i--)
        {
            enabledStates[i]?.Disable();
        }

        enabledStates.Clear();
        PlayerPrefs.DeleteKey(ScreenShakeSettings.PlayerPrefsKey);
    }

    [Test]
    public void ScreenShakeRequestEvent_ProducesCameraLocalOffset()
    {
        ScreenShakeState state = CreateEnabledState();

        EventManager.eventBus.Publish(new ScreenShakeRequestEvent(this, 0.5f, 1f, 1f));
        Vector3 offset = state.Tick(0f, Quaternion.identity);

        Assert.That(offset.sqrMagnitude, Is.GreaterThan(0.0001f));
        Assert.That(offset.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void PlayerDamageEvent_ProducesShakeOffset()
    {
        ScreenShakeState state = CreateEnabledState();

        EventManager.eventBus.Publish(new PlayerHealthChangedEvent(null, 100f, 75f, 100f, -25f, false));
        Vector3 offset = state.Tick(0f, Quaternion.identity);

        Assert.That(offset.sqrMagnitude, Is.GreaterThan(0.0001f));
    }

    [Test]
    public void Tick_AfterDuration_ClearsShakeOffset()
    {
        ScreenShakeState state = new();

        state.RequestShake(1f, 0.1f, 1f);
        Vector3 firstOffset = state.Tick(0f, Quaternion.identity);
        Vector3 expiredOffset = state.Tick(0.1f, Quaternion.identity);

        Assert.That(firstOffset.sqrMagnitude, Is.GreaterThan(0.0001f));
        Assert.That(expiredOffset, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void DisabledSetting_BlocksNewShakeRequests()
    {
        PlayerPrefs.SetInt(ScreenShakeSettings.PlayerPrefsKey, 0);
        ScreenShakeState state = CreateEnabledState();

        EventManager.eventBus.Publish(new ScreenShakeRequestEvent(this, 0.5f, 1f, 1f));
        Vector3 offset = state.Tick(0f, Quaternion.identity);

        Assert.That(offset, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void DisabledSetting_ClearsActiveShakeOffset()
    {
        ScreenShakeState state = new();

        state.RequestShake(1f, 1f, 1f);
        Vector3 firstOffset = state.Tick(0f, Quaternion.identity);
        PlayerPrefs.SetInt(ScreenShakeSettings.PlayerPrefsKey, 0);
        Vector3 disabledOffset = state.Tick(0f, Quaternion.identity);

        Assert.That(firstOffset.sqrMagnitude, Is.GreaterThan(0.0001f));
        Assert.That(disabledOffset, Is.EqualTo(Vector3.zero));
    }

    private ScreenShakeState CreateEnabledState()
    {
        ScreenShakeState state = new();
        state.Enable();
        enabledStates.Add(state);
        return state;
    }
}
