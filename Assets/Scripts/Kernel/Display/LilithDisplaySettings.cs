using System.Globalization;
using UnityEngine;

namespace Kernel.Display
{
    public static class LilithDisplaySettings
    {
        public const string ResolutionPrefsKey = "Options.Display.Resolution";
        public const string FullscreenPrefsKey = "Options.Display.Fullscreen";

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
