using Kernel.Display;
using NUnit.Framework;

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
}
