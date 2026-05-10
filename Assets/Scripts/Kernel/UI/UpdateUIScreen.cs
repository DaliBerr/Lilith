using System.Collections;
using System.Collections.Generic;
using Kernel.GameState;
using Kernel.Upgrade;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Vocalith.Localization;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 永久升级主屏，负责按 JSON 目录动态生成 section 与升级按钮。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Update UI Screen")]
    public sealed class UpdateUIScreen : GameUIScreen
    {
        private const string DefaultTitle = "永久升级";
        private const string UpgradeGridPrefabAddress = "Assets/Prefabs/UI/Upgrade Grid Prefab";

        [Header("Layout")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform sectionTemplate;

        private readonly List<GameObject> runtimeSections = new();
        private Coroutine refreshRoutine;
        private bool refreshQueued;
        private string resolvedBaseTitle = DefaultTitle;
        private AsyncOperationHandle<GameObject> upgradeGridPrefabHandle;
        private bool hasUpgradeGridPrefabHandle;
        private PlayerRemnantWallet observedWallet;

        public override Status currentStatus { get; } = StatusList.InUpgradeScreenStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            CacheBaseTitle();
            HideSectionTemplate();
            QueueRefreshContent();
        }

        protected override void OnBeforeShow()
        {
            SubscribeWalletChanges();
            QueueRefreshContent();
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
            UnsubscribeWalletChanges();
            StopRefreshRoutine();
            ClearRuntimeSections();
        }

        private void OnDestroy()
        {
            RemoveCurrentStatus();
            UnsubscribeWalletChanges();
            StopRefreshRoutine();
            ClearRuntimeSections();
            ReleaseUpgradeGridPrefabHandle();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            CacheBaseTitle();
        }

        /// <summary>
        /// summary: 请求关闭当前永久升级 Screen；若当前界面位于栈顶则会直接 Pop。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            if (ui?.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        private void QueueRefreshContent()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (refreshRoutine != null)
            {
                refreshQueued = true;
                return;
            }

            refreshRoutine = StartCoroutine(RefreshContentCo());
        }

        /// <summary>
        /// summary: 协程式刷新升级列表，等待目录与 Grid prefab 都就绪后再重建内容。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        private IEnumerator RefreshContentCo()
        {
            do
            {
                refreshQueued = false;
                TryAutoBindReferences();
                CacheBaseTitle();
                HideSectionTemplate();
                UpdateTitleWithRemnants();
                ClearRuntimeSections();

                if (contentRoot == null || sectionTemplate == null)
                {
                    refreshRoutine = null;
                    yield break;
                }

                PermanentUpgradeService upgradeService = PermanentUpgradeService.GetOrCreateInstance();
                if (upgradeService == null)
                {
                    refreshRoutine = null;
                    yield break;
                }

                yield return upgradeService.LoadCatalogIfNeededCo();
                yield return EnsureUpgradeGridPrefabLoadedCo();

                if (!upgradeService.HasCatalogLoaded || !TryGetUpgradeGridPrefab(out GameObject upgradeGridPrefab))
                {
                    refreshRoutine = null;
                    yield break;
                }

                BuildSections(upgradeService.GetSections(), upgradeGridPrefab, upgradeService);
                UpdateTitleWithRemnants();
            }
            while (refreshQueued);

            refreshRoutine = null;
        }

        private void BuildSections(
            IReadOnlyList<PermanentUpgradeSectionData> sections,
            GameObject upgradeGridPrefab,
            PermanentUpgradeService upgradeService)
        {
            if (sections == null || upgradeGridPrefab == null)
            {
                return;
            }

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                PermanentUpgradeSectionData section = sections[sectionIndex];
                if (section == null)
                {
                    continue;
                }

                RectTransform sectionInstance = Instantiate(sectionTemplate, contentRoot, false);
                sectionInstance.name = $"Upgrade Section {sectionIndex + 1:D2}";
                sectionInstance.gameObject.SetActive(true);
                runtimeSections.Add(sectionInstance.gameObject);

                TMP_Text sectionTitle = sectionInstance.Find("Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
                if (sectionTitle != null)
                {
                    sectionTitle.text = section.Title;
                }

                RectTransform gridRoot = sectionInstance.Find("Grid") as RectTransform;
                if (gridRoot == null || section.Entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
                {
                    PermanentUpgradeEntryData entry = section.Entries[entryIndex];
                    if (entry == null)
                    {
                        continue;
                    }

                    GameObject gridInstance = Instantiate(upgradeGridPrefab, gridRoot, false);
                    gridInstance.name = $"Upgrade Entry {entryIndex + 1:D2}";

                    Button button = gridInstance.GetComponentInChildren<Button>(true);
                    TMP_Text label = gridInstance.GetComponentInChildren<TMP_Text>(true);
                    ConfigureUpgradeButton(button, label, entry, upgradeService);
                }
            }
        }

        private void ConfigureUpgradeButton(
            Button button,
            TMP_Text label,
            PermanentUpgradeEntryData entry,
            PermanentUpgradeService upgradeService)
        {
            int currentLevel = upgradeService.GetPurchasedLevel(entry.Id);
            int currentRemnants = PlayerRemnantWallet.GetCurrentRemnants();
            bool isMaxLevel = currentLevel >= entry.MaxLevel;
            bool canAfford = currentRemnants >= entry.CostRemnants;
            bool canPurchase = !isMaxLevel && canAfford;

            if (label != null)
            {
                label.text = BuildEntryLabel(entry, currentLevel, canAfford, isMaxLevel);
            }

            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = canPurchase;
            button.onClick.AddListener(() => HandleUpgradeButtonClicked(entry.Id));
        }

        private void HandleUpgradeButtonClicked(string entryId)
        {
            PermanentUpgradeService upgradeService = PermanentUpgradeService.GetOrCreateInstance();
            if (upgradeService == null)
            {
                return;
            }

            if (!upgradeService.TryPurchase(entryId, out PermanentUpgradePurchaseResult purchaseResult))
            {
                if (!string.IsNullOrWhiteSpace(purchaseResult.Message))
                {
                    StartCoroutine(PopUpUIUtility.ShowInfoPopup(ui, nameof(UpdateUIScreen), purchaseResult.Message));
                }

                QueueRefreshContent();
                return;
            }

            QueueRefreshContent();
        }

        private IEnumerator EnsureUpgradeGridPrefabLoadedCo()
        {
            if (TryGetUpgradeGridPrefab(out _))
            {
                yield break;
            }

            if (hasUpgradeGridPrefabHandle)
            {
                yield break;
            }

            upgradeGridPrefabHandle = Addressables.LoadAssetAsync<GameObject>(UpgradeGridPrefabAddress);
            hasUpgradeGridPrefabHandle = true;
            yield return upgradeGridPrefabHandle;

            if (upgradeGridPrefabHandle.Status != AsyncOperationStatus.Succeeded || upgradeGridPrefabHandle.Result == null)
            {
                ReleaseUpgradeGridPrefabHandle();
            }
        }

        private bool TryGetUpgradeGridPrefab(out GameObject prefab)
        {
            prefab = null;
            if (!hasUpgradeGridPrefabHandle)
            {
                return false;
            }

            if (!upgradeGridPrefabHandle.IsValid() || upgradeGridPrefabHandle.Status != AsyncOperationStatus.Succeeded)
            {
                return false;
            }

            prefab = upgradeGridPrefabHandle.Result;
            return prefab != null;
        }

        private void ReleaseUpgradeGridPrefabHandle()
        {
            if (!hasUpgradeGridPrefabHandle)
            {
                return;
            }

            if (upgradeGridPrefabHandle.IsValid())
            {
                Addressables.Release(upgradeGridPrefabHandle);
            }

            upgradeGridPrefabHandle = default;
            hasUpgradeGridPrefabHandle = false;
        }

        private void TryAutoBindReferences()
        {
            panelRoot ??= transform.Find("Panel") as RectTransform;
            if (panelRoot == null)
            {
                return;
            }

            titleText ??= panelRoot.Find("Titile/Text (TMP)")?.GetComponent<TMP_Text>();
            contentRoot ??= panelRoot.Find("Main Content/Scroll View/Viewport/Content") as RectTransform;
            if (contentRoot != null)
            {
                sectionTemplate ??= contentRoot.Find("Upgrage Section Prefab") as RectTransform;
            }
        }

        private void CacheBaseTitle()
        {
            if (titleText == null)
            {
                resolvedBaseTitle = LocalizationManager.TranslateOrDefault("ui.upgrade.title", DefaultTitle);
                return;
            }

            string trimmedTitle = titleText.text != null ? titleText.text.Trim() : string.Empty;
            int suffixIndex = trimmedTitle.IndexOf(" (残卷:", System.StringComparison.Ordinal);
            if (suffixIndex >= 0)
            {
                trimmedTitle = trimmedTitle.Substring(0, suffixIndex).TrimEnd();
            }

            resolvedBaseTitle = string.IsNullOrEmpty(trimmedTitle)
                ? LocalizationManager.TranslateOrDefault("ui.upgrade.title", DefaultTitle)
                : trimmedTitle;
        }

        private void UpdateTitleWithRemnants()
        {
            if (titleText == null)
            {
                return;
            }

            titleText.text = LocalizationManager.TranslateFormatOrDefault(
                "ui.upgrade.title_with_remnants",
                "{0} (残卷: {1})",
                resolvedBaseTitle,
                PlayerRemnantWallet.GetCurrentRemnants());
        }

        private void HideSectionTemplate()
        {
            if (sectionTemplate != null && sectionTemplate.gameObject.activeSelf)
            {
                sectionTemplate.gameObject.SetActive(false);
            }
        }

        private void ClearRuntimeSections()
        {
            for (int index = runtimeSections.Count - 1; index >= 0; index--)
            {
                GameObject runtimeSection = runtimeSections[index];
                if (runtimeSection != null)
                {
                    DestroyChild(runtimeSection);
                }
            }

            runtimeSections.Clear();
        }

        private void StopRefreshRoutine()
        {
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }

            refreshQueued = false;
        }

        private void SubscribeWalletChanges()
        {
            PlayerRemnantWallet wallet = PlayerRemnantWallet.Instance ?? FindFirstObjectByType<PlayerRemnantWallet>();
            if (wallet == null || observedWallet == wallet)
            {
                return;
            }

            UnsubscribeWalletChanges();
            observedWallet = wallet;
            observedWallet.Changed += HandleWalletChanged;
        }

        private void UnsubscribeWalletChanges()
        {
            if (observedWallet == null)
            {
                return;
            }

            observedWallet.Changed -= HandleWalletChanged;
            observedWallet = null;
        }

        private void HandleWalletChanged(int previousCount, int currentCount)
        {
            QueueRefreshContent();
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private static string BuildEntryLabel(
            PermanentUpgradeEntryData entry,
            int currentLevel,
            bool canAfford,
            bool isMaxLevel)
        {
            string levelLine = LocalizationManager.TranslateFormatOrDefault(
                "ui.upgrade.level",
                "等级 {0}/{1}",
                currentLevel,
                entry.MaxLevel);
            string costLine = isMaxLevel
                ? LocalizationManager.TranslateOrDefault("ui.upgrade.max_level", "已满级")
                : canAfford
                    ? LocalizationManager.TranslateFormatOrDefault("ui.upgrade.cost", "消耗 {0} 残卷", entry.CostRemnants)
                    : LocalizationManager.TranslateFormatOrDefault("ui.upgrade.insufficient_remnants", "残卷不足 ({0})", entry.CostRemnants);
            return $"{entry.Title}\n{levelLine}\n{costLine}";
        }

        private static void DestroyChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
