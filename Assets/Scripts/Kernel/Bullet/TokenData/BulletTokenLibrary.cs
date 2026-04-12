using System;
using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.Bullet
{
    /// <summary>
    /// 维护一份用于 Token Select 弹窗的可选 token 库。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Bullet Token Library", fileName = "BulletTokenLibrary")]
    public sealed class BulletTokenLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class TokenWeightEntry
        {
            [SerializeField] private PlaceableTokenData token;
            [SerializeField, Min(0f)] private float drawWeight = 1f;

            /// <summary>
            /// summary: 当前条目指向的 token。
            /// param: 无
            /// return: token 引用
            /// </summary>
            public PlaceableTokenData Token => token;

            /// <summary>
            /// summary: 当前条目的抽取权重（相对概率）。
            /// param: 无
            /// return: 抽取权重
            /// </summary>
            public float DrawWeight => drawWeight;

            /// <summary>
            /// summary: 设置当前条目的 token 与权重。
            /// param name="tokenData": token 引用
            /// param name="weight": 抽取权重
            /// return: 无
            /// </summary>
            public void Set(PlaceableTokenData tokenData, float weight)
            {
                token = tokenData;
                drawWeight = NormalizeWeight(weight);
            }
        }

        [SerializeField, HideInInspector] private List<PlaceableTokenData> selectableTokens = new();
        [SerializeField] private List<TokenWeightEntry> tokenWeights = new();

        /// <summary>
        /// summary: 返回当前库中已清理后的可选 token 列表。
        /// param: 无
        /// return: 只读 token 列表
        /// </summary>
        public IReadOnlyList<PlaceableTokenData> SelectableTokens
        {
            get
            {
                SanitizeEntries(removeNull: true);
                RebuildSelectableTokenCache();
                return selectableTokens;
            }
        }

        /// <summary>
        /// summary: 返回当前库中可配置的 token 权重条目。
        /// param: 无
        /// return: token 权重条目只读列表
        /// </summary>
        public IReadOnlyList<TokenWeightEntry> TokenWeights
        {
            get
            {
                SanitizeEntries(removeNull: false);
                return tokenWeights;
            }
        }

        /// <summary>
        /// summary: 用一组新的 token 集合替换当前库内容，并自动去重。
        /// param name="tokens": 需要写入库中的 token 集合
        /// return: 无
        /// </summary>
        public void SetTokens(IEnumerable<PlaceableTokenData> tokens)
        {
            tokenWeights ??= new List<TokenWeightEntry>();
            tokenWeights.Clear();
            AppendUniqueTokens(tokens, defaultWeight: 1f);
            RebuildSelectableTokenCache();
        }

        /// <summary>
        /// summary: 向当前库补充一个 token；若已存在则忽略。
        /// param name="token": 需要加入库中的 token
        /// return: 无
        /// </summary>
        public void AddToken(PlaceableTokenData token)
        {
            AddToken(token, drawWeight: 1f);
        }

        /// <summary>
        /// summary: 向当前库补充一个 token 与其抽取权重；若已存在则忽略。
        /// param name="token": 需要加入库中的 token
        /// param name="drawWeight": token 抽取权重
        /// return: 无
        /// </summary>
        public void AddToken(PlaceableTokenData token, float drawWeight)
        {
            if (token == null)
            {
                return;
            }

            SanitizeEntries(removeNull: false);
            if (!TryAddEntry(token, drawWeight, overwriteExistingWeight: false))
            {
                return;
            }

            RebuildSelectableTokenCache();
        }

        /// <summary>
        /// summary: 设置某个 token 的抽取权重；若当前不存在则会自动加入。
        /// param name="token": 目标 token
        /// param name="drawWeight": 新的抽取权重
        /// return: 无
        /// </summary>
        public void SetTokenWeight(PlaceableTokenData token, float drawWeight)
        {
            if (token == null)
            {
                return;
            }

            SanitizeEntries(removeNull: false);
            TokenWeightEntry existing = FindEntry(token);
            if (existing == null)
            {
                existing = new TokenWeightEntry();
                tokenWeights.Add(existing);
            }

            existing.Set(token, drawWeight);
            RebuildSelectableTokenCache();
        }

        /// <summary>
        /// summary: 读取某个 token 当前配置的抽取权重。
        /// param name="token": 目标 token
        /// return: 抽取权重；找不到时返回 0
        /// </summary>
        public float GetTokenWeight(PlaceableTokenData token)
        {
            if (token == null)
            {
                return 0f;
            }

            SanitizeEntries(removeNull: false);
            TokenWeightEntry entry = FindEntry(token);
            return entry != null ? NormalizeWeight(entry.DrawWeight) : 0f;
        }

        /// <summary>
        /// summary: 按 token 权重进行不重复抽样；desiredCount 小于等于 0 时按 minCount-maxCount 随机决定数量。
        /// param name="random": 随机源
        /// param name="desiredCount": 指定抽样数量
        /// param name="minCount": 随机抽样最小数量
        /// param name="maxCount": 随机抽样最大数量
        /// return: 抽样结果列表
        /// </summary>
        public List<PlaceableTokenData> SampleChoices(
            VocalithRandom random = null,
            int desiredCount = -1,
            int minCount = 1,
            int maxCount = 3)
        {
            SanitizeEntries(removeNull: true);
            List<WeightedCandidate> candidates = CollectWeightedCandidates();
            if (candidates.Count <= 0)
            {
                return new List<PlaceableTokenData>();
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            int targetCount = desiredCount > 0
                ? desiredCount
                : DetermineChoiceCount(candidates.Count, rng, minCount, maxCount);

            targetCount = Mathf.Clamp(targetCount, 0, candidates.Count);
            if (targetCount <= 0)
            {
                return new List<PlaceableTokenData>();
            }

            List<PlaceableTokenData> sampledTokens = new(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                int selectedIndex = PickWeightedIndex(candidates, rng);
                sampledTokens.Add(candidates[selectedIndex].Token);
                candidates.RemoveAt(selectedIndex);
            }

            return sampledTokens;
        }

        /// <summary>
        /// summary: 返回当前库中的 token 视图，供 UI 采样器直接读取。
        /// param: 无
        /// return: 清理后的 token 只读列表
        /// </summary>
        public IReadOnlyList<PlaceableTokenData> GetTokens()
        {
            SanitizeEntries(removeNull: true);
            RebuildSelectableTokenCache();
            return selectableTokens;
        }

        /// <summary>
        /// summary: Inspector 修改时仅执行去重，保留空槽位以便手动赋值。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnValidate()
        {
            SanitizeEntries(removeNull: false);
            RebuildSelectableTokenCache();
        }

        /// <summary>
        /// summary: 追加一组 token，并自动跳过重复项。
        /// param name="tokens": 待追加的 token 集合
        /// return: 无
        /// </summary>
        private void AppendUniqueTokens(IEnumerable<PlaceableTokenData> tokens, float defaultWeight)
        {
            if (tokens == null)
            {
                return;
            }

            foreach (PlaceableTokenData token in tokens)
            {
                TryAddEntry(token, defaultWeight, overwriteExistingWeight: false);
            }
        }

        /// <summary>
        /// summary: 判断当前库中是否已存在指定 token。
        /// param name="token": 待检查的 token
        /// return: 是否已存在
        /// </summary>
        private bool ContainsToken(PlaceableTokenData token)
        {
            return FindEntry(token) != null;
        }

        /// <summary>
        /// summary: 清理列表中的重复项，并按需移除空引用。
        /// param name="removeNull": 是否删除空引用
        /// return: 无
        /// </summary>
        private void SanitizeEntries(bool removeNull)
        {
            selectableTokens ??= new List<PlaceableTokenData>();
            tokenWeights ??= new List<TokenWeightEntry>();
            MigrateLegacyTokensIfNeeded();

            HashSet<int> seenInstanceIds = new();

            for (int i = tokenWeights.Count - 1; i >= 0; i--)
            {
                TokenWeightEntry entry = tokenWeights[i];
                if (entry == null)
                {
                    if (removeNull)
                    {
                        tokenWeights.RemoveAt(i);
                    }

                    continue;
                }

                PlaceableTokenData token = entry.Token;
                if (token == null)
                {
                    if (removeNull)
                    {
                        tokenWeights.RemoveAt(i);
                    }

                    continue;
                }

                float normalizedWeight = NormalizeWeight(entry.DrawWeight);
                if (!Mathf.Approximately(entry.DrawWeight, normalizedWeight))
                {
                    entry.Set(token, normalizedWeight);
                }

                if (!seenInstanceIds.Add(token.GetInstanceID()))
                {
                    tokenWeights.RemoveAt(i);
                }
            }
        }

        private void RebuildSelectableTokenCache()
        {
            selectableTokens ??= new List<PlaceableTokenData>();
            selectableTokens.Clear();

            if (tokenWeights == null)
            {
                return;
            }

            HashSet<int> seenInstanceIds = new();
            for (int i = 0; i < tokenWeights.Count; i++)
            {
                TokenWeightEntry entry = tokenWeights[i];
                PlaceableTokenData token = entry != null ? entry.Token : null;
                if (token == null)
                {
                    continue;
                }

                if (seenInstanceIds.Add(token.GetInstanceID()))
                {
                    selectableTokens.Add(token);
                }
            }
        }

        private bool TryAddEntry(PlaceableTokenData token, float drawWeight, bool overwriteExistingWeight)
        {
            if (token == null)
            {
                return false;
            }

            tokenWeights ??= new List<TokenWeightEntry>();
            TokenWeightEntry existing = FindEntry(token);
            if (existing != null)
            {
                if (overwriteExistingWeight)
                {
                    existing.Set(token, drawWeight);
                }

                return false;
            }

            TokenWeightEntry entry = new TokenWeightEntry();
            entry.Set(token, drawWeight);
            tokenWeights.Add(entry);
            return true;
        }

        private TokenWeightEntry FindEntry(PlaceableTokenData token)
        {
            if (token == null || tokenWeights == null)
            {
                return null;
            }

            for (int i = 0; i < tokenWeights.Count; i++)
            {
                TokenWeightEntry entry = tokenWeights[i];
                if (entry != null && entry.Token == token)
                {
                    return entry;
                }
            }

            return null;
        }

        private void MigrateLegacyTokensIfNeeded()
        {
            if (tokenWeights == null || tokenWeights.Count > 0 || selectableTokens == null || selectableTokens.Count == 0)
            {
                return;
            }

            for (int i = 0; i < selectableTokens.Count; i++)
            {
                TryAddEntry(selectableTokens[i], drawWeight: 1f, overwriteExistingWeight: false);
            }
        }

        private List<WeightedCandidate> CollectWeightedCandidates()
        {
            List<WeightedCandidate> candidates = new();
            if (tokenWeights == null)
            {
                return candidates;
            }

            for (int i = 0; i < tokenWeights.Count; i++)
            {
                TokenWeightEntry entry = tokenWeights[i];
                PlaceableTokenData token = entry != null ? entry.Token : null;
                if (token == null)
                {
                    continue;
                }

                float normalizedWeight = NormalizeWeight(entry.DrawWeight);
                if (normalizedWeight <= 0f)
                {
                    continue;
                }

                candidates.Add(new WeightedCandidate(token, normalizedWeight));
            }

            return candidates;
        }

        private static int DetermineChoiceCount(int availableCount, VocalithRandom random, int minCount, int maxCount)
        {
            if (availableCount <= 0)
            {
                return 0;
            }

            int lower = Math.Min(minCount, maxCount);
            int upper = Math.Max(minCount, maxCount);
            VocalithRandom rng = random ?? new VocalithRandom();
            int selected = rng.Next(lower, upper + 1);
            if (selected < lower)
            {
                selected = lower;
            }

            if (selected > upper)
            {
                selected = upper;
            }

            return Math.Min(selected, availableCount);
        }

        private static int PickWeightedIndex(List<WeightedCandidate> candidates, VocalithRandom random)
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

        private static float NormalizeWeight(float weight)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                return 0f;
            }

            return Mathf.Max(0f, weight);
        }

        private sealed class WeightedCandidate
        {
            public WeightedCandidate(PlaceableTokenData token, float weight)
            {
                Token = token;
                Weight = weight;
            }

            public PlaceableTokenData Token { get; }
            public float Weight { get; }
        }
    }
}