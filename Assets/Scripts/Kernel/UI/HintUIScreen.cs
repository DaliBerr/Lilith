using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Vocalith.Logging;
using Vocalith.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kernel.UI
{
    /// <summary>
    /// Hint 弹窗屏幕：展示图鉴与帮助条目，支持分类、条目切换与正文阅读。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Hint UI")]
    public sealed class HintUIScreen : GameUIScreen
    {
        private const string DefaultCatalogAddress = "Assets/Data/UI/HintCatalog";
        private const string DefaultHintEntryPrefabAddress = "Assets/Prefabs/UI/Hint Entry";
        private const string DefaultHintCatalogEntryPrefabAddress = "Assets/Prefabs/UI/Hint Catalog Entry";
        private const string DefaultEnemyCategoryTitle = "图鉴";
        private const string EmptyContentText = "暂无帮助内容。";
        private const string EmptyEnemyDescriptionText = "暂无描述。";
        private const string EmptyCategoryText = "当前分类暂无条目。";

        [Header("Layout")]
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private RectTransform catalogRoot;
        [SerializeField] private RectTransform leftPanelRoot;
        [SerializeField] private RectTransform mainContentRoot;
        [SerializeField] private TMP_Text mainContentText;
        [SerializeField] private Button closeButton;

        [Header("Data")]
        [SerializeField] private string catalogAddress = DefaultCatalogAddress;
        [SerializeField] private string hintEntryPrefabAddress = DefaultHintEntryPrefabAddress;
        [SerializeField] private string hintCatalogEntryPrefabAddress = DefaultHintCatalogEntryPrefabAddress;
        [SerializeField] private string enemyCategoryTitle = DefaultEnemyCategoryTitle;

        [Header("State Colors")]
        [SerializeField] private Color normalCategoryColor = Color.white;
        [SerializeField] private Color selectedCategoryColor = new(0.86f, 0.93f, 1f, 1f);
        [SerializeField] private Color normalEntryColor = new(1f, 1f, 1f, 0.86f);
        [SerializeField] private Color selectedEntryColor = new(1f, 0.95f, 0.82f, 1f);

        private readonly List<GameObject> runtimeCategoryObjects = new();
        private readonly List<GameObject> runtimeEntryObjects = new();
        private readonly List<Button> runtimeCategoryButtons = new();
        private readonly List<Button> runtimeEntryButtons = new();
        private readonly List<HintCategoryData> runtimeCategories = new();
        private readonly List<HintEntryData> runtimeEntries = new();

        private AsyncOperationHandle<GameObject> hintEntryPrefabHandle;
        private bool hasHintEntryPrefabHandle;
        private AsyncOperationHandle<GameObject> hintCatalogEntryPrefabHandle;
        private bool hasHintCatalogEntryPrefabHandle;
        private GameObject hintEntryPrefab;
        private GameObject hintCatalogEntryPrefab;

        private HintCatalogData manualCatalog;
        private bool hasLoadedManualCatalog;
        private int activeCategoryIndex = -1;
        private int activeEntryIndex = -1;

        public override Status currentStatus { get; } = StatusList.InHintStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindCloseButton();
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
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindCloseButton();
            ClearRuntimeView();
            ReleasePrefabHandles();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 请求关闭当前 Hint 弹窗；优先关闭 modal 栈顶实例。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            if (ui == null)
            {
                return;
            }

            if (ui.GetTopModal() == this)
            {
                ui.CloseModal(this);
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        private IEnumerator EnsureAssetsLoadedCo()
        {
            if (!hasHintEntryPrefabHandle)
            {
                string address = string.IsNullOrWhiteSpace(hintEntryPrefabAddress)
                    ? DefaultHintEntryPrefabAddress
                    : hintEntryPrefabAddress.Trim();
                hintEntryPrefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                yield return hintEntryPrefabHandle;
                hasHintEntryPrefabHandle = true;
                if (hintEntryPrefabHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    hintEntryPrefab = hintEntryPrefabHandle.Result;
                }
                else
                {
                    GameDebug.LogWarning($"[HintUIScreen] Failed to load hint entry prefab at '{address}'.");
                }
            }

            if (!hasHintCatalogEntryPrefabHandle)
            {
                string address = string.IsNullOrWhiteSpace(hintCatalogEntryPrefabAddress)
                    ? DefaultHintCatalogEntryPrefabAddress
                    : hintCatalogEntryPrefabAddress.Trim();
                hintCatalogEntryPrefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                yield return hintCatalogEntryPrefabHandle;
                hasHintCatalogEntryPrefabHandle = true;
                if (hintCatalogEntryPrefabHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    hintCatalogEntryPrefab = hintCatalogEntryPrefabHandle.Result;
                }
                else
                {
                    GameDebug.LogWarning($"[HintUIScreen] Failed to load hint catalog entry prefab at '{address}'.");
                }
            }

            if (!hasLoadedManualCatalog)
            {
                yield return LoadManualCatalogCo();
            }
        }

        private IEnumerator LoadManualCatalogCo()
        {
            hasLoadedManualCatalog = true;
            manualCatalog = HintCatalogUtility.CreateDefault();

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
                    GameDebug.LogWarning($"[HintUIScreen] Failed to load hint catalog JSON at '{address}'.");
                    yield break;
                }

                if (!HintCatalogUtility.TryDeserializeCatalogJson(handle.Result.text, out HintCatalogData parsedCatalog, out string errorMessage))
                {
                    GameDebug.LogWarning($"[HintUIScreen] {errorMessage}");
                    yield break;
                }

                manualCatalog = parsedCatalog;
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
            ClearRuntimeView();
            runtimeCategories.Clear();
            runtimeEntries.Clear();

            if (catalogRoot == null || leftPanelRoot == null || mainContentRoot == null || mainContentText == null)
            {
                SetMainContentText(EmptyContentText);
                return;
            }

            BuildRuntimeCategories(runtimeCategories);
            if (runtimeCategories.Count <= 0)
            {
                SetMainContentText(EmptyContentText);
                return;
            }

            BuildCategoryButtons(runtimeCategories);
            SelectCategory(0);
        }

        private void BuildRuntimeCategories(List<HintCategoryData> categories)
        {
            categories.Clear();

            List<HintEntryData> enemyEntries = BuildEnemyEntries();
            if (enemyEntries.Count > 0)
            {
                categories.Add(new HintCategoryData
                {
                    Id = "enemy_catalog",
                    Title = ResolveEnemyCategoryTitle(),
                    Entries = enemyEntries,
                });
            }

            if (manualCatalog?.Categories == null)
            {
                return;
            }

            for (int categoryIndex = 0; categoryIndex < manualCatalog.Categories.Count; categoryIndex++)
            {
                HintCategoryData category = manualCatalog.Categories[categoryIndex];
                if (category == null)
                {
                    continue;
                }

                List<HintEntryData> copiedEntries = new();
                if (category.Entries != null)
                {
                    for (int entryIndex = 0; entryIndex < category.Entries.Count; entryIndex++)
                    {
                        HintEntryData entry = category.Entries[entryIndex];
                        if (entry == null)
                        {
                            continue;
                        }

                        copiedEntries.Add(new HintEntryData
                        {
                            Id = entry.Id,
                            Title = entry.Title,
                            Content = entry.Content,
                        });
                    }
                }

                categories.Add(new HintCategoryData
                {
                    Id = category.Id,
                    Title = category.Title,
                    Entries = copiedEntries,
                });
            }
        }

        private List<HintEntryData> BuildEnemyEntries()
        {
            List<EnemyDefinition> loadedDefinitions = CollectEnemyDefinitions();
            Dictionary<string, EnemyDefinition> bestDefinitionById = new(StringComparer.Ordinal);

            for (int index = 0; index < loadedDefinitions.Count; index++)
            {
                EnemyDefinition definition = loadedDefinitions[index];
                if (definition == null)
                {
                    continue;
                }

                string enemyId = definition.EnemyId;
                if (string.IsNullOrWhiteSpace(enemyId))
                {
                    continue;
                }

                if (!bestDefinitionById.TryGetValue(enemyId, out EnemyDefinition existing))
                {
                    bestDefinitionById.Add(enemyId, definition);
                    continue;
                }

                bool existingHasDescription = !string.IsNullOrWhiteSpace(existing.Description);
                bool candidateHasDescription = !string.IsNullOrWhiteSpace(definition.Description);
                if (!existingHasDescription && candidateHasDescription)
                {
                    bestDefinitionById[enemyId] = definition;
                }
            }

            List<EnemyDefinition> sortedDefinitions = bestDefinitionById.Values
                .OrderBy(def => ResolveEnemyDisplayName(def), StringComparer.Ordinal)
                .ToList();

            List<HintEntryData> enemyEntries = new(sortedDefinitions.Count);
            for (int index = 0; index < sortedDefinitions.Count; index++)
            {
                EnemyDefinition definition = sortedDefinitions[index];
                string enemyId = definition.EnemyId;
                string displayName = ResolveEnemyDisplayName(definition);
                string description = string.IsNullOrWhiteSpace(definition.Description)
                    ? EmptyEnemyDescriptionText
                    : definition.Description;

                enemyEntries.Add(new HintEntryData
                {
                    Id = $"enemy:{enemyId}",
                    Title = displayName,
                    Content = description,
                });
            }

            return enemyEntries;
        }

        /// <summary>
        /// summary: 收集当前可用于 Hint 图鉴的敌人定义；编辑器下优先从资产数据库全量读取。
        /// param: 无
        /// returns: 可用于图鉴渲染的敌人定义列表
        /// </summary>
        private static List<EnemyDefinition> CollectEnemyDefinitions()
        {
            List<EnemyDefinition> definitions = new();
            HashSet<EnemyDefinition> seen = new();

#if UNITY_EDITOR
            string[] enemyDefinitionGuids = AssetDatabase.FindAssets("t:EnemyDefinition");
            for (int index = 0; index < enemyDefinitionGuids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(enemyDefinitionGuids[index]);
                EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(assetPath);
                if (definition == null || !seen.Add(definition))
                {
                    continue;
                }

                definitions.Add(definition);
            }
#else
            EnemyDefinition[] loadedDefinitions = Resources.FindObjectsOfTypeAll<EnemyDefinition>();
            for (int index = 0; index < loadedDefinitions.Length; index++)
            {
                EnemyDefinition definition = loadedDefinitions[index];
                if (definition == null || !seen.Add(definition))
                {
                    continue;
                }

                definitions.Add(definition);
            }
#endif

            return definitions;
        }

        private void BuildCategoryButtons(IReadOnlyList<HintCategoryData> categories)
        {
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                HintCategoryData category = categories[categoryIndex];
                GameObject categoryObject = Instantiate(hintCatalogEntryPrefab, catalogRoot, false);
                categoryObject.name = $"Hint Catalog Entry {categoryIndex + 1:D2}";
                runtimeCategoryObjects.Add(categoryObject);

                Button categoryButton = PrepareButton(categoryObject, category?.Title ?? string.Empty);
                runtimeCategoryButtons.Add(categoryButton);

                if (categoryButton != null)
                {
                    int capturedIndex = categoryIndex;
                    categoryButton.onClick.AddListener(() => SelectCategory(capturedIndex));
                }
            }
        }

        private void BuildEntryButtons(IReadOnlyList<HintEntryData> entries)
        {
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                HintEntryData entry = entries[entryIndex];
                GameObject entryObject = Instantiate(hintEntryPrefab, leftPanelRoot, false);
                entryObject.name = $"Hint Entry {entryIndex + 1:D2}";
                runtimeEntryObjects.Add(entryObject);

                Button entryButton = PrepareButton(entryObject, entry?.Title ?? string.Empty);
                runtimeEntryButtons.Add(entryButton);

                if (entryButton != null)
                {
                    int capturedIndex = entryIndex;
                    entryButton.onClick.AddListener(() => SelectEntry(capturedIndex));
                }
            }
        }

        private Button PrepareButton(GameObject target, string labelText)
        {
            if (target == null)
            {
                return null;
            }

            TMP_Text label = target.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = string.IsNullOrWhiteSpace(labelText) ? "-" : labelText.Trim();
            }

            Button button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = target.GetComponent<Graphic>();
            }

            button.onClick.RemoveAllListeners();
            button.interactable = true;
            return button;
        }

        private void SelectCategory(int categoryIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= runtimeCategories.Count)
            {
                return;
            }

            activeCategoryIndex = categoryIndex;
            UpdateCategorySelectionVisual();

            ClearRuntimeEntries();
            runtimeEntries.Clear();

            HintCategoryData category = runtimeCategories[categoryIndex];
            if (category?.Entries != null)
            {
                runtimeEntries.AddRange(category.Entries);
            }

            if (runtimeEntries.Count <= 0)
            {
                activeEntryIndex = -1;
                SetMainContentText(EmptyCategoryText);
                return;
            }

            BuildEntryButtons(runtimeEntries);
            SelectEntry(0);
        }

        private void SelectEntry(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= runtimeEntries.Count)
            {
                return;
            }

            activeEntryIndex = entryIndex;
            UpdateEntrySelectionVisual();

            HintEntryData entry = runtimeEntries[entryIndex];
            string entryTitle = string.IsNullOrWhiteSpace(entry?.Title) ? "未命名条目" : entry.Title.Trim();
            string entryContent = string.IsNullOrWhiteSpace(entry?.Content) ? EmptyContentText : entry.Content.Trim();
            SetMainContentText($"{entryTitle}\n\n{entryContent}");
        }

        private void UpdateCategorySelectionVisual()
        {
            for (int index = 0; index < runtimeCategoryButtons.Count; index++)
            {
                Image image = ResolveButtonImage(runtimeCategoryButtons[index]);
                if (image == null)
                {
                    continue;
                }

                image.color = index == activeCategoryIndex ? selectedCategoryColor : normalCategoryColor;
            }
        }

        private void UpdateEntrySelectionVisual()
        {
            for (int index = 0; index < runtimeEntryButtons.Count; index++)
            {
                Image image = ResolveButtonImage(runtimeEntryButtons[index]);
                if (image == null)
                {
                    continue;
                }

                image.color = index == activeEntryIndex ? selectedEntryColor : normalEntryColor;
            }
        }

        private static Image ResolveButtonImage(Button button)
        {
            if (button == null)
            {
                return null;
            }

            if (button.targetGraphic is Image targetImage)
            {
                return targetImage;
            }

            return button.GetComponent<Image>();
        }

        private void SetMainContentText(string content)
        {
            if (mainContentText != null)
            {
                mainContentText.text = content ?? string.Empty;
            }
        }

        private string ResolveEnemyCategoryTitle()
        {
            return string.IsNullOrWhiteSpace(enemyCategoryTitle)
                ? DefaultEnemyCategoryTitle
                : enemyCategoryTitle.Trim();
        }

        private static string ResolveEnemyDisplayName(EnemyDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            return definition.EnemyId;
        }

        private void BindCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
        }

        private void UnbindCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
        }

        private void TryAutoBindReferences()
        {
            mainPanel ??= transform.Find("Main Panel") as RectTransform;
            if (mainPanel == null)
            {
                return;
            }

            catalogRoot ??= mainPanel.Find("Catalog") as RectTransform;
            leftPanelRoot ??= mainPanel.Find("Left Panel") as RectTransform;
            mainContentRoot ??= mainPanel.Find("Main Content") as RectTransform;
            mainContentText ??= mainContentRoot != null ? mainContentRoot.GetComponentInChildren<TMP_Text>(true) : null;
            closeButton ??= ResolveOptionalCloseButton(mainPanel);
        }

        private static Button ResolveOptionalCloseButton(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform namedCloseButton = root.Find("Close Button") ?? root.Find("Close");
            if (namedCloseButton != null)
            {
                return namedCloseButton.GetComponent<Button>();
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button candidate = buttons[index];
                if (candidate != null && candidate.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ClearRuntimeView()
        {
            ClearRuntimeCategories();
            ClearRuntimeEntries();
        }

        private void ClearRuntimeCategories()
        {
            for (int index = runtimeCategoryObjects.Count - 1; index >= 0; index--)
            {
                if (runtimeCategoryObjects[index] != null)
                {
                    Destroy(runtimeCategoryObjects[index]);
                }
            }

            runtimeCategoryObjects.Clear();
            runtimeCategoryButtons.Clear();
            activeCategoryIndex = -1;
        }

        private void ClearRuntimeEntries()
        {
            for (int index = runtimeEntryObjects.Count - 1; index >= 0; index--)
            {
                if (runtimeEntryObjects[index] != null)
                {
                    Destroy(runtimeEntryObjects[index]);
                }
            }

            runtimeEntryObjects.Clear();
            runtimeEntryButtons.Clear();
            activeEntryIndex = -1;
        }

        private void ReleasePrefabHandles()
        {
            if (hasHintEntryPrefabHandle && hintEntryPrefabHandle.IsValid())
            {
                Addressables.Release(hintEntryPrefabHandle);
            }

            if (hasHintCatalogEntryPrefabHandle && hintCatalogEntryPrefabHandle.IsValid())
            {
                Addressables.Release(hintCatalogEntryPrefabHandle);
            }

            hasHintEntryPrefabHandle = false;
            hasHintCatalogEntryPrefabHandle = false;
            hintEntryPrefab = null;
            hintCatalogEntryPrefab = null;
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }
    }
}
