using System;
using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.Bullet
{
    /// <summary>
    /// 维护一份可供 Run 内选择界面抽取的法术书奖励库。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Spell Books/Spell Book Reward Library", fileName = "SpellBookRewardLibrary")]
    public sealed class SpellBookRewardLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class SpellBookWeightEntry
        {
            [SerializeField] private SpellBookData spellBook;
            [SerializeField, Min(0f)] private float drawWeight = 1f;

            public SpellBookData SpellBook => spellBook;
            public float DrawWeight => drawWeight;

            public void Set(SpellBookData spellBookData, float weight)
            {
                spellBook = spellBookData;
                drawWeight = NormalizeWeight(weight);
            }
        }

        [SerializeField, HideInInspector] private List<SpellBookData> selectableSpellBooks = new();
        [SerializeField] private List<SpellBookWeightEntry> spellBookWeights = new();

        public IReadOnlyList<SpellBookData> SelectableSpellBooks
        {
            get
            {
                SanitizeEntries(removeNull: true);
                RebuildSelectableSpellBookCache();
                return selectableSpellBooks;
            }
        }

        public IReadOnlyList<SpellBookWeightEntry> SpellBookWeights
        {
            get
            {
                SanitizeEntries(removeNull: false);
                return spellBookWeights;
            }
        }

        public void SetSpellBooks(IEnumerable<SpellBookData> spellBooks)
        {
            spellBookWeights ??= new List<SpellBookWeightEntry>();
            spellBookWeights.Clear();
            AppendUniqueSpellBooks(spellBooks, defaultWeight: 1f);
            RebuildSelectableSpellBookCache();
        }

        public void AddSpellBook(SpellBookData spellBook)
        {
            AddSpellBook(spellBook, drawWeight: 1f);
        }

        public void AddSpellBook(SpellBookData spellBook, float drawWeight)
        {
            if (spellBook == null)
            {
                return;
            }

            SanitizeEntries(removeNull: false);
            if (!TryAddEntry(spellBook, drawWeight, overwriteExistingWeight: false))
            {
                return;
            }

            RebuildSelectableSpellBookCache();
        }

        public void SetSpellBookWeight(SpellBookData spellBook, float drawWeight)
        {
            if (spellBook == null)
            {
                return;
            }

            SanitizeEntries(removeNull: false);
            SpellBookWeightEntry existing = FindEntry(spellBook);
            if (existing == null)
            {
                existing = new SpellBookWeightEntry();
                spellBookWeights.Add(existing);
            }

            existing.Set(spellBook, drawWeight);
            RebuildSelectableSpellBookCache();
        }

        public List<SpellBookData> SampleChoices(
            VocalithRandom random = null,
            int desiredCount = -1,
            int minCount = 1,
            int maxCount = 3)
        {
            SanitizeEntries(removeNull: true);
            List<WeightedCandidate> candidates = CollectWeightedCandidates();
            if (candidates.Count <= 0)
            {
                return new List<SpellBookData>();
            }

            VocalithRandom rng = random ?? new VocalithRandom();
            int targetCount = desiredCount > 0
                ? desiredCount
                : DetermineChoiceCount(candidates.Count, rng, minCount, maxCount);

            targetCount = Mathf.Clamp(targetCount, 0, candidates.Count);
            if (targetCount <= 0)
            {
                return new List<SpellBookData>();
            }

            List<SpellBookData> sampledSpellBooks = new(targetCount);
            for (int i = 0; i < targetCount; i++)
            {
                int selectedIndex = PickWeightedIndex(candidates, rng);
                sampledSpellBooks.Add(candidates[selectedIndex].SpellBook);
                candidates.RemoveAt(selectedIndex);
            }

            return sampledSpellBooks;
        }

        public IReadOnlyList<SpellBookData> GetSpellBooks()
        {
            SanitizeEntries(removeNull: true);
            RebuildSelectableSpellBookCache();
            return selectableSpellBooks;
        }

        private void OnValidate()
        {
            SanitizeEntries(removeNull: false);
            RebuildSelectableSpellBookCache();
        }

        private void AppendUniqueSpellBooks(IEnumerable<SpellBookData> spellBooks, float defaultWeight)
        {
            if (spellBooks == null)
            {
                return;
            }

            foreach (SpellBookData spellBook in spellBooks)
            {
                TryAddEntry(spellBook, defaultWeight, overwriteExistingWeight: false);
            }
        }

        private void SanitizeEntries(bool removeNull)
        {
            selectableSpellBooks ??= new List<SpellBookData>();
            spellBookWeights ??= new List<SpellBookWeightEntry>();
            MigrateLegacySpellBooksIfNeeded();

            HashSet<int> seenInstanceIds = new();
            for (int i = spellBookWeights.Count - 1; i >= 0; i--)
            {
                SpellBookWeightEntry entry = spellBookWeights[i];
                if (entry == null)
                {
                    if (removeNull)
                    {
                        spellBookWeights.RemoveAt(i);
                    }

                    continue;
                }

                SpellBookData spellBook = entry.SpellBook;
                if (spellBook == null)
                {
                    if (removeNull)
                    {
                        spellBookWeights.RemoveAt(i);
                    }

                    continue;
                }

                float normalizedWeight = NormalizeWeight(entry.DrawWeight);
                if (!Mathf.Approximately(entry.DrawWeight, normalizedWeight))
                {
                    entry.Set(spellBook, normalizedWeight);
                }

                if (!seenInstanceIds.Add(spellBook.GetInstanceID()))
                {
                    spellBookWeights.RemoveAt(i);
                }
            }
        }

        private void RebuildSelectableSpellBookCache()
        {
            selectableSpellBooks ??= new List<SpellBookData>();
            selectableSpellBooks.Clear();

            if (spellBookWeights == null)
            {
                return;
            }

            HashSet<int> seenInstanceIds = new();
            for (int i = 0; i < spellBookWeights.Count; i++)
            {
                SpellBookData spellBook = spellBookWeights[i] != null ? spellBookWeights[i].SpellBook : null;
                if (spellBook != null && seenInstanceIds.Add(spellBook.GetInstanceID()))
                {
                    selectableSpellBooks.Add(spellBook);
                }
            }
        }

        private bool TryAddEntry(SpellBookData spellBook, float drawWeight, bool overwriteExistingWeight)
        {
            if (spellBook == null)
            {
                return false;
            }

            spellBookWeights ??= new List<SpellBookWeightEntry>();
            SpellBookWeightEntry existing = FindEntry(spellBook);
            if (existing != null)
            {
                if (overwriteExistingWeight)
                {
                    existing.Set(spellBook, drawWeight);
                }

                return false;
            }

            SpellBookWeightEntry entry = new SpellBookWeightEntry();
            entry.Set(spellBook, drawWeight);
            spellBookWeights.Add(entry);
            return true;
        }

        private SpellBookWeightEntry FindEntry(SpellBookData spellBook)
        {
            if (spellBook == null || spellBookWeights == null)
            {
                return null;
            }

            for (int i = 0; i < spellBookWeights.Count; i++)
            {
                SpellBookWeightEntry entry = spellBookWeights[i];
                if (entry != null && entry.SpellBook == spellBook)
                {
                    return entry;
                }
            }

            return null;
        }

        private void MigrateLegacySpellBooksIfNeeded()
        {
            if (spellBookWeights == null || spellBookWeights.Count > 0 || selectableSpellBooks == null || selectableSpellBooks.Count == 0)
            {
                return;
            }

            for (int i = 0; i < selectableSpellBooks.Count; i++)
            {
                TryAddEntry(selectableSpellBooks[i], drawWeight: 1f, overwriteExistingWeight: false);
            }
        }

        private List<WeightedCandidate> CollectWeightedCandidates()
        {
            List<WeightedCandidate> candidates = new();
            if (spellBookWeights == null)
            {
                return candidates;
            }

            for (int i = 0; i < spellBookWeights.Count; i++)
            {
                SpellBookWeightEntry entry = spellBookWeights[i];
                SpellBookData spellBook = entry != null ? entry.SpellBook : null;
                if (spellBook == null)
                {
                    continue;
                }

                float normalizedWeight = NormalizeWeight(entry.DrawWeight);
                if (normalizedWeight > 0f)
                {
                    candidates.Add(new WeightedCandidate(spellBook, normalizedWeight));
                }
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
            return Math.Min(Mathf.Clamp(selected, lower, upper), availableCount);
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
            public WeightedCandidate(SpellBookData spellBook, float weight)
            {
                SpellBook = spellBook;
                Weight = weight;
            }

            public SpellBookData SpellBook { get; }
            public float Weight { get; }
        }
    }
}
