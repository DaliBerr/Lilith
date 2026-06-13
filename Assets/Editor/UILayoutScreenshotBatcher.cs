using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Kernel.UI;
using Vocalith.UI;
using Object = UnityEngine.Object;

public static class UILayoutScreenshotBatcher
{
    private const string MenuRoot = "Tools/Lilith/UI";
    private const string OutputArg = "-layoutScreenshotOutput";
    private const string PrefabsArg = "-layoutScreenshotPrefabs";
    private const string TargetSetArg = "-layoutScreenshotSet";
    private const string QuitArg = "-layoutScreenshotQuit";
    private const string ScreenIgnoreLabel = "ui-layout-ignore";
    private const string DefaultOutputFolderName = "UILayoutScreenshotBatches";
    private const float AuditCanvasMatchWidthOrHeight = 0.5f;
    private const float LogicalSizeAutoFixTolerance = 2f;
    private const int SortingOrder = 32767;
    private const int WarmupFrames = 5;
    private const int StableFileFrames = 2;
    private const int MaxCaptureWaitFrames = 240;
    private const int ContactCellWidth = 420;
    private static readonly Color CaptureCameraBackground = new(0.36f, 0.36f, 0.36f, 1f);
    private static readonly string[] AutoScreenPrefabSearchRoots = { "Assets/Prefabs/UI" };
    private static readonly Vector2 AuditReferenceResolution = new(1920f, 1080f);
    private static readonly Vector2 FallbackRuntimeChildSize = new(640f, 220f);
    private const float RuntimeChildPreviewWidthRatio = 0.42f;
    private const float RuntimeChildPreviewHeightRatio = 0.5f;
    private const float RuntimeChildMaxPreviewScale = 12f;

    private static readonly PrefabTarget[] BackPackScreenPrefabTargets =
    {
        new("BackPackUI", "Assets/Prefabs/UI/Backpack/BackPackUI.prefab", TargetLayoutMode.Screen, "screen-backpack"),
    };

    private static readonly PrefabTarget[] MainUIScreenPrefabTargets =
    {
        new("MainUI", "Assets/Prefabs/UI/MainHUD/MainUI.prefab", TargetLayoutMode.Screen, "screen-main-ui"),
    };

    private static readonly PrefabTarget[] NarrativeScreenPrefabTargets =
    {
        new("Dialog_UI", "Assets/Prefabs/UI/Narrative/Dialog UI.prefab", TargetLayoutMode.Screen, "screen-narrative-dialog"),
        new("Narrative_Content_Panel", "Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab", TargetLayoutMode.Screen, "screen-narrative-content"),
        new("Narrative_Menu_Panel", "Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab", TargetLayoutMode.Screen, "screen-narrative-menu"),
    };

    private static readonly PrefabTarget[] TokenSelectScreenPrefabTargets =
    {
        new("Token_Select_Panel", "Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab", TargetLayoutMode.Screen, "screen-token-select"),
    };

