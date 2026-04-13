using System;
using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.Bullet
{
    /// <summary>
    /// 定义一次 BulletTokenLibrary 抽取时可参与加权随机的候选库集合。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Selection Plan", fileName = "BulletTokenSelectionPlan")]
    public sealed class CombatEntryTokenSelectionPlan : ScriptableObject
    {
        [Serializable]
        public sealed class LibraryWeightEntry
        {
            [SerializeField] private BulletTokenLibrary library;
            [SerializeField, Min(0f)] private float selectionWeight = 1f;

            /// <summary>
            /// 获取当前候选条目指向的 BulletTokenLibrary。
            /// </summary>
            public BulletTokenLibrary Library => library;

            /// <summary>
            /// 获取当前候选条目的抽取权重。
            /// </summary>
            public float SelectionWeight => selectionWeight;

            /// <summary>
            /// 设置当前条目的库引用和抽取权重。
            /// </summary>
            /// <param name="nextLibrary">新的库引用。</param>
            /// <param name="weight">新的抽取权重。</param>
            public void Set(BulletTokenLibrary nextLibrary, float weight)
            {
                library = nextLibrary;
                selectionWeight = NormalizeWeight(weight);
            }

            internal void Sanitize()
            {
                selectionWeight = NormalizeWeight(selectionWeight);
            }
        }

        [SerializeField] private List<LibraryWeightEntry> libraryEntries = new();

        /// <summary>
        /// 获取当前计划中的全部候选库条目。
        /// </summary>
        public IReadOnlyList<LibraryWeightEntry> LibraryEntries
        {
            get
            {
                Sanitize();
                return libraryEntries;
            }
        }

        private void OnValidate()
        {
            Sanitize();
        }

        /// <summary>
        /// 向当前抽取计划追加一个候选库条目。
        /// </summary>
        /// <param name="library">候选库引用。</param>
        /// <param name="weight">该候选库的相对权重。</param>
        public void AddLibrary(BulletTokenLibrary library, float weight)
        {
            libraryEntries ??= new List<LibraryWeightEntry>();
            LibraryWeightEntry entry = new();
            entry.Set(library, weight);
            libraryEntries.Add(entry);
        }

        /// <summary>
        /// 清空并替换当前抽取计划的候选库列表。
        /// </summary>
        /// <param name="entries">新的候选库集合。</param>
        public void SetLibraries(IEnumerable<LibraryWeightEntry> entries)
        {
            libraryEntries = entries != null ? new List<LibraryWeightEntry>(entries) : new List<LibraryWeightEntry>();
            Sanitize();
        }

        /// <summary>
        /// 按权重从当前计划中抽取一个 BulletTokenLibrary。
        /// </summary>
        /// <param name="random">用于抽样的随机源。</param>
        /// <param name="library">输出抽中的库。</param>
        /// <returns>成功抽到有效库时返回 true。</returns>
        public bool TrySampleLibrary(VocalithRandom random, out BulletTokenLibrary library)
        {
            library = null;
            Sanitize();
            List<WeightedLibraryCandidate> candidates = CollectCandidates();
            if (candidates.Count <= 0)
            {
                return false;
            }

            int selectedIndex = PickWeightedIndex(candidates, random);
            library = candidates[selectedIndex].Library;
            return library != null;
        }

        private void Sanitize()
        {
            libraryEntries ??= new List<LibraryWeightEntry>();
            for (int i = 0; i < libraryEntries.Count; i++)
            {
                libraryEntries[i]?.Sanitize();
            }
        }

        private List<WeightedLibraryCandidate> CollectCandidates()
        {
            List<WeightedLibraryCandidate> candidates = new();
            libraryEntries ??= new List<LibraryWeightEntry>();
            for (int i = 0; i < libraryEntries.Count; i++)
            {
                LibraryWeightEntry entry = libraryEntries[i];
                BulletTokenLibrary library = entry != null ? entry.Library : null;
                float normalizedWeight = entry != null ? NormalizeWeight(entry.SelectionWeight) : 0f;
                if (library == null || normalizedWeight <= 0f)
                {
                    continue;
                }

                candidates.Add(new WeightedLibraryCandidate(library, normalizedWeight));
            }

            return candidates;
        }

        private static int PickWeightedIndex(IReadOnlyList<WeightedLibraryCandidate> candidates, VocalithRandom random)
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

        private readonly struct WeightedLibraryCandidate
        {
            public WeightedLibraryCandidate(BulletTokenLibrary library, float weight)
            {
                Library = library;
                Weight = weight;
            }

            public BulletTokenLibrary Library { get; }
            public float Weight { get; }
        }
    }
}
