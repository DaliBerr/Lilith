using System;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.UI
{
    /// <summary>
    /// Token Select 弹窗屏幕：从 BulletTokenLibrary 中默认抽样 3 个唯一 token 并生成可点击卡片。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Token Select Panel")]
    public sealed class TokenSelectUIScreen : GameUIScreen
    {
        private enum SelectionOutcome
        {
            None = 0,
            Selected = 1,
            Cancelled = 2,
        }

        [Header("Layout")]
        [SerializeField] private RectTransform mainContent;

        [Header("Data")]
        [SerializeField] private BulletTokenLibrary bulletTokenLibrary;
        [SerializeField] private SpellBookRewardLibrary spellBookRewardLibrary;
        [SerializeField] private BulletTokenSelectionView selectionPrefab;

        private readonly List<BulletTokenSelectionView> runtimeCards = new();
        private Action<PlaceableTokenData> selectedCallback;
        private Action<RunRewardOption> selectedRewardCallback;
        private Action cancelledCallback;
        private SelectionOutcome pendingOutcome = SelectionOutcome.None;
        private RunRewardOption pendingSelection;
        private VocalithRandom selectionRandom;
        private int choiceCountOverride = 3;
        private bool hasInitialized;
        private bool hasResolvedOutcome;
        private bool hasLoggedConfigurationWarning;

        public override Status currentStatus { get; } = StatusList.PopUpStatus;

        /// <summary>
        /// summary: 当前 Token Select 面板的主内容容器。
        /// param: 无
        /// returns: Main Content 根节点
        /// </summary>
        public RectTransform MainContent => mainContent;

        /// <summary>
        /// summary: 当前绑定的 BulletToken 库。
        /// param: 无
        /// returns: BulletTokenLibrary 资产引用
        /// </summary>
        public BulletTokenLibrary BulletTokenLibrary => bulletTokenLibrary;

        public SpellBookRewardLibrary SpellBookRewardLibrary => spellBookRewardLibrary;

        /// <summary>
        /// summary: 当前使用的卡片 prefab 引用。
        /// param: 无
        /// returns: BulletTokenSelectionView prefab 引用
        /// </summary>
        public BulletTokenSelectionView SelectionPrefab => selectionPrefab;

        /// <summary>
        /// summary: 当前运行时生成的卡片实例集合。
        /// param: 无
        /// returns: 运行时卡片只读列表
        /// </summary>
        public IReadOnlyList<BulletTokenSelectionView> RuntimeCards => runtimeCards;

        protected override void OnInit()
        {
            hasInitialized = true;
            TryAutoBindReferences();
            RefreshChoices();
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
            try
            {
                FinalizeOutcomeIfNeeded();
            }
            finally
            {
                ClearRuntimeCards();
            }
        }

        private void OnDestroy()
        {
            ClearRuntimeCards();
            RemoveCurrentStatus();
            selectedCallback = null;
            selectedRewardCallback = null;
            cancelledCallback = null;
            pendingSelection = RunRewardOption.None;
            pendingOutcome = SelectionOutcome.None;
            hasResolvedOutcome = false;
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Token Select Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 配置当前弹窗的回调；UI 样式与数据源由 prefab 序列化字段提供。
        /// param name="onSelected": 选择某个 token 后执行的回调
        /// param name="onCancelled": 用户关闭弹窗时执行的可选回调
        /// returns: 无
        /// </summary>
        public void SetCallbacks(Action<PlaceableTokenData> onSelected, Action onCancelled = null)
        {
            selectedCallback = onSelected;
            selectedRewardCallback = null;
            cancelledCallback = onCancelled;
        }

        public void SetRewardCallbacks(Action<RunRewardOption> onSelected, Action onCancelled = null)
        {
            selectedCallback = null;
            selectedRewardCallback = onSelected;
            cancelledCallback = onCancelled;
        }

        /// <summary>
        /// summary: 替换当前弹窗使用的 token 库，并在已初始化时立即重建选项。
        /// param name="library": 新的 BulletToken 库
        /// returns: 无
        /// </summary>
        public void SetBulletTokenLibrary(BulletTokenLibrary library)
        {
            bulletTokenLibrary = library;
            RefreshIfInitialized();
        }

        public void SetSpellBookRewardLibrary(SpellBookRewardLibrary library)
        {
            spellBookRewardLibrary = library;
            RefreshIfInitialized();
        }

        public void SetRewardLibraries(BulletTokenLibrary tokenLibrary, SpellBookRewardLibrary bookLibrary)
        {
            bulletTokenLibrary = tokenLibrary;
            spellBookRewardLibrary = bookLibrary;
            RefreshIfInitialized();
        }

        /// <summary>
        /// summary: 替换当前弹窗使用的卡片 prefab，并在已初始化时立即重建选项。
        /// param name="prefab": 新的卡片 prefab
        /// returns: 无
        /// </summary>
        public void SetSelectionPrefab(BulletTokenSelectionView prefab)
        {
            selectionPrefab = prefab;
            RefreshIfInitialized();
        }

        /// <summary>
        /// summary: 覆盖当前弹窗的抽样数量；传入小于等于 0 的值会恢复为默认 3 抽样。
        /// param name="choiceCount": 需要固定展示的卡片数量
        /// returns: 无
        /// </summary>
        public void SetChoiceCountOverride(int choiceCount)
        {
            choiceCountOverride = choiceCount > 0 ? choiceCount : 3;
            RefreshIfInitialized();
        }

        /// <summary>
        /// summary: 覆盖当前弹窗使用的随机源，便于测试或特殊脚本重放。
        /// param name="random": 新的随机源；为空时保留当前随机源
        /// returns: 无
        /// </summary>
        public void SetSelectionRandom(VocalithRandom random)
        {
            selectionRandom = random;
            RefreshIfInitialized();
        }

        /// <summary>
        /// summary: 选择某个 token 并请求关闭当前弹窗；若当前没有 UIManager，则会立即回调。
        /// param name="token": 需要确认选择的 token
        /// returns: 无
        /// </summary>
        public void RequestSelection(PlaceableTokenData token)
        {
            RequestSelection(RunRewardOption.FromToken(token));
        }

        public void RequestSelection(RunRewardOption reward)
        {
            if (!reward.IsValid || hasResolvedOutcome)
            {
                return;
            }

            pendingSelection = reward;
            pendingOutcome = SelectionOutcome.Selected;
            RequestClose();
        }

        /// <summary>
        /// summary: 请求关闭当前弹窗；未发生选择时会走取消回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            if (hasResolvedOutcome)
            {
                return;
            }

            if (ui == null)
            {
                FinalizeOutcomeIfNeeded();
                ClearRuntimeCards();
                return;
            }

            if (ui.GetTopModal() == this)
            {
                ui.CloseTopModal();
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
                return;
            }

            FinalizeOutcomeIfNeeded();
            ClearRuntimeCards();
        }

        private void RefreshIfInitialized()
        {
            if (hasInitialized)
            {
                RefreshChoices();
            }
        }

        private void RefreshChoices()
        {
            TryAutoBindReferences();
            ClearRuntimeCards();

            if (mainContent == null)
            {
                LogMissingConfiguration("Main Content container is missing.");
                return;
            }

            if (selectionPrefab == null)
            {
                LogMissingConfiguration("Selection prefab is missing.");
                return;
            }

            if (bulletTokenLibrary == null && spellBookRewardLibrary == null)
            {
                LogMissingConfiguration("BulletTokenLibrary is missing.");
                return;
            }

            List<RunRewardOption> sampledRewards = SampleRewardChoices();
            if (sampledRewards == null || sampledRewards.Count <= 0)
            {
                LogMissingConfiguration("No valid rewards were sampled.");
                return;
            }

            hasLoggedConfigurationWarning = false;
            pendingOutcome = SelectionOutcome.None;
            pendingSelection = RunRewardOption.None;
            hasResolvedOutcome = false;

            for (int i = 0; i < sampledRewards.Count; i++)
            {
                RunRewardOption reward = sampledRewards[i];
                if (!reward.IsValid)
                {
                    continue;
                }

                BulletTokenSelectionView card = Instantiate(selectionPrefab, mainContent, false);
                card.name = $"Token Choice {i + 1:D2}";
                card.Bind(this, reward);
                runtimeCards.Add(card);
            }
        }

        private List<RunRewardOption> SampleRewardChoices()
        {
            VocalithRandom rng = selectionRandom ??= CreateDefaultRandom();
            int desiredCount = choiceCountOverride > 0 ? choiceCountOverride : 3;
            List<WeightedRewardCandidate> candidates = CollectWeightedRewardCandidates();
            if (candidates.Count <= 0)
            {
                return new List<RunRewardOption>();
            }

            int targetCount = Mathf.Clamp(desiredCount, 0, candidates.Count);
            if (targetCount <= 0)
            {
                return new List<RunRewardOption>();
            }

            List<RunRewardOption> sampledRewards = new(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                int selectedIndex = PickWeightedIndex(candidates, rng);
                sampledRewards.Add(candidates[selectedIndex].Reward);
                candidates.RemoveAt(selectedIndex);
            }

            return sampledRewards;
        }

        private List<WeightedRewardCandidate> CollectWeightedRewardCandidates()
        {
            List<WeightedRewardCandidate> candidates = new();
            HashSet<int> seenObjectIds = new();

            if (bulletTokenLibrary != null)
            {
                IReadOnlyList<BulletTokenLibrary.TokenWeightEntry> tokenWeights = bulletTokenLibrary.TokenWeights;
                for (int i = 0; i < tokenWeights.Count; i++)
                {
                    PlaceableTokenData token = tokenWeights[i] != null ? tokenWeights[i].Token : null;
                    float drawWeight = tokenWeights[i] != null ? Mathf.Max(0f, tokenWeights[i].DrawWeight) : 0f;
                    if (token == null || drawWeight <= 0f || !seenObjectIds.Add(token.GetInstanceID()))
                    {
                        continue;
                    }

                    candidates.Add(new WeightedRewardCandidate(RunRewardOption.FromToken(token), drawWeight));
                }
            }

            if (spellBookRewardLibrary != null)
            {
                IReadOnlyList<SpellBookRewardLibrary.SpellBookWeightEntry> spellBookWeights = spellBookRewardLibrary.SpellBookWeights;
                for (int i = 0; i < spellBookWeights.Count; i++)
                {
                    SpellBookData spellBook = spellBookWeights[i] != null ? spellBookWeights[i].SpellBook : null;
                    float drawWeight = spellBookWeights[i] != null ? Mathf.Max(0f, spellBookWeights[i].DrawWeight) : 0f;
                    if (spellBook == null || drawWeight <= 0f || !seenObjectIds.Add(spellBook.GetInstanceID()))
                    {
                        continue;
                    }

                    candidates.Add(new WeightedRewardCandidate(RunRewardOption.FromSpellBook(spellBook), drawWeight));
                }
            }

            return candidates;
        }

        private void FinalizeOutcomeIfNeeded()
        {
            if (hasResolvedOutcome)
            {
                return;
            }

            hasResolvedOutcome = true;
            SelectionOutcome resolvedOutcome = pendingOutcome;
            if (resolvedOutcome == SelectionOutcome.None)
            {
                resolvedOutcome = SelectionOutcome.Cancelled;
            }

            try
            {
                if (resolvedOutcome == SelectionOutcome.Selected)
                {
                    selectedRewardCallback?.Invoke(pendingSelection);
                    if (selectedCallback != null && pendingSelection.Kind == RunRewardOptionKind.Token)
                    {
                        selectedCallback.Invoke(pendingSelection.Token);
                    }
                }
                else
                {
                    cancelledCallback?.Invoke();
                }
            }
            finally
            {
                pendingOutcome = SelectionOutcome.None;
                pendingSelection = RunRewardOption.None;
                selectedCallback = null;
                selectedRewardCallback = null;
                cancelledCallback = null;
            }
        }

        private void ClearRuntimeCards()
        {
            for (int i = runtimeCards.Count - 1; i >= 0; i--)
            {
                BulletTokenSelectionView card = runtimeCards[i];
                DestroyChild(card != null ? card.gameObject : null);
            }

            runtimeCards.Clear();
        }

        private void TryAutoBindReferences()
        {
            mainContent ??= transform.Find("Main Content") as RectTransform;
        }

        private void LogMissingConfiguration(string reason)
        {
            if (hasLoggedConfigurationWarning)
            {
                return;
            }

            GameDebug.LogWarning($"[TokenSelectUIScreen] {reason}");
            hasLoggedConfigurationWarning = true;
        }

        private VocalithRandom CreateDefaultRandom()
        {
            return new VocalithRandom(unchecked(GetInstanceID() ^ Environment.TickCount));
        }

        private static int PickWeightedIndex(List<WeightedRewardCandidate> candidates, VocalithRandom random)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return 0;
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            double totalWeight = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += candidates[i].Weight;
            }

            if (totalWeight <= 0d)
            {
                return rng.Next(0, candidates.Count);
            }

            double roll = rng.NextDouble01() * totalWeight;
            double cumulativeWeight = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulativeWeight += candidates[i].Weight;
                if (roll <= cumulativeWeight)
                {
                    return i;
                }
            }

            return candidates.Count - 1;
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

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private readonly struct WeightedRewardCandidate
        {
            public WeightedRewardCandidate(RunRewardOption reward, float weight)
            {
                Reward = reward;
                Weight = weight;
            }

            public RunRewardOption Reward { get; }
            public float Weight { get; }
        }
    }
}
