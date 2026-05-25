using System.Globalization;
using UnityEngine;

namespace Kernel.Display
{
    public static class LilithDisplaySettings
    {
        public const string ResolutionPrefsKey = "Options.Display.Resolution";
        public const string FullscreenPrefsKey = "Options.Display.Fullscreen";
        public const string VSyncPrefsKey = "Options.Display.VSync";
        public const int BaseTargetFrameRateLimit = 360;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyFramePacingBeforeSceneLoad()
        {
            ApplyStoredVSyncAndFrameRateLimit();
        }

        public static void ApplyStoredDisplaySettings()
        {
            ApplyStoredResolutionAndFullscreen();
            ApplyStoredVSyncAndFrameRateLimit();
        }

        public static void ApplyStoredResolutionAndFullscreen()
        {
            bool hasStoredResolution = PlayerPrefs.HasKey(ResolutionPrefsKey);
            bool hasStoredFullscreen = PlayerPrefs.HasKey(FullscreenPrefsKey);
            if (!hasStoredResolution && !hasStoredFullscreen)
            {
                return;
            }

            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            if (hasStoredResolution
                && !TryParseResolutionValue(PlayerPrefs.GetString(ResolutionPrefsKey, string.Empty), out width, out height))
            {
                width = Mathf.Max(1, Screen.width);
                height = Mathf.Max(1, Screen.height);
            }

            bool fullscreen = hasStoredFullscreen
                ? PlayerPrefs.GetInt(FullscreenPrefsKey, Screen.fullScreen ? 1 : 0) != 0
                : Screen.fullScreen;
            ApplyResolutionAndFullscreen(width, height, fullscreen);
        }

        public static void ApplyResolutionAndFullscreen(int width, int height, bool fullscreen)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(safeWidth, safeHeight, mode);
        }

        public static void ApplyStoredVSyncAndFrameRateLimit()
        {
            bool vSyncEnabled = PlayerPrefs.GetInt(VSyncPrefsKey, 1) != 0;
            ApplyVSyncAndFrameRateLimit(vSyncEnabled);
        }

        public static void ApplyVSyncAndFrameRateLimit(bool vSyncEnabled)
        {
            QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
            ApplyFrameRateLimit();
        }

        public static void ApplyFrameRateLimit()
        {
            Application.targetFrameRate = ResolveTargetFrameRateLimit();
        }

        public static int ResolveTargetFrameRateLimit()
        {
            return ResolveTargetFrameRateLimit(Screen.currentResolution.refreshRateRatio.value);
        }

        public static int ResolveTargetFrameRateLimit(double displayRefreshRate)
        {
            if (double.IsNaN(displayRefreshRate) || double.IsInfinity(displayRefreshRate) || displayRefreshRate <= 0d)
            {
                return BaseTargetFrameRateLimit;
            }

            int refreshRateLimit = Mathf.CeilToInt((float)displayRefreshRate);
            return Mathf.Max(BaseTargetFrameRateLimit, refreshRateLimit);
        }

        public static bool TryParseResolutionValue(string value, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Trim().Split('x', 'X');
            return parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height)
                && width > 0
                && height > 0;
        }

        public static string FormatResolutionValue(int width, int height)
        {
            return $"{Mathf.Max(1, width)}x{Mathf.Max(1, height)}";
        }
    }
}
