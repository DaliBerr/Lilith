using System;
using System.Collections.Generic;
using Kernel.Bullet;
using VocalithRandom = Vocalith.Random;

namespace Kernel.UI
{
    /// <summary>
    /// 从 BulletToken 库中抽样出 1-3 个唯一 token 的辅助工具。
    /// </summary>
    public static class BulletTokenSelectionSampler
    {
        /// <summary>
        /// summary: 根据当前可用 token 数量和随机源，计算本轮应展示的卡片数量。
        /// param name="availableCount": 当前库中可用 token 的数量
        /// param name="random": 用于决定抽样数量的随机源
        /// param name="minCount": 最小抽样数量
        /// param name="maxCount": 最大抽样数量
        /// returns: 本轮应展示的卡片数量
        /// </summary>
        public static int DetermineChoiceCount(int availableCount, VocalithRandom random = null, int minCount = 1, int maxCount = 3)
        {
            if (availableCount <= 0)
            {
                return 0;
            }

            int lower = Math.Min(minCount, maxCount);
            int upper = Math.Max(minCount, maxCount);
            int selected = random != null ? random.Next(lower, upper + 1) : upper;
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

        /// <summary>
        /// summary: 从输入库中抽样出指定数量的唯一 token；当 desiredCount 小于等于 0 时，会自动抽 1-3 个。
        /// param name="source": 原始 token 库
        /// param name="random": 随机源
        /// param name="desiredCount": 指定的目标数量；小于等于 0 时走随机数量
        /// param name="minCount": 随机抽样的最小数量
        /// param name="maxCount": 随机抽样的最大数量
        /// returns: 去重并洗牌后的 token 列表
        /// </summary>
        public static List<PlaceableTokenData> SampleChoices(
            IReadOnlyList<PlaceableTokenData> source,
            VocalithRandom random = null,
            int desiredCount = -1,
            int minCount = 1,
            int maxCount = 3)
        {
            List<PlaceableTokenData> candidates = CollectUniqueCandidates(source);
            if (candidates.Count == 0)
            {
                return new List<PlaceableTokenData>();
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            int targetCount = desiredCount > 0
                ? desiredCount
                : DetermineChoiceCount(candidates.Count, rng, minCount, maxCount);

            targetCount = Math.Min(targetCount, candidates.Count);
            if (targetCount <= 0)
            {
                return new List<PlaceableTokenData>();
            }

            Shuffle(candidates, rng);
            if (targetCount >= candidates.Count)
            {
                return candidates;
            }

            return candidates.GetRange(0, targetCount);
        }

        private static List<PlaceableTokenData> CollectUniqueCandidates(IReadOnlyList<PlaceableTokenData> source)
        {
            List<PlaceableTokenData> candidates = new();
            if (source == null)
            {
                return candidates;
            }

            HashSet<int> seenInstanceIds = new();
            for (int i = 0; i < source.Count; i++)
            {
                PlaceableTokenData token = source[i];
                if (token == null)
                {
                    continue;
                }

                int instanceId = token.GetInstanceID();
                if (seenInstanceIds.Add(instanceId))
                {
                    candidates.Add(token);
                }
            }

            return candidates;
        }

        private static void Shuffle(List<PlaceableTokenData> candidates, VocalithRandom random)
        {
            if (candidates == null || candidates.Count <= 1)
            {
                return;
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(0, i + 1);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }
        }
    }
}
