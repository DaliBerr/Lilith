using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Kernel.GameState;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// Options prefab 的运行时脚本：从 JSON 读取设置目录，并按配置动态生成设置项。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Options")]
    public sealed class OptionsUIScreen : GameUIScreen
    {
        private const string DefaultCatalogAddress = "Assets/Data/UI/OptionsCatalog";
        private const string DefaultEntryPrefabAddress = "Assets/Prefabs/UI/Option Entry Entry";
        private const string SliderMode = "slider";
        private const string ButtonMode = "button";
        private const string DropdownMode = "dropdown";
        private const string ToggleMode = "toggle";

        [Header("Layout")]
        [SerializeField] private RectTransform catalogRoot;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject catalogButtonTemplate;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject buttonPanel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button applyButton;

        [Header("Data")]
        [SerializeField] private string catalogAddress = DefaultCatalogAddress;
        [SerializeField] private string entryPrefabAddress = DefaultEntryPrefabAddress;

        [Header("State Colors")]
        [SerializeField] private Color normalCatalogColor = Color.white;
        [SerializeField] private Color selectedCatalogColor = new(0.86f, 0.93f, 1f, 1f);

        private readonly List<GameObject> runtimeCatalogObjects = new();
        private readonly List<GameObject> runtimeEntryObjects = new();
        private readonly List<Button> runtimeCatalogButtons = new();
        private readonly List<Image> runtimeCatalogImages = new();
        private readonly Dictionary<string, OptionsEntryRuntimeState> entryStates = new(StringComparer.Ordinal);

        private AsyncOperationHandle<GameObject> entryPrefabHandle;
        private bool hasEntryPrefabHandle;
        private GameObject entryPrefab;
        private OptionsCatalogData catalog = OptionsCatalogUtility.CreateDefault();
        private bool hasLoadedCatalog;
        private int activeCategoryIndex = -1;
        private bool isRefreshingControls;
        private bool isShowingUnsavedChangesPrompt;
        private bool isResolvingUnsavedChangesPrompt;
        private bool isClosingSelf;

        public override Status currentStatus { get; } = StatusList.PopUpStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            HideTemplates();
            BindButtons();
        }

        public override IEnumerator Show(float fade = 0.15f)
        {
            EnsureCurrentStatus();
            yield return EnsureAssetsLoadedCo();
            RebuildView();
            yield return base.Show(fade);
        }

        protected override void OnAfterHide()
        {
            ClearRuntimeView();
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            ClearRuntimeView();
            ReleaseEntryPrefabHandle();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        public void RequestClose()
        {
            if (ui == null)
            {
                return;
            }

            if (isClosingSelf)
            {
                return;
            }

            if (HasPendingChanges())
            {
                ShowUnsavedChangesPrompt();
                return;
            }

            StartCoroutine(CloseSelfCo());
        }

        private IEnumerator CloseSelfCo()
        {
            if (ui == null || isClosingSelf)
            {
                yield break;
            }

            isClosingSelf = true;
            try
            {
                while (ui.IsNavigating())
                {
                    yield return null;
                }

                if (ui.GetTopModal() == this)
                {
                    yield return ui.PopModalAndWait();
                    yield break;
                }

                if (ui.GetTopScreen() == this)
                {
                    yield return ui.PopScreenAndWait();
                }
            }
            finally
            {
                isClosingSelf = false;
            }
        }

        private void ShowUnsavedChangesPrompt()
        {
            if (ui == null || isShowingUnsavedChangesPrompt)
            {
                return;
            }

            if (ui.GetTopModal() is PopUpUIScreen popup)
            {
                ConfigureUnsavedChangesPopup(popup);
                return;
            }

            StartCoroutine(ShowUnsavedChangesPromptCo());
        }

        private IEnumerator ShowUnsavedChangesPromptCo()
        {
            isShowingUnsavedChangesPrompt = true;
            try
            {
                yield return ui.ShowModalAndWait<PopUpUIScreen>();
                if (ui.GetTopModal() is PopUpUIScreen popup)
                {
                    ConfigureUnsavedChangesPopup(popup);
                }
            }
            finally
            {
                isShowingUnsavedChangesPrompt = false;
            }
        }

        private void ConfigureUnsavedChangesPopup(PopUpUIScreen popup)
        {
            if (popup == null)
            {
                return;
            }

            popup.Configure(
                "当前设置尚未应用。要保存这些改动吗？",
                onConfirm: HandleUnsavedPromptSave,
                onClose: HandleUnsavedPromptDiscard,
                confirmLabel: "保存",
                closeLabel: "丢弃",
                shouldCloseAfterConfirm: false,
                shouldCloseAfterClose: false);
        }

        private void HandleUnsavedPromptSave()
        {
            if (ui == null)
            {
                return;
            }

            StartCoroutine(ResolveUnsavedPromptAndCloseCo(true));
        }

        private void HandleUnsavedPromptDiscard()
        {
            if (ui == null)
            {
                return;
            }

            StartCoroutine(ResolveUnsavedPromptAndCloseCo(false));
        }

        private IEnumerator ResolveUnsavedPromptAndCloseCo(bool shouldApply)
        {
            if (ui == null || isClosingSelf || isResolvingUnsavedChangesPrompt)
            {
                yield break;
            }

            isResolvingUnsavedChangesPrompt = true;
            try
            {
                if (ui.GetTopModal() is PopUpUIScreen)
                {
                    yield return ui.PopModalAndWait();
                }

                if (shouldApply)
                {
                    ApplyPendingChanges();
                }
                else
                {
                    DiscardPendingChanges();
                }

                yield return CloseSelfCo();
            }
            finally
            {
                isResolvingUnsavedChangesPrompt = false;
            }
        }

        private IEnumerator EnsureAssetsLoadedCo()
        {
            if (!hasEntryPrefabHandle)
            {
                string address = string.IsNullOrWhiteSpace(entryPrefabAddress)
                    ? DefaultEntryPrefabAddress
                    : entryPrefabAddress.Trim();
                entryPrefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                hasEntryPrefabHandle = true;
                yield return entryPrefabHandle;

                if (entryPrefabHandle.Status == AsyncOperationStatus.Succeeded && entryPrefabHandle.Result != null)
                {
                    entryPrefab = entryPrefabHandle.Result;
                }
                else
                {
                    GameDebug.LogWarning($"[OptionsUIScreen] Failed to load option entry prefab at '{address}'.");
                    ReleaseEntryPrefabHandle();
                }
            }

            if (!hasLoadedCatalog)
            {
                yield return LoadCatalogCo();
            }
        }

        private IEnumerator LoadCatalogCo()
        {
            hasLoadedCatalog = true;
            catalog = OptionsCatalogUtility.CreateDefault();

            string address = string.IsNullOrWhiteSpace(catalogAddress)
                ? DefaultCatalogAddress
                : catalogAddress.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                yield break;
            }

            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            yield return handle;
            try
            {
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    GameDebug.LogWarning($"[OptionsUIScreen] Failed to load options catalog JSON at '{address}'.");
                    yield break;
                }

                if (!OptionsCatalogUtility.TryDeserializeCatalogJson(handle.Result.text, out OptionsCatalogData parsedCatalog, out string errorMessage))
                {
                    GameDebug.LogWarning($"[OptionsUIScreen] {errorMessage}");
                    yield break;
                }

                catalog = parsedCatalog;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void RebuildView()
        {
            TryAutoBindReferences();
            HideTemplates();
            ClearRuntimeView();

            if (catalogRoot == null || contentRoot == null || catalogButtonTemplate == null || entryPrefab == null)
            {
                return;
            }

            InitializeEntryStates();
            RefreshActionButtons();

            IReadOnlyList<OptionsCategoryData> categories = catalog?.Categories != null
                ? catalog.Categories
                : Array.Empty<OptionsCategoryData>();
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                CreateCatalogButton(categories[categoryIndex], categoryIndex);
            }

            if (categories.Count > 0)
            {
                SelectCategory(0);
            }
        }

        private void CreateCatalogButton(OptionsCategoryData category, int categoryIndex)
        {
            GameObject instance = Instantiate(catalogButtonTemplate, catalogRoot, false);
            instance.name = $"Option Catalog {categoryIndex + 1:D2}";
            instance.SetActive(true);
            runtimeCatalogObjects.Add(instance);

            Button button = instance.GetComponent<Button>() ?? instance.GetComponentInChildren<Button>(true);
            TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
            Image image = button != null && button.image != null ? button.image : instance.GetComponent<Image>();

            if (label != null)
            {
                label.text = category?.Title ?? string.Empty;
            }

            if (button != null)
            {
                int capturedIndex = categoryIndex;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectCategory(capturedIndex));
            }

            runtimeCatalogButtons.Add(button);
            runtimeCatalogImages.Add(image);
        }

        private void SelectCategory(int categoryIndex)
        {
            IReadOnlyList<OptionsCategoryData> categories = catalog?.Categories != null
                ? catalog.Categories
                : Array.Empty<OptionsCategoryData>();
            if (categoryIndex < 0 || categoryIndex >= categories.Count)
            {
                return;
            }

            activeCategoryIndex = categoryIndex;
            RefreshCatalogSelection();
            ClearRuntimeEntries();

            OptionsCategoryData category = categories[categoryIndex];
            IReadOnlyList<OptionsEntryData> entries = category?.Entries != null
                ? category.Entries
                : Array.Empty<OptionsEntryData>();
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                CreateEntry(category, entries[entryIndex], entryIndex);
            }
        }

        private void CreateEntry(OptionsCategoryData category, OptionsEntryData entry, int entryIndex)
        {
            if (entryPrefab == null || contentRoot == null || entry == null)
            {
                return;
            }

            GameObject instance = Instantiate(entryPrefab, contentRoot, false);
            instance.name = $"Option Entry {entryIndex + 1:D2} {entry.Id}";
            instance.SetActive(true);
            runtimeEntryObjects.Add(instance);

            Transform root = instance.transform;
            TMP_Text settingText = root.Find("Setting Text")?.GetComponent<TMP_Text>();
            Slider slider = root.Find("Slider")?.GetComponent<Slider>();
            Button button = root.Find("Button")?.GetComponent<Button>();
            TMP_Text buttonText = root.Find("Button/Text (TMP)")?.GetComponent<TMP_Text>();
            TMP_Dropdown dropdown = root.Find("Dropdown")?.GetComponent<TMP_Dropdown>();
            Toggle toggle = root.Find("Toggle")?.GetComponent<Toggle>();
            string prefsKey = ResolvePrefsKey(category, entry);
            if (!entryStates.TryGetValue(prefsKey, out OptionsEntryRuntimeState state))
            {
                state = CreateRuntimeState(category, entry);
                entryStates[prefsKey] = state;
            }

            SetObjectActive(slider != null ? slider.gameObject : null, entry.Mode == SliderMode);
            SetObjectActive(button != null ? button.gameObject : null, entry.Mode == ButtonMode);
            SetObjectActive(dropdown != null ? dropdown.gameObject : null, entry.Mode == DropdownMode);
            SetObjectActive(toggle != null ? toggle.gameObject : null, entry.Mode == ToggleMode);

            switch (entry.Mode)
            {
                case SliderMode:
                    ConfigureSlider(state, entry, settingText, slider);
                    break;
                case DropdownMode:
                    ConfigureDropdown(state, entry, settingText, dropdown);
                    break;
                case ToggleMode:
                    ConfigureToggle(state, entry, settingText, toggle);
                    break;
                default:
                    ConfigureButton(entry, settingText, button, buttonText);
                    break;
            }
        }

        private void ConfigureSlider(OptionsEntryRuntimeState state, OptionsEntryData entry, TMP_Text label, Slider slider)
        {
            if (slider == null)
            {
                SetSettingLabel(label, entry.Title, string.Empty);
                return;
            }

            ResolveSliderBounds(entry, out float min, out float max);
            float value = ParseFloatValue(state.CurrentValue, ResolveDefaultSliderValue(entry));

            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = entry.WholeNumbers;
            slider.SetValueWithoutNotify(value);
            InstallSliderScrollBlocker(slider);
            SetSettingLabel(label, entry.Title, FormatSliderValue(entry, value, min, max));
            slider.onValueChanged.AddListener(nextValue =>
            {
                if (isRefreshingControls)
                {
                    return;
                }

                float clampedValue = Mathf.Clamp(nextValue, min, max);
                state.CurrentValue = FormatStoredFloat(clampedValue);
                SetSettingLabel(label, entry.Title, FormatSliderValue(entry, clampedValue, min, max));
                RefreshActionButtons();
            });
        }

        private void ConfigureDropdown(OptionsEntryRuntimeState state, OptionsEntryData entry, TMP_Text label, TMP_Dropdown dropdown)
        {
            SetSettingLabel(label, entry.Title, string.Empty);
            if (dropdown == null)
            {
                return;
            }

            List<OptionsChoiceData> choices = entry.Options ?? new List<OptionsChoiceData>();
            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.ClearOptions();

            List<TMP_Dropdown.OptionData> options = new(choices.Count);
            for (int optionIndex = 0; optionIndex < choices.Count; optionIndex++)
            {
                options.Add(new TMP_Dropdown.OptionData(choices[optionIndex].Title));
            }

            dropdown.AddOptions(options);
            dropdown.interactable = options.Count > 0;
            if (options.Count <= 0)
            {
                return;
            }

            int selectedIndex = FindChoiceIndex(choices, state.CurrentValue);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(nextIndex =>
            {
                if (isRefreshingControls)
                {
                    return;
                }

                int clampedIndex = Mathf.Clamp(nextIndex, 0, choices.Count - 1);
                state.CurrentValue = choices[clampedIndex].Value;
                RefreshActionButtons();
            });
        }

        private void ConfigureToggle(OptionsEntryRuntimeState state, OptionsEntryData entry, TMP_Text label, Toggle toggle)
        {
            SetSettingLabel(label, entry.Title, string.Empty);
            if (toggle == null)
            {
                return;
            }

            bool value = string.Equals(state.CurrentValue, bool.TrueString, StringComparison.Ordinal);

            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(nextValue =>
            {
                if (isRefreshingControls)
                {
                    return;
                }

                state.CurrentValue = nextValue ? bool.TrueString : bool.FalseString;
                RefreshActionButtons();
            });
        }

        private static void ConfigureButton(OptionsEntryData entry, TMP_Text label, Button button, TMP_Text buttonText)
        {
            SetSettingLabel(label, entry.Title, string.Empty);
            if (buttonText != null)
            {
                buttonText.text = entry.ButtonText;
            }

            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = false;
        }

        private void InitializeEntryStates()
        {
            IReadOnlyList<OptionsCategoryData> categories = catalog?.Categories != null
                ? catalog.Categories
                : Array.Empty<OptionsCategoryData>();

            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                OptionsCategoryData category = categories[categoryIndex];
                IReadOnlyList<OptionsEntryData> entries = category?.Entries != null
                    ? category.Entries
                    : Array.Empty<OptionsEntryData>();

                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    OptionsEntryData entry = entries[entryIndex];
                    if (entry == null || entry.Mode == ButtonMode)
                    {
                        continue;
                    }

                    string prefsKey = ResolvePrefsKey(category, entry);
                    if (!entryStates.ContainsKey(prefsKey))
                    {
                        entryStates[prefsKey] = CreateRuntimeState(category, entry);
                    }
                }
            }
        }

        private static OptionsEntryRuntimeState CreateRuntimeState(OptionsCategoryData category, OptionsEntryData entry)
        {
            string prefsKey = ResolvePrefsKey(category, entry);
            string defaultValue = ResolveDefaultStoredValue(entry);
            string originalValue = LoadStoredValue(entry, prefsKey, defaultValue);

            return new OptionsEntryRuntimeState
            {
                Entry = entry,
                PrefsKey = prefsKey,
                DefaultValue = defaultValue,
                OriginalValue = originalValue,
                CurrentValue = originalValue,
            };
        }

        private static string LoadStoredValue(OptionsEntryData entry, string prefsKey, string defaultValue)
        {
            switch (entry.Mode)
            {
                case SliderMode:
                    float sliderDefault = ParseFloatValue(defaultValue, ResolveDefaultSliderValue(entry));
                    return FormatStoredFloat(PlayerPrefs.GetFloat(prefsKey, sliderDefault));
                case DropdownMode:
                    if (PlayerPrefs.HasKey(prefsKey))
                    {
                        string storedDropdownValue = PlayerPrefs.GetString(prefsKey, string.Empty);
                        if (FindChoiceIndex(entry.Options, storedDropdownValue) >= 0)
                        {
                            return storedDropdownValue;
                        }
                    }

                    return defaultValue;
                case ToggleMode:
                    bool toggleDefault = string.Equals(defaultValue, bool.TrueString, StringComparison.Ordinal);
                    bool toggleValue = PlayerPrefs.GetInt(prefsKey, toggleDefault ? 1 : 0) != 0;
                    return toggleValue ? bool.TrueString : bool.FalseString;
                default:
                    return defaultValue;
            }
        }

        private static string ResolveDefaultStoredValue(OptionsEntryData entry)
        {
            switch (entry.Mode)
            {
                case SliderMode:
                    return FormatStoredFloat(ResolveDefaultSliderValue(entry));
                case DropdownMode:
                    return ResolveDefaultDropdownValue(entry);
                case ToggleMode:
                    return (entry.DefaultBool ?? false) ? bool.TrueString : bool.FalseString;
                default:
                    return string.Empty;
            }
        }

        private static string ResolveDefaultDropdownValue(OptionsEntryData entry)
        {
            List<OptionsChoiceData> choices = entry.Options ?? new List<OptionsChoiceData>();
            if (choices.Count <= 0)
            {
                return string.Empty;
            }

            int defaultById = FindChoiceIndex(choices, entry.DefaultOptionId);
            if (defaultById >= 0)
            {
                return choices[defaultById].Value;
            }

            int defaultIndex = Mathf.Clamp(entry.DefaultOptionIndex ?? 0, 0, choices.Count - 1);
            return choices[defaultIndex].Value;
        }

        private static int FindChoiceIndex(IReadOnlyList<OptionsChoiceData> choices, string value)
        {
            if (choices == null || string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i]?.Value, value.Trim(), StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ResolveSliderBounds(OptionsEntryData entry, out float min, out float max)
        {
            min = entry.Min ?? 0f;
            max = entry.Max ?? 1f;
            if (max < min)
            {
                (min, max) = (max, min);
            }
        }

        private static float ResolveDefaultSliderValue(OptionsEntryData entry)
        {
            ResolveSliderBounds(entry, out float min, out float max);
            return Mathf.Clamp(entry.DefaultValue ?? min, min, max);
        }

        private static float ParseFloatValue(string value, float fallback)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue)
                ? parsedValue
                : fallback;
        }

        private static string FormatStoredFloat(float value)
        {
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        private static string ResolvePrefsKey(OptionsCategoryData category, OptionsEntryData entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.PlayerPrefsKey))
            {
                return entry.PlayerPrefsKey.Trim();
            }

            string categoryId = string.IsNullOrWhiteSpace(category?.Id) ? "general" : category.Id.Trim();
            string entryId = string.IsNullOrWhiteSpace(entry.Id) ? "entry" : entry.Id.Trim();
            return $"Options.{categoryId}.{entryId}";
        }

        private static string FormatSliderValue(OptionsEntryData entry, float value, float min, float max)
        {
            if (!string.IsNullOrWhiteSpace(entry.ValueFormat))
            {
                string numericValue = value.ToString("0.##", CultureInfo.InvariantCulture);
                string percentValue = ResolvePercent(value, min, max).ToString(CultureInfo.InvariantCulture);
                return entry.ValueFormat
                    .Replace("{value}", numericValue)
                    .Replace("{percent}", percentValue);
            }

            if (entry.WholeNumbers)
            {
                return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
            }

            if (min >= 0f && max <= 1f)
            {
                return $"{Mathf.RoundToInt(value * 100f)}%";
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static int ResolvePercent(float value, float min, float max)
        {
            if (Mathf.Approximately(max, min))
            {
                return 0;
            }

            return Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 100f);
        }

        private static void SetSettingLabel(TMP_Text label, string title, string valueText)
        {
            if (label == null)
            {
                return;
            }

            label.text = string.IsNullOrWhiteSpace(valueText) ? title : $"{title}: {valueText}";
        }

        private void HandleCancelButtonClicked()
        {
            DiscardPendingChanges();
            RequestClose();
        }

        private void HandleResetButtonClicked()
        {
            bool changedAny = false;
            foreach (OptionsEntryRuntimeState state in entryStates.Values)
            {
                if (state == null || string.Equals(state.CurrentValue, state.DefaultValue, StringComparison.Ordinal))
                {
                    continue;
                }

                state.CurrentValue = state.DefaultValue;
                changedAny = true;
            }

            if (!changedAny)
            {
                RefreshActionButtons();
                return;
            }

            RefreshCurrentEntryControls();
            RefreshActionButtons();
        }

        private void HandleApplyButtonClicked()
        {
            ApplyPendingChanges();
        }

        private void ApplyPendingChanges()
        {
            foreach (OptionsEntryRuntimeState state in entryStates.Values)
            {
                ApplyStateToPlayerPrefs(state);
                state.OriginalValue = state.CurrentValue;
            }

            PlayerPrefs.Save();
            RefreshActionButtons();
        }

        private void DiscardPendingChanges()
        {
            bool changedAny = false;
            foreach (OptionsEntryRuntimeState state in entryStates.Values)
            {
                if (state == null || string.Equals(state.CurrentValue, state.OriginalValue, StringComparison.Ordinal))
                {
                    continue;
                }

                state.CurrentValue = state.OriginalValue;
                changedAny = true;
            }

            if (changedAny)
            {
                RefreshCurrentEntryControls();
            }

            RefreshActionButtons();
        }

        private static void ApplyStateToPlayerPrefs(OptionsEntryRuntimeState state)
        {
            if (state == null || state.Entry == null || string.IsNullOrWhiteSpace(state.PrefsKey))
            {
                return;
            }

            switch (state.Entry.Mode)
            {
                case SliderMode:
                    PlayerPrefs.SetFloat(
                        state.PrefsKey,
                        ParseFloatValue(state.CurrentValue, ResolveDefaultSliderValue(state.Entry)));
                    break;
                case DropdownMode:
                    PlayerPrefs.SetString(state.PrefsKey, state.CurrentValue ?? string.Empty);
                    break;
                case ToggleMode:
                    bool isOn = string.Equals(state.CurrentValue, bool.TrueString, StringComparison.Ordinal);
                    PlayerPrefs.SetInt(state.PrefsKey, isOn ? 1 : 0);
                    break;
            }
        }

        private void RefreshCurrentEntryControls()
        {
            if (activeCategoryIndex < 0)
            {
                return;
            }

            isRefreshingControls = true;
            try
            {
                SelectCategory(activeCategoryIndex);
            }
            finally
            {
                isRefreshingControls = false;
            }
        }

        private bool HasPendingChanges()
        {
            foreach (OptionsEntryRuntimeState state in entryStates.Values)
            {
                if (state != null && !string.Equals(state.CurrentValue, state.OriginalValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasResettableChanges()
        {
            foreach (OptionsEntryRuntimeState state in entryStates.Values)
            {
                if (state != null && !string.Equals(state.CurrentValue, state.DefaultValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshActionButtons()
        {
            bool hasPendingChanges = HasPendingChanges();
            bool hasResettableChanges = HasResettableChanges();

            SetButtonVisible(cancelButton, hasPendingChanges);
            SetButtonVisible(applyButton, hasPendingChanges);
            SetButtonVisible(resetButton, hasResettableChanges);
            SetObjectActive(buttonPanel, hasPendingChanges || hasResettableChanges);
        }

        private static void SetButtonVisible(Button button, bool isVisible)
        {
            if (button == null)
            {
                return;
            }

            if (button.gameObject.activeSelf != isVisible)
            {
                button.gameObject.SetActive(isVisible);
            }

            button.interactable = isVisible;
        }

        private void RefreshCatalogSelection()
        {
            for (int i = 0; i < runtimeCatalogButtons.Count; i++)
            {
                if (runtimeCatalogButtons[i] != null)
                {
                    runtimeCatalogButtons[i].interactable = i != activeCategoryIndex;
                }

                if (runtimeCatalogImages[i] != null)
                {
                    runtimeCatalogImages[i].color = i == activeCategoryIndex ? selectedCatalogColor : normalCatalogColor;
                }
            }
        }

        private void TryAutoBindReferences()
        {
            catalogRoot ??= transform.Find("Top Panel/Catalog") as RectTransform;
            contentRoot ??= transform.Find("Body/Scroll View/Viewport/Content") as RectTransform;
            closeButton ??= transform.Find("Top Panel/Close Button/Edge/Button")?.GetComponent<Button>();
            scrollRect ??= transform.Find("Body/Scroll View")?.GetComponent<ScrollRect>();
            buttonPanel ??= transform.Find("Body/Button Panel")?.gameObject;
            cancelButton ??= transform.Find("Body/Button Panel/Cancel")?.GetComponent<Button>();
            resetButton ??= transform.Find("Body/Button Panel/Reset")?.GetComponent<Button>();
            applyButton ??= transform.Find("Body/Button Panel/Apply")?.GetComponent<Button>();
            if (catalogRoot != null)
            {
                catalogButtonTemplate ??= catalogRoot.Find("Setting Catalog Button Prefab")?.gameObject;
            }
        }

        private void HideTemplates()
        {
            if (catalogButtonTemplate != null && catalogButtonTemplate.activeSelf)
            {
                catalogButtonTemplate.SetActive(false);
            }
        }

        private void BindButtons()
        {
            BindButton(closeButton, RequestClose);
            BindButton(cancelButton, HandleCancelButtonClicked);
            BindButton(resetButton, HandleResetButtonClicked);
            BindButton(applyButton, HandleApplyButtonClicked);
            RefreshActionButtons();
        }

        private void UnbindButtons()
        {
            UnbindButton(closeButton, RequestClose);
            UnbindButton(cancelButton, HandleCancelButtonClicked);
            UnbindButton(resetButton, HandleResetButtonClicked);
            UnbindButton(applyButton, HandleApplyButtonClicked);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
        }

        private void ClearRuntimeView()
        {
            ClearRuntimeEntries();
            ClearRuntimeCatalog();
            activeCategoryIndex = -1;
        }

        private void ClearRuntimeCatalog()
        {
            for (int i = 0; i < runtimeCatalogButtons.Count; i++)
            {
                if (runtimeCatalogButtons[i] != null)
                {
                    runtimeCatalogButtons[i].onClick.RemoveAllListeners();
                }
            }

            DestroyRuntimeObjects(runtimeCatalogObjects);
            runtimeCatalogButtons.Clear();
            runtimeCatalogImages.Clear();
        }

        private void ClearRuntimeEntries()
        {
            for (int i = 0; i < runtimeEntryObjects.Count; i++)
            {
                GameObject entryObject = runtimeEntryObjects[i];
                if (entryObject == null)
                {
                    continue;
                }

                Slider slider = entryObject.GetComponentInChildren<Slider>(true);
                TMP_Dropdown dropdown = entryObject.GetComponentInChildren<TMP_Dropdown>(true);
                Toggle toggle = entryObject.GetComponentInChildren<Toggle>(true);
                Button button = entryObject.transform.Find("Button")?.GetComponent<Button>();
                if (slider != null)
                {
                    slider.onValueChanged.RemoveAllListeners();
                }

                if (dropdown != null)
                {
                    dropdown.onValueChanged.RemoveAllListeners();
                }

                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                }

                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }

            DestroyRuntimeObjects(runtimeEntryObjects);
        }

        private static void DestroyRuntimeObjects(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    Destroy(objects[i]);
                }
            }

            objects.Clear();
        }

        private static void SetObjectActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private void ReleaseEntryPrefabHandle()
        {
            if (!hasEntryPrefabHandle)
            {
                return;
            }

            if (entryPrefabHandle.IsValid())
            {
                Addressables.Release(entryPrefabHandle);
            }

            entryPrefabHandle = default;
            hasEntryPrefabHandle = false;
            entryPrefab = null;
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private void InstallSliderScrollBlocker(Slider slider)
        {
            if (slider == null || scrollRect == null)
            {
                return;
            }

            SliderScrollRectBlocker blocker = slider.GetComponent<SliderScrollRectBlocker>();
            if (blocker == null)
            {
                blocker = slider.gameObject.AddComponent<SliderScrollRectBlocker>();
            }

            blocker.Initialize(scrollRect);
        }

        private sealed class OptionsEntryRuntimeState
        {
            public OptionsEntryData Entry { get; set; }
            public string PrefsKey { get; set; }
            public string DefaultValue { get; set; }
            public string OriginalValue { get; set; }
            public string CurrentValue { get; set; }
        }

        private sealed class SliderScrollRectBlocker :
            MonoBehaviour,
            IInitializePotentialDragHandler,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IPointerUpHandler,
            IBeginDragHandler,
            IEndDragHandler,
            ICancelHandler
        {
            private ScrollRect scrollRect;
            private bool hadScrollRectState;
            private bool previousScrollRectEnabled;
            private bool isPointerInside;
            private bool isPointerHeld;

            public void Initialize(ScrollRect targetScrollRect)
            {
                scrollRect = targetScrollRect;
            }

            public void OnInitializePotentialDrag(PointerEventData eventData)
            {
                DisableScrollRect();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                isPointerInside = true;
                DisableScrollRect();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                isPointerInside = false;
                if (!isPointerHeld)
                {
                    RestoreScrollRect();
                }
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                isPointerHeld = true;
                DisableScrollRect();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                DisableScrollRect();
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                isPointerHeld = false;
                if (!isPointerInside)
                {
                    RestoreScrollRect();
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                isPointerHeld = false;
                if (!isPointerInside)
                {
                    RestoreScrollRect();
                }
            }

            public void OnCancel(BaseEventData eventData)
            {
                isPointerHeld = false;
                RestoreScrollRect();
            }

            private void OnDisable()
            {
                isPointerInside = false;
                isPointerHeld = false;
                RestoreScrollRect();
            }

            private void DisableScrollRect()
            {
                if (scrollRect == null || hadScrollRectState)
                {
                    return;
                }

                previousScrollRectEnabled = scrollRect.enabled;
                scrollRect.enabled = false;
                hadScrollRectState = true;
            }

            private void RestoreScrollRect()
            {
                if (scrollRect == null || !hadScrollRectState)
                {
                    return;
                }

                scrollRect.enabled = previousScrollRectEnabled;
                hadScrollRectState = false;
            }
        }
    }

    [Serializable]
    public sealed class OptionsCatalogData
    {
        [JsonProperty("categories")]
        public List<OptionsCategoryData> Categories { get; set; } = new();
    }

    [Serializable]
    public sealed class OptionsCategoryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("entries")]
        public List<OptionsEntryData> Entries { get; set; } = new();
    }

    [Serializable]
    public sealed class OptionsEntryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("playerPrefsKey")]
        public string PlayerPrefsKey { get; set; }

        [JsonProperty("min")]
        public float? Min { get; set; }

        [JsonProperty("max")]
        public float? Max { get; set; }

        [JsonProperty("defaultValue")]
        public float? DefaultValue { get; set; }

        [JsonProperty("wholeNumbers")]
        public bool WholeNumbers { get; set; }

        [JsonProperty("valueFormat")]
        public string ValueFormat { get; set; }

        [JsonProperty("options")]
        public List<OptionsChoiceData> Options { get; set; } = new();

        [JsonProperty("defaultOptionId")]
        public string DefaultOptionId { get; set; }

        [JsonProperty("defaultOptionIndex")]
        public int? DefaultOptionIndex { get; set; }

        [JsonProperty("defaultBool")]
        public bool? DefaultBool { get; set; }

        [JsonProperty("buttonText")]
        public string ButtonText { get; set; }
    }

    [Serializable]
    public sealed class OptionsChoiceData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public static class OptionsCatalogUtility
    {
        private static readonly HashSet<string> SupportedModes = new(StringComparer.Ordinal)
        {
            "slider",
            "button",
            "dropdown",
            "toggle",
        };

        public static OptionsCatalogData CreateDefault()
        {
            return new OptionsCatalogData();
        }

        public static bool TryDeserializeCatalogJson(string jsonText, out OptionsCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Options catalog JSON is empty.";
                return false;
            }

            try
            {
                OptionsCatalogData rawCatalog = JsonConvert.DeserializeObject<OptionsCatalogData>(jsonText);
                catalog = Sanitize(rawCatalog);
                return true;
            }
            catch (JsonException exception)
            {
                errorMessage = $"Options catalog JSON is invalid: {exception.Message}";
                return false;
            }
        }

        public static OptionsCatalogData Sanitize(OptionsCatalogData catalog)
        {
            List<OptionsCategoryData> rawCategories = catalog?.Categories ?? new List<OptionsCategoryData>();
            List<OptionsCategoryData> categories = new(rawCategories.Count);
            HashSet<string> seenCategoryIds = new(StringComparer.Ordinal);

            for (int categoryIndex = 0; categoryIndex < rawCategories.Count; categoryIndex++)
            {
                OptionsCategoryData rawCategory = rawCategories[categoryIndex];
                string categoryId = SanitizeIdentifier(rawCategory?.Id, $"category_{categoryIndex + 1}");
                categoryId = EnsureUniqueIdentifier(categoryId, seenCategoryIds);

                categories.Add(new OptionsCategoryData
                {
                    Id = categoryId,
                    Title = SanitizeText(rawCategory?.Title, categoryId),
                    Entries = SanitizeEntries(rawCategory?.Entries),
                });
            }

            return new OptionsCatalogData
            {
                Categories = categories,
            };
        }

        private static List<OptionsEntryData> SanitizeEntries(List<OptionsEntryData> entries)
        {
            List<OptionsEntryData> rawEntries = entries ?? new List<OptionsEntryData>();
            List<OptionsEntryData> sanitizedEntries = new(rawEntries.Count);
            HashSet<string> seenEntryIds = new(StringComparer.Ordinal);

            for (int entryIndex = 0; entryIndex < rawEntries.Count; entryIndex++)
            {
                OptionsEntryData rawEntry = rawEntries[entryIndex];
                string entryId = SanitizeIdentifier(rawEntry?.Id, $"entry_{entryIndex + 1}");
                entryId = EnsureUniqueIdentifier(entryId, seenEntryIds);
                string mode = SanitizeMode(rawEntry);

                sanitizedEntries.Add(new OptionsEntryData
                {
                    Id = entryId,
                    Title = SanitizeText(rawEntry?.Title, entryId),
                    Mode = mode,
                    Type = mode,
                    PlayerPrefsKey = SanitizeOptionalText(rawEntry?.PlayerPrefsKey),
                    Min = rawEntry?.Min,
                    Max = rawEntry?.Max,
                    DefaultValue = rawEntry?.DefaultValue,
                    WholeNumbers = rawEntry?.WholeNumbers ?? false,
                    ValueFormat = SanitizeOptionalText(rawEntry?.ValueFormat),
                    Options = SanitizeChoices(rawEntry?.Options),
                    DefaultOptionId = SanitizeOptionalText(rawEntry?.DefaultOptionId),
                    DefaultOptionIndex = rawEntry?.DefaultOptionIndex,
                    DefaultBool = rawEntry?.DefaultBool,
                    ButtonText = SanitizeOptionalText(rawEntry?.ButtonText),
                });
            }

            return sanitizedEntries;
        }

        private static List<OptionsChoiceData> SanitizeChoices(List<OptionsChoiceData> choices)
        {
            List<OptionsChoiceData> rawChoices = choices ?? new List<OptionsChoiceData>();
            List<OptionsChoiceData> sanitizedChoices = new(rawChoices.Count);
            HashSet<string> seenChoiceValues = new(StringComparer.Ordinal);

            for (int choiceIndex = 0; choiceIndex < rawChoices.Count; choiceIndex++)
            {
                OptionsChoiceData rawChoice = rawChoices[choiceIndex];
                string value = SanitizeText(rawChoice?.Value, rawChoice?.Id);
                value = SanitizeIdentifier(value, $"choice_{choiceIndex + 1}");
                value = EnsureUniqueIdentifier(value, seenChoiceValues);
                string title = SanitizeText(rawChoice?.Title, value);

                sanitizedChoices.Add(new OptionsChoiceData
                {
                    Id = SanitizeText(rawChoice?.Id, value),
                    Title = title,
                    Value = value,
                });
            }

            return sanitizedChoices;
        }

        private static string SanitizeMode(OptionsEntryData entry)
        {
            string rawMode = !string.IsNullOrWhiteSpace(entry?.Mode) ? entry.Mode : entry?.Type;
            string mode = rawMode != null ? rawMode.Trim().ToLowerInvariant() : string.Empty;
            return SupportedModes.Contains(mode) ? mode : "button";
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string EnsureUniqueIdentifier(string identifier, ISet<string> seen)
        {
            if (!seen.Contains(identifier))
            {
                seen.Add(identifier);
                return identifier;
            }

            int suffix = 2;
            string uniqueId;
            do
            {
                uniqueId = $"{identifier}_{suffix}";
                suffix++;
            }
            while (!seen.Add(uniqueId));

            return uniqueId;
        }

        private static string SanitizeText(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string SanitizeOptionalText(string value)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : trimmed;
        }
    }
}
