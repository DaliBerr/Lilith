using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Kernel.Display
{
    public static class LilithDisplaySettings
    {
        public const string ResolutionPrefsKey = "Options.Display.Resolution";
        public const string TargetDisplayPrefsKey = "Options.Display.TargetDisplay";
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
            ApplyStoredTargetDisplay();
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

        public static void ApplyStoredTargetDisplay()
        {
            if (!PlayerPrefs.HasKey(TargetDisplayPrefsKey))
            {
                return;
            }

            ApplyTargetDisplay(PlayerPrefs.GetString(TargetDisplayPrefsKey, string.Empty));
        }

        public static void ApplyTargetDisplay(string value)
        {
            if (!TryResolveDisplay(value, out DisplayInfo display))
            {
                return;
            }

            MoveMainWindowToDisplay(display);
        }

        public static bool TryResolveDisplay(string value, out DisplayInfo display)
        {
            display = default;
            if (!TryParseDisplayValue(value, out int displayIndex))
            {
                return false;
            }

            List<DisplayInfo> displays = new();
            GetDisplayLayout(displays);
            if (displayIndex < 0 || displayIndex >= displays.Count)
            {
                return false;
            }

            display = displays[displayIndex];
            return true;
        }

        public static void GetDisplayLayout(List<DisplayInfo> displays)
        {
            if (displays == null)
            {
                return;
            }

            displays.Clear();

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            try
            {
                Screen.GetDisplayLayout(displays);
            }
            catch (System.Exception)
            {
                displays.Clear();
            }
#endif

            if (displays.Count <= 0)
            {
                displays.Add(Screen.mainWindowDisplayInfo);
            }
        }

        public static string ResolveCurrentDisplayValue()
        {
            List<DisplayInfo> displays = new();
            GetDisplayLayout(displays);
            DisplayInfo currentDisplay = Screen.mainWindowDisplayInfo;
            for (int i = 0; i < displays.Count; i++)
            {
                if (DisplayMatches(displays[i], currentDisplay))
                {
                    return FormatDisplayValue(i);
                }
            }

            return FormatDisplayValue(0);
        }

        public static bool TryParseDisplayValue(string value, out int displayIndex)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out displayIndex))
            {
                return displayIndex >= 0;
            }

            displayIndex = -1;
            return false;
        }

        public static string FormatDisplayValue(int displayIndex)
        {
            return Mathf.Max(0, displayIndex).ToString(CultureInfo.InvariantCulture);
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

        private static void MoveMainWindowToDisplay(DisplayInfo display)
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            Screen.MoveMainWindowTo(display, Vector2Int.zero);
#endif
        }

        private static bool DisplayMatches(DisplayInfo left, DisplayInfo right)
        {
            return string.Equals(left.name, right.name, System.StringComparison.Ordinal)
                && left.width == right.width
                && left.height == right.height
                && left.workArea.x == right.workArea.x
                && left.workArea.y == right.workArea.y
                && left.workArea.width == right.workArea.width
                && left.workArea.height == right.workArea.height;
        }
    }
}
