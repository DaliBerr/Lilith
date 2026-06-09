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
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 永久升级主屏，负责按 JSON 目录动态生成科技树节点与连线。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Update UI Screen")]
    public sealed class UpdateUIScreen : GameUIScreen
    {
        private const string DefaultTitle = "永久升级";
        private const string UpgradeNodePrefabAddress = "Assets/Prefabs/UI/Upgrade/Upgrade Node Prefab";
        private const string EdgesLayerName = "Edges Layer";
        private const string NodesLayerName = "Nodes Layer";

        [Header("Layout")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform contentRoot;

        private readonly List<GameObject> runtimeTreeObjects = new();
        private Coroutine refreshRoutine;
        private bool refreshQueued;
        private string resolvedBaseTitle = DefaultTitle;
        private AsyncOperationHandle<GameObject> upgradeNodePrefabHandle;
        private bool hasUpgradeNodePrefabHandle;
        private PlayerRemnantWallet observedWallet;

        public override Status currentStatus { get; } = StatusList.InUpgradeScreenStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            CacheBaseTitle();
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
            ClearRuntimeTree();
        }

        private void OnDestroy()
        {
            RemoveCurrentStatus();
            UnsubscribeWalletChanges();
            StopRefreshRoutine();
            ClearRuntimeTree();
            ReleaseUpgradeNodePrefabHandle();
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
        /// summary: 协程式刷新科技树，等待目录与节点 prefab 都就绪后再重建内容。
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
                UpdateTitleWithRemnants();
                ClearRuntimeTree();

                if (contentRoot == null)
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
                yield return EnsureUpgradeNodePrefabLoadedCo();

                if (!upgradeService.HasCatalogLoaded || !TryGetUpgradeNodePrefab(out GameObject nodePrefab))
                {
                    refreshRoutine = null;
                    yield break;
                }

                BuildTree(upgradeService, nodePrefab);
                UpdateTitleWithRemnants();
            }
            while (refreshQueued);

            refreshRoutine = null;
        }

        private void BuildTree(PermanentUpgradeService upgradeService, GameObject nodePrefab)
        {
            if (contentRoot == null || upgradeService == null || nodePrefab == null)
            {
                return;
            }

            ClearRuntimeTree();
            List<PermanentUpgradeEntryData> entries = FlattenEntries(upgradeService.GetSections());
            Vector2 canvasSize = ResolveCanvasSize(upgradeService.GetCanvasSize(), entries);
            ConfigureContentRoot(canvasSize);

            RectTransform edgesLayer = CreateRuntimeLayer(EdgesLayerName, canvasSize);
            RectTransform nodesLayer = CreateRuntimeLayer(NodesLayerName, canvasSize);
            Dictionary<string, RectTransform> nodeRectByEntryId = new(System.StringComparer.Ordinal);

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                PermanentUpgradeEntryData entry = entries[entryIndex];
                if (entry == null)
                {
                    continue;
                }

                RectTransform nodeRect = CreateNode(nodesLayer, nodePrefab, entryIndex, entry, upgradeService);
                if (nodeRect != null)
                {
                    nodeRectByEntryId[entry.Id] = nodeRect;
                }
            }

            BuildEdges(upgradeService.GetEdges(), entries, edgesLayer, nodeRectByEntryId);
        }

        private RectTransform CreateNode(
            RectTransform nodesLayer,
            GameObject nodePrefab,
            int entryIndex,
            PermanentUpgradeEntryData entry,
            PermanentUpgradeService upgradeService)
        {
            GameObject nodeObject = Instantiate(nodePrefab, nodesLayer, false);
            nodeObject.name = $"Upgrade Node {entryIndex + 1:D2} - {entry.Id}";
            RectTransform nodeRect = nodeObject.GetComponent<RectTransform>();
            if (nodeRect == null)
            {
                nodeRect = nodeObject.AddComponent<RectTransform>();
            }

            PermanentUpgradeVector2Data position = entry.Position ?? new PermanentUpgradeVector2Data();
            PermanentUpgradeVector2Data size = entry.Size ?? new PermanentUpgradeVector2Data { X = 100f, Y = 100f };
            nodeRect.anchorMin = new Vector2(0f, 1f);
            nodeRect.anchorMax = new Vector2(0f, 1f);
            nodeRect.pivot = new Vector2(0f, 1f);
            nodeRect.anchoredPosition = new Vector2(position.X, -position.Y);
            nodeRect.sizeDelta = new Vector2(Mathf.Max(1f, size.X), Mathf.Max(1f, size.Y));

            UpgradeNodeView nodeView = nodeObject.GetComponent<UpgradeNodeView>();
            if (nodeView == null)
            {
                nodeView = nodeObject.AddComponent<UpgradeNodeView>();
            }

            int currentLevel = upgradeService.GetPurchasedLevel(entry.Id);
            int currentRemnants = PlayerRemnantWallet.GetCurrentRemnants();
            bool prerequisitesMet = upgradeService.HasPrerequisitesMet(entry.Id);
            nodeView.Bind(entry, currentLevel, currentRemnants, prerequisitesMet, HandleUpgradeNodeClicked);
            runtimeTreeObjects.Add(nodeObject);
            return nodeRect;
        }

        private void BuildEdges(
            IReadOnlyList<PermanentUpgradeEdgeData> edges,
            IReadOnlyList<PermanentUpgradeEntryData> entries,
            RectTransform edgesLayer,
            Dictionary<string, RectTransform> nodeRectByEntryId)
        {
            if (edges == null || edgesLayer == null || nodeRectByEntryId == null)
            {
                return;
            }

            Dictionary<string, PermanentUpgradeEntryData> entryById = BuildEntryLookup(entries);
            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                PermanentUpgradeEdgeData edge = edges[edgeIndex];
                if (edge == null
                    || !entryById.TryGetValue(edge.From, out PermanentUpgradeEntryData fromEntry)
                    || !entryById.TryGetValue(edge.To, out PermanentUpgradeEntryData toEntry)
                    || !nodeRectByEntryId.ContainsKey(edge.From)
                    || !nodeRectByEntryId.ContainsKey(edge.To))
                {
                    continue;
                }

                Vector2 from = GetNodeCenter(fromEntry);
                Vector2 to = GetNodeCenter(toEntry);
                Vector2 delta = to - from;
                float distance = delta.magnitude;
                if (distance <= 0.001f)
                {
                    continue;
                }

                GameObject edgeObject = new($"Upgrade Edge {edgeIndex + 1:D2} - {edge.From} to {edge.To}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                edgeObject.transform.SetParent(edgesLayer, false);
                runtimeTreeObjects.Add(edgeObject);

                RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
                edgeRect.anchorMin = new Vector2(0f, 1f);
                edgeRect.anchorMax = new Vector2(0f, 1f);
                edgeRect.pivot = new Vector2(0.5f, 0.5f);
                edgeRect.anchoredPosition = (from + to) * 0.5f;
                edgeRect.sizeDelta = new Vector2(distance, Mathf.Max(1f, edge.Width));
                edgeRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

                Image image = edgeObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = ColorUtility.TryParseHtmlString(edge.Color, out Color parsedColor)
                    ? parsedColor
                    : new Color(0.4f, 0.89f, 0.37f, 1f);
            }
        }

        private void HandleUpgradeNodeClicked(string entryId)
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
                    if (ui != null)
                    {
                        StartCoroutine(PopUpUIUtility.ShowInfoPopup(ui, nameof(UpdateUIScreen), purchaseResult.Message));
                    }
                    else
                    {
                        GameDebug.LogWarning($"[UpdateUIScreen] {purchaseResult.Message}");
                    }
                }

                QueueRefreshContent();
                return;
            }

            QueueRefreshContent();
        }

        private IEnumerator EnsureUpgradeNodePrefabLoadedCo()
        {
            if (TryGetUpgradeNodePrefab(out _))
            {
                yield break;
            }

            if (hasUpgradeNodePrefabHandle)
            {
                yield break;
            }

            upgradeNodePrefabHandle = Addressables.LoadAssetAsync<GameObject>(UpgradeNodePrefabAddress);
            hasUpgradeNodePrefabHandle = true;
            yield return upgradeNodePrefabHandle;

            if (upgradeNodePrefabHandle.Status != AsyncOperationStatus.Succeeded || upgradeNodePrefabHandle.Result == null)
            {
                GameDebug.LogError($"[UpdateUIScreen] Failed to load upgrade node prefab at '{UpgradeNodePrefabAddress}'.");
                ReleaseUpgradeNodePrefabHandle();
            }
        }

        private bool TryGetUpgradeNodePrefab(out GameObject prefab)
        {
            prefab = null;
            if (!hasUpgradeNodePrefabHandle)
            {
                return false;
            }

            if (!upgradeNodePrefabHandle.IsValid() || upgradeNodePrefabHandle.Status != AsyncOperationStatus.Succeeded)
            {
                return false;
            }

            prefab = upgradeNodePrefabHandle.Result;
            return prefab != null;
        }

        private void ReleaseUpgradeNodePrefabHandle()
        {
            if (!hasUpgradeNodePrefabHandle)
            {
                return;
            }

            if (upgradeNodePrefabHandle.IsValid())
            {
                Addressables.Release(upgradeNodePrefabHandle);
            }

            upgradeNodePrefabHandle = default;
            hasUpgradeNodePrefabHandle = false;
        }

        private RectTransform CreateRuntimeLayer(string layerName, Vector2 canvasSize)
        {
            GameObject layerObject = new(layerName, typeof(RectTransform));
            layerObject.transform.SetParent(contentRoot, false);
            runtimeTreeObjects.Add(layerObject);

            RectTransform layerRect = layerObject.GetComponent<RectTransform>();
            layerRect.anchorMin = new Vector2(0f, 1f);
            layerRect.anchorMax = new Vector2(0f, 1f);
            layerRect.pivot = new Vector2(0f, 1f);
            layerRect.anchoredPosition = Vector2.zero;
            layerRect.sizeDelta = canvasSize;
            return layerRect;
        }

        private void ConfigureContentRoot(Vector2 canvasSize)
        {
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.sizeDelta = canvasSize;
        }

        private static Vector2 ResolveCanvasSize(
            PermanentUpgradeVector2Data configuredCanvasSize,
            IReadOnlyList<PermanentUpgradeEntryData> entries)
        {
            float width = configuredCanvasSize != null ? Mathf.Max(1f, configuredCanvasSize.X) : 1800f;
            float height = configuredCanvasSize != null ? Mathf.Max(1f, configuredCanvasSize.Y) : 1200f;

            if (entries != null)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    PermanentUpgradeEntryData entry = entries[index];
                    if (entry == null)
                    {
                        continue;
                    }

                    PermanentUpgradeVector2Data position = entry.Position ?? new PermanentUpgradeVector2Data();
                    PermanentUpgradeVector2Data size = entry.Size ?? new PermanentUpgradeVector2Data { X = 100f, Y = 100f };
                    width = Mathf.Max(width, position.X + size.X);
                    height = Mathf.Max(height, position.Y + size.Y);
                }
            }

            return new Vector2(width, height);
        }

        private static Vector2 GetNodeCenter(PermanentUpgradeEntryData entry)
        {
            PermanentUpgradeVector2Data position = entry.Position ?? new PermanentUpgradeVector2Data();
            PermanentUpgradeVector2Data size = entry.Size ?? new PermanentUpgradeVector2Data { X = 100f, Y = 100f };
            return new Vector2(position.X + size.X * 0.5f, -(position.Y + size.Y * 0.5f));
        }

        private static List<PermanentUpgradeEntryData> FlattenEntries(IReadOnlyList<PermanentUpgradeSectionData> sections)
        {
            List<PermanentUpgradeEntryData> entries = new();
            if (sections == null)
            {
                return entries;
            }

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                PermanentUpgradeSectionData section = sections[sectionIndex];
                if (section?.Entries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
                {
                    PermanentUpgradeEntryData entry = section.Entries[entryIndex];
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static Dictionary<string, PermanentUpgradeEntryData> BuildEntryLookup(IReadOnlyList<PermanentUpgradeEntryData> entries)
        {
            Dictionary<string, PermanentUpgradeEntryData> entryById = new(System.StringComparer.Ordinal);
            if (entries == null)
            {
                return entryById;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                PermanentUpgradeEntryData entry = entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                {
                    entryById[entry.Id] = entry;
                }
            }

            return entryById;
        }

        private void TryAutoBindReferences()
        {
            panelRoot ??= transform.Find("Panel") as RectTransform;
            if (panelRoot == null)
            {
                return;
            }

            titleText ??= panelRoot.Find("Titile/Text (TMP)")?.GetComponent<TMP_Text>();
            RectTransform activeTreeContent = panelRoot.Find("Main Content/Content/Scroll View/Viewport/Content") as RectTransform;
            if (activeTreeContent != null)
            {
                contentRoot = activeTreeContent;
                return;
            }

            contentRoot ??= panelRoot.Find("Main Content/Scroll View/Viewport/Content") as RectTransform;
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

        private void ClearRuntimeTree()
        {
            RemoveExistingRuntimeLayer(EdgesLayerName);
            RemoveExistingRuntimeLayer(NodesLayerName);

            for (int index = runtimeTreeObjects.Count - 1; index >= 0; index--)
            {
                GameObject runtimeObject = runtimeTreeObjects[index];
                if (runtimeObject != null)
                {
                    DestroyChild(runtimeObject);
                }
            }

            runtimeTreeObjects.Clear();
        }

        private void RemoveExistingRuntimeLayer(string layerName)
        {
            if (contentRoot == null)
            {
                return;
            }

            Transform existingLayer = contentRoot.Find(layerName);
            if (existingLayer != null)
            {
                DestroyChild(existingLayer.gameObject);
            }
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
