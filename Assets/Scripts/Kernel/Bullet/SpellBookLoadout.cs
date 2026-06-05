using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 维护一本法术书执行器与玩家当前放入槽位的 token，并缓存编译结果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpellBookLoadout : MonoBehaviour
    {
        private const int FallbackSlotCount = 5;

        [SerializeField] private SpellBookData spellBook;
        [SerializeField] private List<PlaceableTokenData> equippedItems = new();

        private readonly List<PlaceableTokenData> executionItems = new();
        private readonly List<BaseTokenData> expandedTokens = new();
        private readonly List<PlaceableTokenData> startingItems = new();
        private CompiledSpellProgram compiledProgram;
        private float currentEnergy;
        private float lastEnergyRefreshTime;
        private bool hasInitializedEnergy;
        private bool hasCapturedStartingItems;
        private bool isDirty = true;
        private int revision;

        public event Action Changed;

        public SpellBookData SpellBook => spellBook;
        public int SlotCount => spellBook != null ? spellBook.SlotCount : FallbackSlotCount;
        public float CastCooldownSeconds => spellBook != null ? spellBook.CastCooldownSeconds : 0f;
        public int CastsPerActivation => spellBook != null ? spellBook.CastsPerActivation : 1;
        public float ActivationSpreadAngleStep => spellBook != null ? spellBook.ActivationSpreadAngleStep : 0f;
        public bool UsesEnergy => spellBook != null && spellBook.UsesEnergy;
        public float EnergyCapacity => spellBook != null ? spellBook.EnergyCapacity : 0f;
        public float EnergyRegenPerSecond => spellBook != null ? spellBook.EnergyRegenPerSecond : 0f;
        public float EnergyCostPerActivation => spellBook != null ? spellBook.EnergyCostPerActivation : 0f;
        public float CurrentEnergy => UsesEnergy ? currentEnergy : 0f;
        public IReadOnlyList<PlaceableTokenData> EquippedItems => equippedItems;
        public IReadOnlyList<PlaceableTokenData> ExecutionItems => executionItems;
        public IReadOnlyList<BaseTokenData> Tokens => expandedTokens;
        public int Revision => revision;
        public bool HasTokens => executionItems.Count > 0;

        public CompiledSpellProgram CurrentCompiledProgram
        {
            get
            {
                EnsureCompiledProgram();
                return compiledProgram;
            }
        }

        private void Awake()
        {
            CaptureStartingItemsIfNeeded();
            RefillActivationEnergy(Time.time);
            RebuildExecutionItems();
            EnsureCompiledProgram();
        }

        private void OnValidate()
        {
            RebuildExecutionItems();
            MarkDirty();
        }

        /// <summary>
        /// summary: 替换当前使用的法术书执行器，并重建执行序列。
        /// param: newSpellBook 新的法术书数据资产
        /// returns: 无
        /// </summary>
        public void SetSpellBook(SpellBookData newSpellBook)
        {
            spellBook = newSpellBook;
            TrimEquippedItemsToSlotCount();
            RefillActivationEnergy(Time.time);
            MarkDirty();
        }

        /// <summary>
        /// summary: 替换当前法术书槽位中的 token 物件，超出槽位上限的项会被跳过。
        /// param: orderedItems 新的有序 token 物件列表
        /// returns: 无
        /// </summary>
        public void SetItems(IEnumerable<PlaceableTokenData> orderedItems)
        {
            CaptureStartingItemsIfNeeded();
            equippedItems.Clear();
            if (orderedItems != null)
            {
                int acceptedSlots = 0;
                int maxSlots = SlotCount;
                foreach (PlaceableTokenData item in orderedItems)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    int slotSpan = Mathf.Max(1, item.SlotSpan);
                    if (acceptedSlots + slotSpan > maxSlots)
                    {
                        continue;
                    }

                    equippedItems.Add(item);
                    acceptedSlots += slotSpan;
                }
            }

            MarkDirty();
        }

        /// <summary>
        /// summary: 用单格基础 token 顺序替换当前法术书槽位内容。
        /// param: orderedTokens 新的基础 token 序列
        /// returns: 无
        /// </summary>
        public void SetTokens(IEnumerable<BaseTokenData> orderedTokens)
        {
            CaptureStartingItemsIfNeeded();
            equippedItems.Clear();
            if (orderedTokens != null)
            {
                int acceptedSlots = 0;
                int maxSlots = SlotCount;
                foreach (BaseTokenData token in orderedTokens)
                {
                    if (token == null || acceptedSlots >= maxSlots)
                    {
                        continue;
                    }

                    equippedItems.Add(token);
                    acceptedSlots++;
                }
            }

            MarkDirty();
        }

        /// <summary>
        /// summary: 显式把当前缓存标记为需要重新编译。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void MarkDirty()
        {
            RebuildExecutionItems();
            isDirty = true;
            revision++;
            Changed?.Invoke();
        }

        /// <summary>
        /// summary: 获取当前可执行的 SpellProgram；若缓存失效则先重新编译。
        /// param: program 输出的法术程序
        /// returns: 当前法术程序可施放时返回 true
        /// </summary>
        public bool TryGetCompiledProgram(out CompiledSpellProgram program)
        {
            program = EnsureCompiledProgram();
            return program != null && program.CanCast;
        }

        /// <summary>
        /// summary: 强制基于当前法术书执行序列重新编译 SpellProgram。
        /// param: 无
        /// returns: 最新的法术程序
        /// </summary>
        public CompiledSpellProgram RecompileProgram()
        {
            compiledProgram = SpellProgramCompiler.Compile(executionItems, spellBook);
            isDirty = false;
            return compiledProgram;
        }

        /// <summary>
        /// summary: 把当前法术书槽位恢复到对象初次启用时的起始配置。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ResetToStartingItems()
        {
            CaptureStartingItemsIfNeeded();
            SetItems(startingItems);
        }

        public void RefillActivationEnergy(float currentTime)
        {
            currentEnergy = UsesEnergy ? EnergyCapacity : 0f;
            lastEnergyRefreshTime = Mathf.Max(0f, currentTime);
            hasInitializedEnergy = true;
        }

        public void RefreshActivationEnergy(float currentTime)
        {
            float resolvedTime = Mathf.Max(0f, currentTime);
            if (!UsesEnergy)
            {
                currentEnergy = 0f;
                lastEnergyRefreshTime = resolvedTime;
                hasInitializedEnergy = true;
                return;
            }

            if (!hasInitializedEnergy)
            {
                RefillActivationEnergy(resolvedTime);
                return;
            }

            float deltaTime = Mathf.Max(0f, resolvedTime - lastEnergyRefreshTime);
            if (deltaTime > 0f && EnergyRegenPerSecond > 0f)
            {
                currentEnergy = Mathf.Min(EnergyCapacity, currentEnergy + (EnergyRegenPerSecond * deltaTime));
            }

            lastEnergyRefreshTime = resolvedTime;
        }

        public bool HasActivationEnergy(float currentTime)
        {
            return HasActivationEnergy(currentTime, 1f);
        }

        public bool HasActivationEnergy(float currentTime, float costMultiplier)
        {
            RefreshActivationEnergy(currentTime);
            return !UsesEnergy || currentEnergy + 0.0001f >= ResolveActivationEnergyCost(costMultiplier);
        }

        public bool TryConsumeActivationEnergy(float currentTime)
        {
            return TryConsumeActivationEnergy(currentTime, 1f);
        }

        public bool TryConsumeActivationEnergy(float currentTime, float costMultiplier)
        {
            RefreshActivationEnergy(currentTime);
            if (!UsesEnergy)
            {
                return true;
            }

            float cost = ResolveActivationEnergyCost(costMultiplier);
            if (currentEnergy + 0.0001f < cost)
            {
                return false;
            }

            currentEnergy = Mathf.Max(0f, currentEnergy - cost);
            Changed?.Invoke();
            return true;
        }

        public float ResolveActivationEnergyCost(float costMultiplier)
        {
            return Mathf.Max(0f, EnergyCostPerActivation * Mathf.Max(0f, costMultiplier));
        }

        private CompiledSpellProgram EnsureCompiledProgram()
        {
            if (isDirty || compiledProgram == null)
            {
                RecompileProgram();
            }

            return compiledProgram;
        }

        private void RebuildExecutionItems()
        {
            executionItems.Clear();
            if (spellBook != null)
            {
                executionItems.AddRange(spellBook.BuildExecutionItems(equippedItems));
            }
            else
            {
                AppendEquippedItemsWithoutSpellBook(executionItems);
            }

            RebuildExpandedTokens();
        }

        private void RebuildExpandedTokens()
        {
            expandedTokens.Clear();
            for (int i = 0; i < executionItems.Count; i++)
            {
                executionItems[i]?.AppendCompileTokens(expandedTokens);
            }
        }

        private void CaptureStartingItemsIfNeeded()
        {
            if (hasCapturedStartingItems)
            {
                return;
            }

            startingItems.Clear();
            if (equippedItems != null)
            {
                for (int i = 0; i < equippedItems.Count; i++)
                {
                    PlaceableTokenData item = equippedItems[i];
                    if (item != null)
                    {
                        startingItems.Add(item);
                    }
                }
            }

            hasCapturedStartingItems = true;
        }

        private void TrimEquippedItemsToSlotCount()
        {
            if (equippedItems == null || equippedItems.Count <= 0)
            {
                return;
            }

            List<PlaceableTokenData> trimmedItems = new(equippedItems);
            equippedItems.Clear();
            int acceptedSlots = 0;
            int maxSlots = SlotCount;
            for (int i = 0; i < trimmedItems.Count && acceptedSlots < maxSlots; i++)
            {
                PlaceableTokenData item = trimmedItems[i];
                if (item == null)
                {
                    continue;
                }

                int slotSpan = Mathf.Max(1, item.SlotSpan);
                if (acceptedSlots + slotSpan > maxSlots)
                {
                    continue;
                }

                equippedItems.Add(item);
                acceptedSlots += slotSpan;
            }
        }

        private void AppendEquippedItemsWithoutSpellBook(ICollection<PlaceableTokenData> buffer)
        {
            if (buffer == null || equippedItems == null)
            {
                return;
            }

            int acceptedSlots = 0;
            for (int i = 0; i < equippedItems.Count && acceptedSlots < FallbackSlotCount; i++)
            {
                PlaceableTokenData item = equippedItems[i];
                if (item == null)
                {
                    continue;
                }

                int slotSpan = Mathf.Max(1, item.SlotSpan);
                if (acceptedSlots + slotSpan > FallbackSlotCount)
                {
                    continue;
                }

                buffer.Add(item);
                acceptedSlots += slotSpan;
            }
        }
    }
}