    private static readonly PrefabTarget[] RuntimeChildPrefabTargets =
    {
        new("BackPack_Grid_Prefab", "Assets/Prefabs/UI/Shared/BackPack Grid Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("BackPack_Attack_Preview_Rig", "Assets/Prefabs/UI/Backpack/BackPackAttackPreviewRig.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("BulletToken_Selection_Prefab", "Assets/Prefabs/UI/TokenSelect/BulletToken Selection Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Option_Entry", "Assets/Prefabs/UI/Options/Option Entry Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Option_Catalog_Button", "Assets/Prefabs/UI/Options/Option Catalog Button Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Pause_Panel_Setting_Button", "Assets/Prefabs/UI/System/Pause/Pause Panel Setting Button.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Story_Entry", "Assets/Prefabs/UI/Narrative/Story Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Chapter_Entry", "Assets/Prefabs/UI/Narrative/Chapter Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Hint_Entry", "Assets/Prefabs/UI/Hint/Hint Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Hint_Catalog_Entry", "Assets/Prefabs/UI/Hint/Hint Catalog Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Profile_Item", "Assets/Prefabs/UI/Profile/Profile item Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Quest_Entry", "Assets/Prefabs/UI/MainHUD/Quest Entry.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Health_Prefab", "Assets/Prefabs/UI/MainHUD/Health_Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Word_StrokeAnimation", "Assets/Prefabs/UI/MainHUD/Word_StrokeAnimation.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Upgrade_Node_Prefab", "Assets/Prefabs/UI/Upgrade/Upgrade Node Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Upgrade_Grid_Prefab", "Assets/Prefabs/UI/Upgrade/Upgrade Grid Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Upgrade_Section_Prefab", "Assets/Prefabs/UI/Upgrade/Upgrage Section Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Guide_Popup", "Assets/Prefabs/UI/Guide/Guide Popup.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("Shared_Button_Prefab", "Assets/Prefabs/UI/Shared/Button Prefab.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
        new("StartUp_Button", "Assets/Prefabs/UI/StartUp/Start up Button.prefab", TargetLayoutMode.RuntimeChild, "runtime-child"),
    };

    private static readonly Scenario[] DefaultScenarios =
    {
        new("01_1920x1080_ui1", new Vector2(1920f, 1080f), "screen 1920x1080, ui 1.0"),
        new("01_1920x1080_ui0p6", new Vector2(1920f, 1080f), "screen 1920x1080, ui 0.6"),
        new("01_1920x1080_ui1p5", new Vector2(1280f, 720f), "screen 1920x1080, ui 1.5"),


        new("02_2880x1800_16x10", new Vector2(1822f, 1138f), "screen 2880x1800, ui 1.0"),
        new("02_2880x1800_16x10_ui0p6", new Vector2(1822f, 1138f), "screen 2880x1800, ui 0.6"),
        new("02_2880x1800_16x10_ui1p5", new Vector2(1822f, 1138f), "screen 2880x1800, ui 1.5"),

        new("03_2560x1080_ultra", new Vector2(2217f, 935f), "screen 2560x1080, ui 1.0"),
        new("03_2560x1080_ultra_ui0p6", new Vector2(2217f, 935f), "screen 2560x1080, ui 0.6"),
        new("03_2560x1080_ultra_ui1p5", new Vector2(2217f, 935f), "screen 2560x1080, ui 1.5"),

        new("04_1280x1024_5x4", new Vector2(1610f, 1288f), "screen 1280x1024, ui 1.0"),
        new("04_1280x1024_5x4_ui0p6", new Vector2(1610f, 1288f), "screen 1280x1024, ui 0.6"),
        new("04_1280x1024_5x4_ui1p5", new Vector2(1610f, 1288f), "screen 1280x1024, ui 1.5"),
        
    };

    private static readonly Scenario[] UIScaleOneScenarios =
    {
        new("01_1920x1080_ui1", new Vector2(1920f, 1080f), "screen 1920x1080, ui 1.0"),
        new("02_2880x1800_16x10", new Vector2(1822f, 1138f), "screen 2880x1800, ui 1.0"),
        new("03_2560x1080_ultra", new Vector2(2217f, 935f), "screen 2560x1080, ui 1.0"),
        new("04_1280x1024_5x4", new Vector2(1610f, 1288f), "screen 1280x1024, ui 1.0"),
    };

    private static ScreenshotBatchJob activeJob;

    [MenuItem(MenuRoot + "/Capture Layout Screenshot Batch")]
    public static void CaptureDefaultSetFromMenu()
    {
        StartDefaultBatch(null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture Screen Prefabs Only")]
    public static void CaptureScreenSetFromMenu()
    {
        StartBatch(DiscoverScreenPrefabTargets(), DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture Screen Prefabs UI Scale 1.0 Only")]
    public static void CaptureScreenSetUIScaleOneFromMenu()
    {
        StartBatch(DiscoverScreenPrefabTargets(), UIScaleOneScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture BackPackUI Only")]
    public static void CaptureBackPackScreenSetFromMenu()
    {
        StartBatch(BackPackScreenPrefabTargets, DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture MainUI Only")]
    public static void CaptureMainUIScreenSetFromMenu()
    {
        StartBatch(MainUIScreenPrefabTargets, DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture Narrative Dialog Only")]
    public static void CaptureNarrativeScreenSetFromMenu()
    {
        StartBatch(NarrativeScreenPrefabTargets, DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture Token Select Panel Only")]
    public static void CaptureTokenSelectScreenSetFromMenu()
    {
        StartBatch(TokenSelectScreenPrefabTargets, DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Capture Runtime Child Prefabs")]
    public static void CaptureRuntimeChildSetFromMenu()
    {
        StartBatch(RuntimeChildPrefabTargets, DefaultScenarios, null, quitWhenDone: false);
    }

    [MenuItem(MenuRoot + "/Cancel Layout Screenshot Batch")]
    public static void CancelActiveBatch()
    {
        if (activeJob == null)
        {
            Debug.Log("[UILayoutScreenshotBatcher] No active screenshot batch to cancel.");
            return;
        }

        activeJob.Cancel();
    }

    public static void CaptureDefaultSetFromCommandLine()
    {
        string outputDirectory = ReadCommandLineValue(OutputArg);
        PrefabTarget[] targets = ReadCommandLinePrefabTargets();
        bool quitWhenDone = HasCommandLineArg(QuitArg);
        StartBatch(targets, DefaultScenarios, outputDirectory, quitWhenDone);
    }

    private static void StartDefaultBatch(string outputDirectory, bool quitWhenDone)
    {
        StartBatch(DiscoverScreenPrefabTargets(), DefaultScenarios, outputDirectory, quitWhenDone);
    }

    private static void StartBatch(
        IReadOnlyList<PrefabTarget> targets,
        IReadOnlyList<Scenario> scenarios,
        string outputDirectory,
        bool quitWhenDone)
    {
        if (Application.isBatchMode)
        {
            string message = "[UILayoutScreenshotBatcher] GameView screenshots require a visible Unity Editor. Do not run this tool in batchmode.";
            Debug.LogError(message);
            if (quitWhenDone)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        if (targets == null || targets.Count == 0)
        {
            string message = $"[UILayoutScreenshotBatcher] No screenshot targets resolved. Screen targets are auto-discovered from '{string.Join(", ", AutoScreenPrefabSearchRoots)}' when the prefab root has a UIScreen component and is not labeled '{ScreenIgnoreLabel}'.";
            Debug.LogError(message);
            if (quitWhenDone)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UILayoutScreenshotBatcher] Refusing to capture while PlayMode is active or changing.");
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogError("[UILayoutScreenshotBatcher] Wait until Unity finishes compiling/updating before starting a screenshot batch.");
            return;
        }

        if (activeJob != null)
        {
            Debug.LogWarning("[UILayoutScreenshotBatcher] A previous screenshot batch is still active. Cancelling it before starting a new one.");
            activeJob.Cancel();
        }

        string resolvedOutput = string.IsNullOrWhiteSpace(outputDirectory)
            ? CreateDefaultOutputDirectory()
            : Path.GetFullPath(outputDirectory);

        Directory.CreateDirectory(resolvedOutput);
        activeJob = new ScreenshotBatchJob(targets, scenarios, resolvedOutput, quitWhenDone);
        activeJob.Start();
    }

    private static string CreateDefaultOutputDirectory()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(projectRoot, DefaultOutputFolderName, "LilithUILayoutScreenshotBatch_" + stamp);
    }

    private static PrefabTarget[] DiscoverScreenPrefabTargets()
    {
        List<(string assetPath, string candidateLabel, string category)> discovered = new();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", AutoScreenPrefabSearchRoots))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || HasIgnoreLabel(prefab) || !TryGetAutoScreenCategory(prefab, assetPath, out string category))
            {
                continue;
            }

            discovered.Add((assetPath, BuildScreenTargetCandidateLabel(assetPath), category));
        }

        Dictionary<string, int> labelCounts = discovered
            .GroupBy(entry => entry.candidateLabel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return discovered
            .OrderBy(entry => entry.assetPath, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new PrefabTarget(
                labelCounts[entry.candidateLabel] == 1
                    ? entry.candidateLabel
                    : BuildUniqueScreenTargetLabel(entry.assetPath),
                entry.assetPath,
                TargetLayoutMode.Screen,
                entry.category))
            .ToArray();
    }

    private static bool TryGetAutoScreenCategory(GameObject prefab, string assetPath, out string category)
    {
        if (prefab.GetComponent<UIScreen>() != null)
        {
            category = "screen-auto-uiscreen";
            return true;
        }

        if (MatchesLegacyScreenHeuristic(prefab, assetPath))
        {
            category = "screen-auto-legacy";
            return true;
        }

        category = null;
        return false;
    }

    private static bool HasIgnoreLabel(GameObject prefab)
    {
        return AssetDatabase
            .GetLabels(prefab)
            .Any(label => string.Equals(label, ScreenIgnoreLabel, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesLegacyScreenHeuristic(GameObject prefab, string assetPath)
    {
        RectTransform rectTransform = prefab.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return false;
        }

        if (!Approximately(rectTransform.anchorMin, Vector2.zero) ||
            !Approximately(rectTransform.anchorMax, Vector2.one) ||
            rectTransform.sizeDelta.sqrMagnitude > 0.01f)
        {
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        return fileName.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("Panel", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("Popup", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("Screen", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Approximately(Vector2 left, Vector2 right)
    {
        return Mathf.Abs(left.x - right.x) <= 0.001f &&
               Mathf.Abs(left.y - right.y) <= 0.001f;
    }

    private static bool Approximately(Vector2 left, Vector2 right, float tolerance)
    {
        return Mathf.Abs(left.x - right.x) <= tolerance &&
               Mathf.Abs(left.y - right.y) <= tolerance;
    }

    private static bool TryParseScenarioScreenAndUIScale(string description, out Vector2 screenSize, out float uiScale)
    {
        screenSize = default;
        uiScale = 1f;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        int screenIndex = description.IndexOf("screen ", StringComparison.OrdinalIgnoreCase);
        if (screenIndex < 0)
        {
            return false;
        }

        string afterScreen = description[(screenIndex + "screen ".Length)..];
        int xIndex = afterScreen.IndexOf('x');
        int commaIndex = afterScreen.IndexOf(',');
        if (xIndex <= 0 || commaIndex <= xIndex + 1)
        {
            return false;
        }

        string widthText = afterScreen[..xIndex].Trim();
        string heightText = afterScreen[(xIndex + 1)..commaIndex].Trim();
        if (!float.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out float width) ||
            !float.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
        {
            return false;
        }

        string afterComma = afterScreen[(commaIndex + 1)..].Trim();
        int uiIndex = afterComma.IndexOf("ui ", StringComparison.OrdinalIgnoreCase);
        if (uiIndex < 0)
        {
            return false;
        }

        string uiText = afterComma[(uiIndex + "ui ".Length)..].Trim();
        if (!float.TryParse(uiText, NumberStyles.Float, CultureInfo.InvariantCulture, out uiScale) || uiScale <= 0f)
        {
            return false;
        }

        screenSize = new Vector2(width, height);
        return width > 0f && height > 0f;
    }

    private static float ComputeCanvasScaleFactor(Vector2 screenSize)
    {
        float widthScale = screenSize.x / AuditReferenceResolution.x;
        float heightScale = screenSize.y / AuditReferenceResolution.y;
        float logWidth = Mathf.Log(Mathf.Max(widthScale, 0.0001f), 2f);
        float logHeight = Mathf.Log(Mathf.Max(heightScale, 0.0001f), 2f);
        float weightedLogAverage = Mathf.Lerp(logWidth, logHeight, AuditCanvasMatchWidthOrHeight);
        return Mathf.Pow(2f, weightedLogAverage);
    }

    private static Vector2 ComputeLogicalSize(Vector2 screenSize, float uiScale)
    {
        float scaleFactor = ComputeCanvasScaleFactor(screenSize) * Mathf.Max(uiScale, 0.0001f);
        return screenSize / scaleFactor;
    }

    private static string BuildScreenTargetCandidateLabel(string assetPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (string.Equals(fileName, "Options", StringComparison.OrdinalIgnoreCase))
        {
            return "Options_Legacy";
        }

        return SanitizeFileName(fileName);
    }

    private static string BuildUniqueScreenTargetLabel(string assetPath)
    {
        const string screenRoot = "Assets/Prefabs/UI/";
        string relativePath = assetPath.StartsWith(screenRoot, StringComparison.OrdinalIgnoreCase)
            ? assetPath.Substring(screenRoot.Length)
            : assetPath;

        string withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
        return SanitizeFileName(withoutExtension.Replace('/', '_').Replace('\\', '_'));
    }

    private static PrefabTarget[] ReadCommandLinePrefabTargets()
    {
        string value = ReadCommandLineValue(PrefabsArg);
        if (string.IsNullOrWhiteSpace(value))
        {
            return ResolveTargetSet(ReadCommandLineValue(TargetSetArg));
        }

        return value
            .Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => path.Length > 0)
            .Select(path => new PrefabTarget(SanitizeFileName(Path.GetFileNameWithoutExtension(path)), path, InferCustomLayoutMode(path), "custom"))
            .ToArray();
    }

    private static TargetLayoutMode InferCustomLayoutMode(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab != null)
        {
            return prefab.GetComponent<UIScreen>() != null
                ? TargetLayoutMode.Screen
                : TargetLayoutMode.RuntimeChild;
        }

        if (RuntimeChildPrefabTargets.Any(target => string.Equals(target.AssetPath, assetPath, StringComparison.OrdinalIgnoreCase)))
        {
            return TargetLayoutMode.RuntimeChild;
        }

        return TargetLayoutMode.Screen;
    }

    private static PrefabTarget[] ResolveTargetSet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DiscoverScreenPrefabTargets();
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" or "default" or "full" => DiscoverScreenPrefabTargets(),
            "screen" or "screens" => DiscoverScreenPrefabTargets(),
            "backpack" or "backpack-ui" or "backpackui" => BackPackScreenPrefabTargets,
            "main-ui" or "mainui" or "main-hud" or "mainhud" => MainUIScreenPrefabTargets,
            "narrative" or "dialog" or "narrative-dialog" or "narrative-ui" => NarrativeScreenPrefabTargets,
            "token-select" or "tokenselect" or "token-select-panel" or "tokenselectpanel" => TokenSelectScreenPrefabTargets,
            "child" or "children" or "runtime-child" or "runtime-children" => RuntimeChildPrefabTargets,
            _ => WarnAndReturnDefaultTargetSet(value),
        };
    }

    private static PrefabTarget[] WarnAndReturnDefaultTargetSet(string value)
    {
        Debug.LogWarning($"[UILayoutScreenshotBatcher] Unknown {TargetSetArg} value '{value}'. Falling back to the default screen audit.");
        return DiscoverScreenPrefabTargets();
    }

    private static string ReadCommandLineValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasCommandLineArg(string key)
    {
        return Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, key, StringComparison.OrdinalIgnoreCase));
    }

    private static void NormalizeToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static void CenterRuntimeChild(RectTransform rectTransform)
    {
        Vector2 size = ResolveRuntimeChildSize(rectTransform);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static void ScaleRuntimeChildForPreview(RectTransform rectTransform, Vector2 logicalSize)
    {
        if (rectTransform == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        Canvas.ForceUpdateCanvases();

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rectTransform);
        Vector2 currentSize = bounds.size;
        if (currentSize.x < 1f || currentSize.y < 1f)
        {
            currentSize = ResolveRuntimeChildSize(rectTransform);
        }

        float maxWidth = logicalSize.x * RuntimeChildPreviewWidthRatio;
        float maxHeight = logicalSize.y * RuntimeChildPreviewHeightRatio;
        float previewScale = Mathf.Min(maxWidth / currentSize.x, maxHeight / currentSize.y);
        if (!float.IsFinite(previewScale))
        {
            previewScale = 1f;
        }

        rectTransform.localScale = Vector3.one * Mathf.Clamp(previewScale, 1f, RuntimeChildMaxPreviewScale);
    }

    private static Vector2 ResolveRuntimeChildSize(RectTransform rectTransform)
    {
        Vector2 size = rectTransform.sizeDelta;
        if (size.x < 16f || size.y < 16f)
        {
            float preferredWidth = LayoutUtility.GetPreferredWidth(rectTransform);
            float preferredHeight = LayoutUtility.GetPreferredHeight(rectTransform);
            if (preferredWidth > 16f)
            {
                size.x = preferredWidth;
            }

            if (preferredHeight > 16f)
            {
                size.y = preferredHeight;
            }
        }

        if (size.x < 16f)
        {
            size.x = FallbackRuntimeChildSize.x;
        }

        if (size.y < 16f)
        {
            size.y = FallbackRuntimeChildSize.y;
        }

        return size;
    }

    private static void AddVirtualRootBackground(RectTransform virtualRoot)
    {
        GameObject backgroundObject = new("__VirtualRootBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(virtualRoot, false);
        backgroundObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        NormalizeToParent(backgroundRect);
        backgroundObject.transform.SetAsFirstSibling();

        Image image = backgroundObject.GetComponent<Image>();
        image.color = new Color(0.025f, 0.027f, 0.034f, 0.82f);
        image.raycastTarget = false;
    }

    private static void SetDontSaveFlags(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            child.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }
    }

    private static void TryHydrateRuntimeContent(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        UIScreen screen = instance.GetComponent<UIScreen>();
        if (screen == null)
        {
            return;
        }

        MethodInfo initMethod = typeof(UIScreen).GetMethod("__Init", BindingFlags.Instance | BindingFlags.NonPublic);
        if (initMethod == null)
        {
            return;
        }

        try
        {
            initMethod.Invoke(screen, new object[] { null });
            screen.setAlpha(1f);
            CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            TryApplyScreenshotAuditSampleContent(screen);
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogWarning($"[UILayoutScreenshotBatcher] Runtime hydration failed for '{instance.name}': {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UILayoutScreenshotBatcher] Runtime hydration failed for '{instance.name}': {ex.Message}");
        }
    }

    private static void TryAttachScreenComponentForCapture(GameObject instance, string assetPath)
    {
        if (instance == null || instance.GetComponent<UIScreen>() != null)
        {
            return;
        }

        Type screenType = ResolveScreenTypeForPrefabAssetPath(assetPath);
        if (screenType == null)
        {
            return;
        }

        instance.AddComponent(screenType);
    }

    private static Type ResolveScreenTypeForPrefabAssetPath(string assetPath)
    {
        string normalizedAssetPath = NormalizePrefabAddress(assetPath);
        if (string.IsNullOrEmpty(normalizedAssetPath))
        {
            return null;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (type == null ||
                    type.IsAbstract ||
                    !typeof(UIScreen).IsAssignableFrom(type))
                {
                    continue;
                }

                UIPrefabAttribute attribute = type.GetCustomAttribute<UIPrefabAttribute>();
                if (attribute == null || NormalizePrefabAddress(attribute.Path) != normalizedAssetPath)
                {
                    continue;
                }

                return type;
            }
        }

        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null);
        }
    }

    private static string NormalizePrefabAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().Replace('\\', '/');
        if (normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".prefab".Length];
        }

        return normalized.ToLowerInvariant();
    }

    private static void TryApplyScreenshotAuditSampleContent(UIScreen screen)
    {
        try
        {
            switch (screen)
            {
                case MainUIScreen mainScreen:
                    ApplyMainUIAuditSample(mainScreen);
                    break;
                case DialogUIScreen dialogScreen:
                    ApplyDialogAuditSample(dialogScreen);
                    break;
                case NarrativeContentUIScreen narrativeContentScreen:
                    ApplyNarrativeAuditSample(narrativeContentScreen);
                    break;
                case OptionsUIScreen optionsScreen:
                    ApplyOptionsAuditSample(optionsScreen);
                    break;
                case HintUIScreen hintScreen:
                    ApplyHintAuditSample(hintScreen);
                    break;
                case BossInfoUIScreen bossInfoScreen:
                    ApplyBossInfoAuditSample(bossInfoScreen);
                    break;
                case PauseUIScreen pauseScreen:
                    ApplyPauseAuditSample(pauseScreen);
                    break;
            }
        }
        catch (TargetInvocationException ex)
        {
            Debug.LogWarning($"[UILayoutScreenshotBatcher] Audit sample content failed for '{screen.name}': {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UILayoutScreenshotBatcher] Audit sample content failed for '{screen.name}': {ex.Message}");
        }
    }

    private static void ApplyMainUIAuditSample(MainUIScreen screen)
    {
        if (screen.QuestPanel != null)
        {
            screen.QuestPanel.gameObject.SetActive(true);
        }

        if (screen.QuestListRoot != null)
        {
            GameObject questEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MainHUD/Quest Entry.prefab");
            if (questEntryPrefab == null)
            {
                Debug.LogWarning("[UILayoutScreenshotBatcher] MainUI audit sample skipped quest entries: quest entry prefab was not found.");
            }
            else
            {
                string[] samples =
                {
                    "主线：确认任务面板在 5:4 / 高 UI 缩放下不会贴边裁切",
                    "支线：长任务文本使用 TMP ellipsis，而不是全局线性 fitter",
                };

                for (int i = 0; i < samples.Length; i++)
                {
                    GameObject entryObject = PrefabUtility.InstantiatePrefab(questEntryPrefab) as GameObject;
                    if (entryObject == null)
                    {
                        entryObject = Object.Instantiate(questEntryPrefab);
                    }

                    entryObject.name = $"Screenshot Quest Entry {i + 1:00}";
                    entryObject.hideFlags = HideFlags.DontSave;
                    entryObject.transform.SetParent(screen.QuestListRoot, false);

                    QuestEntryView entryView = entryObject.GetComponent<QuestEntryView>();
                    if (entryView != null)
                    {
                        entryView.SetText(samples[i]);
                    }
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(screen.QuestListRoot);
            }
        }

        MethodInfo showRewardNotification = typeof(MainUIScreen).GetMethod("ShowRewardNotification", BindingFlags.Instance | BindingFlags.NonPublic);
        if (showRewardNotification != null)
        {
            showRewardNotification.Invoke(screen, new object[]
            {
                "获得奖励",
                "截图审查样例：通知面板使用本地锚点和 TMP 自动字号。",
            });
        }

        if (screen.NotificationPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(screen.NotificationPanel);
        }
    }

    private static void ApplyBossInfoAuditSample(BossInfoUIScreen screen)
    {
        SetPrivateField(screen, "transitionDuration", 0f);
        SetPrivateField(screen, "phaseTransitionPulseDuration", 0f);

        GameObject bossObject = new("Screenshot Boss Audit Data");
        bossObject.hideFlags = HideFlags.DontSave;
        bossObject.transform.SetParent(screen.transform, false);

        BaseCharEnemyNorm1 boss = bossObject.AddComponent<BaseCharEnemyNorm1>();
        SetPrivateField(boss, "health", 120f);
        SetPrivateField(boss, "currentHealth", 84f);
        SetPrivateField(boss, "hasInitializedHealth", true);

        InvokePrivateMethod(screen, "HandleBossEncounterStarted", new BossEncounterStartedEvent(
            boss,
            "霜锋·审查样例",
            boss.CurrentHealth,
            boss.MaxHealth));
        InvokePrivateMethod(screen, "HandleBossPhaseChanged", new BossPhaseChangedEvent(
            boss,
            2,
            "霜锋·贰阶段"));
        InvokePrivateMethod(screen, "OnDestroy");

        RectTransform root = screen.transform as RectTransform;
        if (root != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }
    }

    private static void ApplyPauseAuditSample(PauseUIScreen screen)
    {
        if (screen.SettingPanel != null)
        {
            screen.SettingPanel.SetActive(true);
        }

        if (screen.EmbeddedOptionsScreen != null)
        {
            ApplyOptionsAuditSample(screen.EmbeddedOptionsScreen);
        }

        RectTransform root = screen.transform as RectTransform;
        if (root != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }
    }

    private static void ApplyDialogAuditSample(DialogUIScreen screen)
    {
        MethodInfo method = typeof(DialogUIScreen).GetMethod("ApplyStorySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            return;
        }

        method.Invoke(screen, new object[]
        {
            new StorySequenceSnapshot(
                "audit_lilith",
                "莉莉丝",
                "截图审查样例：这段对白用于确认 Dialog UI 在不同分辨率和 UI 缩放下仍然依靠 TMP 实际排版分页，而不是被全局布局脚本压缩。",
                int.MaxValue,
                0,
                1,
                true,
                false),
        });
    }

    private static void ApplyNarrativeAuditSample(NarrativeContentUIScreen screen)
    {
        GameObject chapterEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Chapter Entry.prefab");
        if (chapterEntryPrefab == null)
        {
            Debug.LogWarning("[UILayoutScreenshotBatcher] Narrative audit sample skipped chapter entries: chapter entry prefab was not found.");
        }
        else
        {
            SetPrivateField(screen, "chapterEntryPrefab", chapterEntryPrefab);
        }

        screen.Configure(CreateNarrativeAuditSampleEntry());
    }

    private static NarrativeEntryData CreateNarrativeAuditSampleEntry()
    {
        NarrativeEntryData entry = new()
        {
            Id = "layout_audit_entry",
            Title = "布局审查样例",
            ShowStartBattleOnLastChapter = false,
            Chapters = new List<NarrativeChapterData>
            {
                new()
                {
                    Id = "layout_audit_chapter",
                    Title = "正文分页",
                    Pages = new List<string>
                    {
                        "左页样例：阅读器正文在 Phase 2 中不使用全局线性 fitter。这里保留足够长度，用来观察固定文本区、行距和翻页按钮在多分辨率截图中的关系。",
                        "右页样例：如果文字仍然显得拥挤，后续只能调整 TMP、分页、滚动或明确的 LayoutElement 高度，而不是恢复 Horizontal/Vertical Layout 的全局缩放。",
                    },
                },
            },
        };

        return entry;
    }

    private static void ApplyOptionsAuditSample(OptionsUIScreen screen)
    {
        GameObject entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Options/Option Entry Entry.prefab");
        if (entryPrefab == null)
        {
            Debug.LogWarning("[UILayoutScreenshotBatcher] Options audit sample skipped: option entry prefab was not found.");
            return;
        }

        SetPrivateField(screen, "entryPrefab", entryPrefab);
        SetPrivateField(screen, "hasLoadedCatalog", true);
        SetPrivateField(screen, "catalog", CreateOptionsAuditSampleCatalog());

        MethodInfo rebuildView = typeof(OptionsUIScreen).GetMethod("RebuildView", BindingFlags.Instance | BindingFlags.NonPublic);
        rebuildView?.Invoke(screen, null);
    }

    private static void ApplyHintAuditSample(HintUIScreen screen)
    {
        GameObject hintEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Hint/Hint Entry.prefab");
        GameObject hintCatalogEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Hint/Hint Catalog Entry.prefab");
        if (hintEntryPrefab == null || hintCatalogEntryPrefab == null)
        {
            Debug.LogWarning("[UILayoutScreenshotBatcher] Hint audit sample skipped: hint entry prefabs were not found.");
            return;
        }

        SetPrivateField(screen, "hintEntryPrefab", hintEntryPrefab);
        SetPrivateField(screen, "hintCatalogEntryPrefab", hintCatalogEntryPrefab);
        SetPrivateField(screen, "manualCatalog", CreateHintAuditSampleCatalog());
        SetPrivateField(screen, "hasLoadedManualCatalog", true);

        MethodInfo rebuildView = typeof(HintUIScreen).GetMethod("RebuildView", BindingFlags.Instance | BindingFlags.NonPublic);
        rebuildView?.Invoke(screen, null);

        ApplyHintAuditContent(screen);
    }

    private static void ApplyHintAuditContent(HintUIScreen screen)
    {
        FieldInfo mainContentTextField = typeof(HintUIScreen).GetField("mainContentText", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mainContentTextField?.GetValue(screen) is not TMP_Text mainContentText)
        {
            return;
        }

        mainContentText.text = "布局审查\n\n这是一段用于截图审查的 Hint 正文。它会验证顶部分类、左侧条目列表和右侧滚动正文在不同 UI 缩放下是否仍保持可读，而不是依赖全局 H/V fitter 压缩。";
    }

    private static HintCatalogData CreateHintAuditSampleCatalog()
    {
        return new HintCatalogData
        {
            Categories = new List<HintCategoryData>
            {
                new()
                {
                    Id = "layout_basics",
                    Title = "帮助",
                    Entries = new List<HintEntryData>
                    {
                        new()
                        {
                            Id = "layout_overview",
                            Title = "布局审查",
                            Content = "这是一段用于截图审查的 Hint 正文。它会验证顶部分类、左侧条目列表和右侧滚动正文在不同 UI 缩放下是否仍保持可读，而不是依赖全局 H/V fitter 压缩。",
                        },
                        new()
                        {
                            Id = "layout_long_title",
                            Title = "很长的条目标题会省略",
                            Content = "条目标题使用 NoWrap + Ellipsis；正文区域继续依赖 ScrollRect、TMP 自动字号和固定 padding。",
                        },
                    },
                },
                new()
                {
                    Id = "enemy_notes",
                    Title = "图鉴",
                    Entries = new List<HintEntryData>
                    {
                        new()
                        {
                            Id = "enemy_sample",
                            Title = "示例敌人",
                            Content = "图鉴条目同样走左侧列表和右侧正文，不新增 ResponsiveLayoutGroupFitter。",
                        },
                    },
                },
            },
        };
    }

    private static OptionsCatalogData CreateOptionsAuditSampleCatalog()
    {
        return new OptionsCatalogData
        {
            Categories = new List<OptionsCategoryData>
            {
                new()
                {
                    Id = "layout_display",
                    Title = "显示",
                    Entries = new List<OptionsEntryData>
                    {
                        CreateDropdownEntry(
                            "audit_resolution",
                            "分辨率",
                            "Options.Audit.Resolution",
                            "res_1920x1080",
                            ("res_1280x720", "1280 x 720"),
                            ("res_1920x1080", "1920 x 1080"),
                            ("res_2560x1080", "2560 x 1080")),
                        new()
                        {
                            Id = "audit_fullscreen",
                            Title = "全屏模式",
                            Mode = "toggle",
                            PlayerPrefsKey = "Options.Audit.Fullscreen",
                            DefaultBool = true,
                        },
                    },
                },
                new()
                {
                    Id = "layout_audio",
                    Title = "音频",
                    Entries = new List<OptionsEntryData>
                    {
                        new()
                        {
                            Id = "audit_master_volume",
                            Title = "主音量",
                            Mode = "slider",
                            PlayerPrefsKey = "Options.Audit.MasterVolume",
                            Min = 0f,
                            Max = 100f,
                            DefaultValue = 80f,
                            WholeNumbers = true,
                            ValueFormat = "{0:0}%",
                        },
                        new()
                        {
                            Id = "audit_music_volume",
                            Title = "音乐音量",
                            Mode = "slider",
                            PlayerPrefsKey = "Options.Audit.MusicVolume",
                            Min = 0f,
                            Max = 100f,
                            DefaultValue = 65f,
                            WholeNumbers = true,
                            ValueFormat = "{0:0}%",
                        },
                    },
                },
            },
        };
    }

    private static OptionsEntryData CreateDropdownEntry(
        string id,
        string title,
        string prefsKey,
        string defaultOptionId,
        params (string Id, string Title)[] choices)
    {
        return new OptionsEntryData
        {
            Id = id,
            Title = title,
            Mode = "dropdown",
            PlayerPrefsKey = prefsKey,
            DefaultOptionId = defaultOptionId,
            Options = choices
                .Select(choice => new OptionsChoiceData
                {
                    Id = choice.Id,
                    Title = choice.Title,
                    Value = choice.Id,
                })
                .ToList(),
        };
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, args);
    }

    private static void ApplyResponsiveLayoutsForCapture(RectTransform captureScope, GameObject instance, TargetLayoutMode layoutMode)
    {
        if (captureScope == null || instance == null)
        {
            return;
        }

        RectTransform instanceRect = instance.transform as RectTransform;
        if (instanceRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(instanceRect);

        ApplyTopLevelResponsiveLayoutFitters(instance.transform);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(instanceRect);
        ApplyTokenSelectPanelLayoutFitters(instance.transform);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(instanceRect);
        ApplyTokenSelectPanelLayoutFitters(instance.transform);
        Canvas.ForceUpdateCanvases();
    }

    private static void ApplyTokenSelectPanelLayoutFitters(Transform root)
    {
        if (root == null)
        {
            return;
        }

        TokenSelectPanelLayoutFitter[] fitters = root.GetComponentsInChildren<TokenSelectPanelLayoutFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
        {
            fitters[i]?.FitNow();
        }
    }

    private static void ApplyTopLevelResponsiveLayoutFitters(Transform root)
    {
        if (root == null)
        {
            return;
        }

        ResponsiveLayoutGroupFitter[] fitters = root.GetComponentsInChildren<ResponsiveLayoutGroupFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
        {
            ResponsiveLayoutGroupFitter fitter = fitters[i];
            if (fitter == null || !IsTopLevelResponsiveLayoutFitter(fitter.transform, root))
            {
                continue;
            }

            fitter.FitNow();
        }
    }

    private static bool IsTopLevelResponsiveLayoutFitter(Transform candidate, Transform subtreeRoot)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform current = candidate.parent;
        while (current != null && current != subtreeRoot.parent)
        {
            if (current != candidate && current.GetComponent<ResponsiveLayoutGroupFitter>() != null)
            {
                return false;
            }

            if (current == subtreeRoot)
            {
                break;
            }

            current = current.parent;
        }

        return true;
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return sanitized.Replace(' ', '_');
    }

    private readonly struct PrefabTarget
    {
        public readonly string Label;
        public readonly string AssetPath;
        public readonly TargetLayoutMode LayoutMode;
        public readonly string Category;

        public PrefabTarget(string label, string assetPath, TargetLayoutMode layoutMode, string category)
        {
            Label = label;
            AssetPath = assetPath;
            LayoutMode = layoutMode;
            Category = category;
        }
    }

    private enum TargetLayoutMode
    {
        Screen,
        RuntimeChild,
    }

    private readonly struct Scenario
    {
        public readonly string Id;
        public readonly Vector2 LogicalSize;
        public readonly string Description;

        public Scenario(string id, Vector2 logicalSize, string description)
        {
            Id = id;
            LogicalSize = logicalSize;
            Description = description;
        }

        public Vector2 ResolveLogicalSize()
        {
            if (!TryParseScenarioScreenAndUIScale(Description, out Vector2 screenSize, out float uiScale))
            {
                return LogicalSize;
            }

            if (Mathf.Abs(uiScale - 1f) <= 0.001f)
            {
                return LogicalSize;
            }

            Vector2 baseLogicalSize = ComputeLogicalSize(screenSize, 1f);
            if (!Approximately(LogicalSize, baseLogicalSize, LogicalSizeAutoFixTolerance))
            {
                return LogicalSize;
            }

            return ComputeLogicalSize(screenSize, uiScale);
        }
    }

    private sealed class CaptureResult
    {
        public int TargetIndex;
        public int ScenarioIndex;
        public string Path;
    }

    private sealed class ScreenshotBatchJob
    {
        private readonly IReadOnlyList<PrefabTarget> targets;
        private readonly IReadOnlyList<Scenario> scenarios;
        private readonly List<CaptureResult> results = new();
        private readonly string outputDirectory;
        private readonly bool quitWhenDone;

        private GameObject captureRoot;
        private Camera captureCamera;
        private Vector2Int currentCaptureSize;
        private int targetIndex;
        private int scenarioIndex;
        private int warmupFramesLeft;
        private int captureWaitFrames;
        private int stableFrames;
        private string pendingPath;
        private bool captureRequested;

        public ScreenshotBatchJob(
            IReadOnlyList<PrefabTarget> targets,
            IReadOnlyList<Scenario> scenarios,
            string outputDirectory,
            bool quitWhenDone)
        {
            this.targets = targets;
            this.scenarios = scenarios;
            this.outputDirectory = outputDirectory;
            this.quitWhenDone = quitWhenDone;
        }

        public void Start()
        {
            WriteTargetManifest();
            EditorApplication.update += Tick;
            Debug.Log($"[UILayoutScreenshotBatcher] Started {targets.Count * scenarios.Count} captures. Output: {outputDirectory}");
        }

        public void Cancel()
        {
            EditorApplication.update -= Tick;
            ClearCaptureRoot();
            if (activeJob == this)
            {
                activeJob = null;
            }

            Debug.Log("[UILayoutScreenshotBatcher] Screenshot batch cancelled.");
        }

        private void Tick()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("PlayMode started while the screenshot batch was running.");
                }

                if (pendingPath != null)
                {
                    TickPendingCapture();
                    return;
                }

                if (warmupFramesLeft > 0)
                {
                    warmupFramesLeft--;
                    RepaintGameView();
                    Canvas.ForceUpdateCanvases();
                    return;
                }

                if (captureRequested)
                {
                    RequestScreenshot();
                    return;
                }

                if (targetIndex >= targets.Count)
                {
                    Complete();
                    return;
                }

                SetupCurrentCapture();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        private void SetupCurrentCapture()
        {
            ClearCaptureRoot();

            PrefabTarget target = targets[targetIndex];
            Scenario scenario = scenarios[scenarioIndex];
            Vector2 logicalSize = scenario.ResolveLogicalSize();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.AssetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Could not load UI prefab at {target.AssetPath}");
            }

            Vector2 gameViewSize = ResolveGameViewSize();
            currentCaptureSize = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(gameViewSize.x)),
                Mathf.Max(1, Mathf.RoundToInt(gameViewSize.y)));
            captureCamera = CreateCaptureCamera(currentCaptureSize);

            captureRoot = new GameObject("__LilithUILayoutScreenshotRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            captureRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            Canvas canvas = captureRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = captureCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = SortingOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = captureRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            RectTransform canvasRect = captureRoot.GetComponent<RectTransform>();
            NormalizeToParent(canvasRect);

            GameObject virtualRootObject = new("__VirtualRoot", typeof(RectTransform));
            virtualRootObject.transform.SetParent(captureRoot.transform, false);
            virtualRootObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            RectTransform virtualRoot = virtualRootObject.GetComponent<RectTransform>();
            virtualRoot.anchorMin = new Vector2(0.5f, 0.5f);
            virtualRoot.anchorMax = new Vector2(0.5f, 0.5f);
            virtualRoot.pivot = new Vector2(0.5f, 0.5f);
            virtualRoot.anchoredPosition = Vector2.zero;
            virtualRoot.sizeDelta = logicalSize;
            AddVirtualRootBackground(virtualRoot);

            float fitScale = Mathf.Min(gameViewSize.x / logicalSize.x, gameViewSize.y / logicalSize.y);
            virtualRoot.localScale = new Vector3(fitScale, fitScale, 1f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, virtualRoot);
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, virtualRoot);
            }

            instance.name = target.Label;
            SetDontSaveFlags(instance);

            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            if (instanceRect != null)
            {
                if (target.LayoutMode == TargetLayoutMode.RuntimeChild)
                {
                    CenterRuntimeChild(instanceRect);
                    ScaleRuntimeChildForPreview(instanceRect, logicalSize);
                }
                else
                {
                    UIScreen screen = instance.GetComponent<UIScreen>();
                    if (screen == null || !screen.PreservePrefabRootRectTransform)
                    {
                        NormalizeToParent(instanceRect);
                    }
                }
            }

            if (target.LayoutMode == TargetLayoutMode.Screen)
            {
                TryAttachScreenComponentForCapture(instance, target.AssetPath);
                TryHydrateRuntimeContent(instance);
            }

            ApplyResponsiveLayoutsForCapture(virtualRoot, instance, target.LayoutMode);

            AddLabel(target, scenario, logicalSize, gameViewSize);
            SetDontSaveFlags(captureRoot);
            SetCaptureLayer(captureRoot);

            warmupFramesLeft = WarmupFrames;
            captureRequested = true;
            RepaintGameView(focus: true);
            Canvas.ForceUpdateCanvases();
        }

        private static Vector2 ResolveGameViewSize()
        {
            Vector2 gameViewSize = Handles.GetMainGameViewSize();
            if (gameViewSize.x <= 0f || gameViewSize.y <= 0f)
            {
                return new Vector2(1920f, 1080f);
            }

            return gameViewSize;
        }

        private static Camera CreateCaptureCamera(Vector2Int captureSize)
        {
            int captureLayer = GetCaptureLayer();
            GameObject cameraObject = new("__UILayoutScreenshotCamera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.layer = captureLayer;

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CaptureCameraBackground;
            camera.cullingMask = 1 << captureLayer;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1f, captureSize.y * 0.5f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static int GetCaptureLayer()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            return uiLayer >= 0 ? uiLayer : 0;
        }

        private static void SetCaptureLayer(GameObject root)
        {
            int captureLayer = GetCaptureLayer();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.layer = captureLayer;
            }
        }

        private void AddLabel(PrefabTarget target, Scenario scenario, Vector2 logicalSize, Vector2 gameViewSize)
        {
            GameObject labelBackground = new("__CaptureLabelBackground", typeof(RectTransform), typeof(Image));
            labelBackground.transform.SetParent(captureRoot.transform, false);
            labelBackground.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            RectTransform labelBackgroundRect = labelBackground.GetComponent<RectTransform>();
            labelBackgroundRect.anchorMin = new Vector2(0f, 1f);
            labelBackgroundRect.anchorMax = new Vector2(0f, 1f);
            labelBackgroundRect.pivot = new Vector2(0f, 1f);
            labelBackgroundRect.anchoredPosition = Vector2.zero;
            labelBackgroundRect.sizeDelta = new Vector2(Mathf.Min(760f, gameViewSize.x * 0.55f), 30f);

            Image image = labelBackground.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject labelObject = new("__CaptureLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(labelBackground.transform, false);
            labelObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            NormalizeToParent(labelRect);
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);

            TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
            text.text = $"{target.Label} | {scenario.Id} | logical {Mathf.RoundToInt(logicalSize.x)}x{Mathf.RoundToInt(logicalSize.y)} | {scenario.Description}";
            text.fontSize = 14f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        private void RequestScreenshot()
        {
            captureRequested = false;
            PrefabTarget target = targets[targetIndex];
            Scenario scenario = scenarios[scenarioIndex];
            string fileName = $"{targetIndex + 1:00}_{target.Label}__{scenario.Id}.png";
            pendingPath = Path.Combine(outputDirectory, fileName);
            captureWaitFrames = 0;
            stableFrames = 0;

            if (File.Exists(pendingPath))
            {
                File.Delete(pendingPath);
            }

            RepaintGameView(focus: true);
            Canvas.ForceUpdateCanvases();
            RenderCurrentCaptureToFile(pendingPath);
        }

        private void RenderCurrentCaptureToFile(string outputPath)
        {
            if (captureCamera == null)
            {
                throw new InvalidOperationException("Cannot render UI screenshot without a capture camera.");
            }

            RenderTexture renderTexture = RenderTexture.GetTemporary(
                currentCaptureSize.x,
                currentCaptureSize.y,
                24,
                RenderTextureFormat.ARGB32);
            RenderTexture previousRenderTexture = captureCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                captureCamera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                captureCamera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(currentCaptureSize.x, currentCaptureSize.y, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, currentCaptureSize.x, currentCaptureSize.y), 0, 0);
                texture.Apply();

                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                captureCamera.targetTexture = previousRenderTexture;
                RenderTexture.active = previousActive;
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private void TickPendingCapture()
        {
            captureWaitFrames++;
            RepaintGameView();

            if (File.Exists(pendingPath) && new FileInfo(pendingPath).Length > 0)
            {
                stableFrames++;
                if (stableFrames >= StableFileFrames)
                {
                    results.Add(new CaptureResult
                    {
                        TargetIndex = targetIndex,
                        ScenarioIndex = scenarioIndex,
                        Path = pendingPath,
                    });

                    Advance();
                    return;
                }
            }
            else
            {
                stableFrames = 0;
            }

            if (captureWaitFrames > MaxCaptureWaitFrames)
            {
                throw new TimeoutException($"Timed out waiting for screenshot file: {pendingPath}");
            }
        }

        private void Advance()
        {
            pendingPath = null;
            scenarioIndex++;
            if (scenarioIndex >= scenarios.Count)
            {
                scenarioIndex = 0;
                targetIndex++;
            }
        }

        private void Complete()
        {
            EditorApplication.update -= Tick;
            ClearCaptureRoot();
            GenerateContactSheets();
            activeJob = null;

            Debug.Log($"[UILayoutScreenshotBatcher] Completed {results.Count} captures. Output: {outputDirectory}");
            if (quitWhenDone)
            {
                EditorApplication.Exit(0);
            }
        }

        private void Fail(Exception ex)
        {
            EditorApplication.update -= Tick;
            ClearCaptureRoot();
            activeJob = null;
            Debug.LogError($"[UILayoutScreenshotBatcher] Failed: {ex}");
            if (quitWhenDone)
            {
                EditorApplication.Exit(1);
            }
        }

        private void ClearCaptureRoot()
        {
            if (captureRoot != null)
            {
                Object.DestroyImmediate(captureRoot);
                captureRoot = null;
            }

            if (captureCamera != null)
            {
                Object.DestroyImmediate(captureCamera.gameObject);
                captureCamera = null;
            }
        }

        private void GenerateContactSheets()
        {
            if (results.Count == 0)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                List<CaptureResult> prefabResults = results
                    .Where(result => result.TargetIndex == i)
                    .OrderBy(result => result.ScenarioIndex)
                    .ToList();
                if (prefabResults.Count > 0)
                {
                    string path = Path.Combine(outputDirectory, $"{i + 1:00}_{targets[i].Label}__CONTACT.png");
                    ContactSheetWriter.Write(prefabResults.Select(result => result.Path).ToArray(), scenarios.Count, path);
                }
            }

            string masterPath = Path.Combine(outputDirectory, "00_MASTER_CONTACT.png");
            ContactSheetWriter.Write(
                results.OrderBy(result => result.TargetIndex).ThenBy(result => result.ScenarioIndex).Select(result => result.Path).ToArray(),
                scenarios.Count,
                masterPath);
        }

        private static void RepaintGameView(bool focus = false)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                if (focus)
                {
                    gameView.Show();
                    gameView.Focus();
                }

                gameView.Repaint();
            }
        }

        private void WriteTargetManifest()
        {
            string manifestPath = Path.Combine(outputDirectory, "00_TARGETS.txt");
            List<string> lines = new()
            {
                "Lilith UI layout screenshot targets",
                "Target count: " + targets.Count.ToString(CultureInfo.InvariantCulture),
                "Scenario count: " + scenarios.Count.ToString(CultureInfo.InvariantCulture),
                string.Empty,
            };

            if (targets.Any(target => target.Category.StartsWith("screen-auto", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add("Auto screen discovery:");
                lines.Add("- root UIScreen prefab under Assets/Prefabs/UI");
                lines.Add("- legacy full-stretch UI/Panel/Popup/Screen prefab compatibility");
                lines.Add("Auto screen ignore label: " + ScreenIgnoreLabel);
                lines.Add(string.Empty);
            }

            lines.Add("Audit expectations:");
            lines.Add("- Runtime UI scale is fixed at 1.0; ui 1.0 scenarios are the canonical review baseline.");
            lines.Add("- On ultrawide screens, main Screen/Modal content using UIContentSafeFrame should be centered and capped to 16:9.");
            lines.Add("- Full-screen world, background, overlay, dim and toast layers should remain full-bleed outside the safe frame.");
            lines.Add(string.Empty);

            lines.Add("Scenarios:");

            for (int i = 0; i < scenarios.Count; i++)
            {
                Scenario scenario = scenarios[i];
                Vector2 logicalSize = scenario.ResolveLogicalSize();
                lines.Add($"{i + 1:00}. {scenario.Id} logical {Mathf.RoundToInt(logicalSize.x)}x{Mathf.RoundToInt(logicalSize.y)} - {scenario.Description}");
            }

            lines.Add(string.Empty);
            lines.Add("Targets:");
            for (int i = 0; i < targets.Count; i++)
            {
                PrefabTarget target = targets[i];
                lines.Add($"{i + 1:00}. [{target.Category}] [{target.LayoutMode}] {target.Label} -> {target.AssetPath}");
            }

            File.WriteAllLines(manifestPath, lines);
        }
    }

    private static class ContactSheetWriter
    {
        public static void Write(IReadOnlyList<string> imagePaths, int columns, string outputPath)
        {
            if (imagePaths.Count == 0)
            {
                return;
            }

            Texture2D first = LoadTexture(imagePaths[0]);
            try
            {
                int cellWidth = ContactCellWidth;
                int cellHeight = Mathf.Max(1, Mathf.RoundToInt(first.height * (cellWidth / (float)first.width)));
                int rows = Mathf.CeilToInt(imagePaths.Count / (float)columns);
                Texture2D sheet = new(columns * cellWidth, rows * cellHeight, TextureFormat.RGBA32, false);
                try
                {
                    Fill(sheet, new Color32(31, 32, 38, 255));
                    for (int i = 0; i < imagePaths.Count; i++)
                    {
                        Texture2D source = i == 0 ? first : LoadTexture(imagePaths[i]);
                        try
                        {
                            int column = i % columns;
                            int row = i / columns;
                            CopyScaled(source, sheet, column * cellWidth, (rows - row - 1) * cellHeight, cellWidth, cellHeight);
                        }
                        finally
                        {
                            if (i != 0)
                            {
                                Object.DestroyImmediate(source);
                            }
                        }
                    }

                    File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(sheet);
                }
            }
            finally
            {
                Object.DestroyImmediate(first);
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Object.DestroyImmediate(texture);
                throw new InvalidDataException($"Could not load screenshot image: {path}");
            }

            return texture;
        }

        private static void Fill(Texture2D texture, Color32 color)
        {
            Color32[] colors = Enumerable.Repeat(color, texture.width * texture.height).ToArray();
            texture.SetPixels32(colors);
        }

        private static void CopyScaled(Texture2D source, Texture2D target, int targetX, int targetY, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0f : y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0f : x / (float)(width - 1);
                    target.SetPixel(targetX + x, targetY + y, source.GetPixelBilinear(u, v));
                }
            }

            target.Apply(updateMipmaps: false);
        }
    }
}
