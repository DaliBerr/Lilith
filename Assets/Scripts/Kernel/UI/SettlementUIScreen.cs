using System.Collections;
using System.Text;
using Kernel.GameState;
using Kernel.MapGrid;
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
    /// 对局结束后的结算主屏，负责展示胜负标题、收益列表和敌人统计。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Settlement UI Screen")]
    public sealed class SettlementUIScreen : GameUIScreen
    {
        private const string DefaultPresentationCatalogAddress = "Assets/Data/UI/SettlementPresentationCatalog";
        private static readonly Vocalith.Random RandomSource = new();

        [Header("Layout")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text harvestText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button closeButton;
        [SerializeField] private string presentationCatalogAddress = DefaultPresentationCatalogAddress;

        private SettlementPresentationCatalogData presentationCatalog;
        private bool hasLoadedPresentationCatalog;

        public override Status currentStatus { get; } = StatusList.InSettlementScreenStatus;

        public RectTransform PanelRoot => panelRoot;
        public TMP_Text TitleText => titleText;
        public TMP_Text ResultText => resultText;
        public TMP_Text HarvestText => harvestText;
        public TMP_Text SummaryText => summaryText;
        public Button CloseButton => closeButton;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindCloseButton();
        }

        public override IEnumerator Show(float fade = 0.15f)
        {
            EnsureCurrentStatus();
            yield return EnsurePresentationCatalogLoadedCo();
            ApplyPendingSnapshot();
            yield return base.Show(fade);
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindCloseButton();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 供未来 prefab 上的关闭按钮复用统一的结算页关闭入口。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            UIInputRouter.Instance?.RequestCloseSettlementScreen();
        }

        private void TryAutoBindReferences()
        {
            panelRoot ??= transform.Find("Settlement Panel") as RectTransform;
            if (panelRoot == null)
            {
                return;
            }

            titleText ??= panelRoot.Find("Top Panel/Tittle")?.GetComponent<TMP_Text>();
            resultText ??= panelRoot.Find("Main Content Panel/Result Content/Result")?.GetComponent<TMP_Text>();
            harvestText ??= panelRoot.Find("Main Content Panel/Permanent Thing/Harvest")?.GetComponent<TMP_Text>();
            summaryText ??= panelRoot.Find("Main Content Panel/Permanent Thing/Summarize")?.GetComponent<TMP_Text>();
            closeButton ??= ResolveOptionalCloseButton(panelRoot);
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

        private IEnumerator EnsurePresentationCatalogLoadedCo()
        {
            if (hasLoadedPresentationCatalog)
            {
                yield break;
            }

            presentationCatalog = SettlementPresentationCatalogUtility.CreateDefault();
            hasLoadedPresentationCatalog = true;
            if (string.IsNullOrWhiteSpace(presentationCatalogAddress))
            {
                yield break;
            }

            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(presentationCatalogAddress.Trim());
            yield return handle;

            try
            {
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    GameDebug.LogWarning($"[SettlementUIScreen] Failed to load presentation catalog at '{presentationCatalogAddress}'.");
                    yield break;
                }

                if (!SettlementPresentationCatalogUtility.TryDeserializeCatalogJson(
                        handle.Result.text,
                        out SettlementPresentationCatalogData parsedCatalog,
                        out string errorMessage))
                {
                    GameDebug.LogWarning($"[SettlementUIScreen] {errorMessage}");
                    yield break;
                }

                presentationCatalog = parsedCatalog;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private void ApplyPendingSnapshot()
        {
            MapRunFlowController flowController = FindFirstObjectByType<MapRunFlowController>();
            if (flowController == null || !flowController.TryGetSettlementSnapshot(out SettlementSnapshot snapshot))
            {
                ApplyPresentation(SettlementSnapshot.Empty, presentationCatalog ?? SettlementPresentationCatalogUtility.CreateDefault());
                return;
            }

            ApplyPresentation(snapshot, presentationCatalog ?? SettlementPresentationCatalogUtility.CreateDefault());
        }

        private void ApplyPresentation(SettlementSnapshot snapshot, SettlementPresentationCatalogData catalog)
        {
            SettlementPresentationCatalogData resolvedCatalog = SettlementPresentationCatalogUtility.Sanitize(catalog);
            if (titleText != null)
            {
                titleText.text = ResolveRandomTitle(snapshot.Outcome, resolvedCatalog);
            }

            if (resultText != null)
            {
                string template = snapshot.Outcome == SettlementOutcome.Victory
                    ? resolvedCatalog.VictoryResultTemplate
                    : resolvedCatalog.DefeatResultTemplate;
                resultText.text = template
                    .Replace("{waves}", snapshot.CompletedWaveCount.ToString())
                    .Replace("{bosses}", snapshot.DefeatedBossCount.ToString());
            }

            if (harvestText != null)
            {
                harvestText.text = BuildCountBlock(
                    resolvedCatalog.HarvestHeader,
                    resolvedCatalog.HarvestEmptyText,
                    snapshot.HarvestEntries);
            }

            if (summaryText != null)
            {
                summaryText.text = BuildCountBlock(
                    resolvedCatalog.SummaryHeader,
                    resolvedCatalog.SummaryEmptyText,
                    snapshot.SummaryEntries);
            }
        }

        private static string ResolveRandomTitle(SettlementOutcome outcome, SettlementPresentationCatalogData catalog)
        {
            if (catalog == null)
            {
                catalog = SettlementPresentationCatalogUtility.CreateDefault();
            }

            var titlePool = outcome == SettlementOutcome.Victory ? catalog.VictoryTitles : catalog.DefeatTitles;
            if (titlePool == null || titlePool.Count <= 0)
            {
                titlePool = outcome == SettlementOutcome.Victory
                    ? SettlementPresentationCatalogUtility.CreateDefault().VictoryTitles
                    : SettlementPresentationCatalogUtility.CreateDefault().DefeatTitles;
            }

            return titlePool[RandomSource.Next(0, titlePool.Count)];
        }

        private static string BuildCountBlock(string header, string emptyText, System.Collections.Generic.IReadOnlyList<SettlementCountEntry> entries)
        {
            StringBuilder builder = new();
            builder.Append(string.IsNullOrWhiteSpace(header) ? string.Empty : header.Trim());
            if (entries == null || entries.Count <= 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                    builder.Append('\t');
                }

                builder.Append(string.IsNullOrWhiteSpace(emptyText)
                    ? LocalizationManager.TranslateOrDefault("ui.common.none", "无")
                    : emptyText.Trim());
                return builder.ToString();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SettlementCountEntry entry = entries[i];
                if (builder.Length > 0 || i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append('\t');
                builder.Append(entry.DisplayName);
                builder.Append(" * ");
                builder.Append(entry.Count);
            }

            return builder.ToString();
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private static Button ResolveOptionalCloseButton(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform namedCloseButton = root.Find("Top Panel/Close Button")
                ?? root.Find("Close Button")
                ?? root.Find("Close");
            if (namedCloseButton != null)
            {
                Button button = namedCloseButton.GetComponent<Button>();
                return button != null ? button : namedCloseButton.GetComponentInChildren<Button>(true);
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate != null && candidate.name.ToLowerInvariant().Contains("close"))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
