using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    public enum SpellBookFixedItemPlacement
    {
        BeforeEquipped = 0,
        AfterEquipped = 1,
    }

    /// <summary>
    /// 描述一本法术书作为执行器时提供的槽位、冷却与常驻 token。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Spell Books/Spell Book", fileName = "SpellBook")]
    public sealed class SpellBookData : ScriptableObject
    {
        private const int MinimumSlotCount = 1;

        [SerializeField] private string spellBookId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, TextArea] private string selectionDescription = string.Empty;
        [SerializeField, Min(MinimumSlotCount)] private int slotCount = 5;
        [SerializeField, Min(0f)] private float castCooldownSeconds = 0.2f;
        [SerializeField, Min(1)] private int castsPerActivation = 1;
        [SerializeField, Min(0f)] private float activationSpreadAngleStep = 0f;
        [SerializeField, Min(0f)] private float energyCapacity = 0f;
        [SerializeField, Min(0f)] private float energyRegenPerSecond = 0f;
        [SerializeField, Min(0f)] private float energyCostPerActivation = 0f;
        [SerializeField] private List<TokenModifierDefinition> executorModifiers = new();
        [SerializeField] private SpellBookFixedItemPlacement fixedItemPlacement = SpellBookFixedItemPlacement.BeforeEquipped;
        [SerializeField] private List<PlaceableTokenData> fixedCastItems = new();

        public string SpellBookId
        {
            get => string.IsNullOrWhiteSpace(spellBookId) ? name : spellBookId.Trim();
            set => spellBookId = value != null ? value.Trim() : string.Empty;
        }

        public string DisplayName
        {
            get => string.IsNullOrWhiteSpace(displayName) ? SpellBookId : displayName.Trim();
            set => displayName = value != null ? value.Trim() : string.Empty;
        }

        public string SelectionDescription
        {
            get => selectionDescription ?? string.Empty;
            set => selectionDescription = value ?? string.Empty;
        }

        public int SlotCount
        {
            get => Mathf.Max(MinimumSlotCount, slotCount);
            set => slotCount = Mathf.Max(MinimumSlotCount, value);
        }

        public float CastCooldownSeconds
        {
            get => Mathf.Max(0f, castCooldownSeconds);
            set => castCooldownSeconds = Mathf.Max(0f, value);
        }

        public int CastsPerActivation
        {
            get => Mathf.Max(1, castsPerActivation);
            set => castsPerActivation = Mathf.Max(1, value);
        }

        public float ActivationSpreadAngleStep
        {
            get => Mathf.Max(0f, activationSpreadAngleStep);
            set => activationSpreadAngleStep = Mathf.Max(0f, value);
        }

        public float EnergyCapacity
        {
            get => Mathf.Max(0f, energyCapacity);
            set => energyCapacity = Mathf.Max(0f, value);
        }

        public float EnergyRegenPerSecond
        {
            get => Mathf.Max(0f, energyRegenPerSecond);
            set => energyRegenPerSecond = Mathf.Max(0f, value);
        }

        public float EnergyCostPerActivation
        {
            get => Mathf.Max(0f, energyCostPerActivation);
            set => energyCostPerActivation = Mathf.Max(0f, value);
        }

        public bool UsesEnergy => EnergyCapacity > 0f && EnergyCostPerActivation > 0f;
        public bool HasExecutorModifiers => executorModifiers != null && executorModifiers.Count > 0;

        public IReadOnlyList<TokenModifierDefinition> ExecutorModifiers => executorModifiers;

        public SpellBookFixedItemPlacement FixedItemPlacement
        {
            get => fixedItemPlacement;
            set => fixedItemPlacement = value;
        }

        public IReadOnlyList<PlaceableTokenData> FixedCastItems => fixedCastItems;

        /// <summary>
        /// summary: 替换法术书自带的常驻 token 列表，空项会被忽略。
        /// param: items 新的常驻 token 集合
        /// returns: 无
        /// </summary>
        public void SetFixedCastItems(IEnumerable<PlaceableTokenData> items)
        {
            fixedCastItems ??= new List<PlaceableTokenData>();
            fixedCastItems.Clear();
            if (items == null)
            {
                return;
            }

            foreach (PlaceableTokenData item in items)
            {
                if (item != null)
                {
                    fixedCastItems.Add(item);
                }
            }
        }

        public void SetExecutorModifiers(IEnumerable<TokenModifierDefinition> modifiers)
        {
            executorModifiers ??= new List<TokenModifierDefinition>();
            executorModifiers.Clear();
            if (modifiers == null)
            {
                return;
            }

            foreach (TokenModifierDefinition modifier in modifiers)
            {
                executorModifiers.Add(modifier.GetSanitized());
            }
        }

        /// <summary>
        /// summary: 按法术书槽位上限与常驻 token 配置，构造本次编译应读取的执行序列。
        /// param: equippedItems 玩家放入法术书槽位中的 token 物件
        /// returns: 新建的执行序列；调用方可以安全修改返回列表
        /// </summary>
        public List<PlaceableTokenData> BuildExecutionItems(IReadOnlyList<PlaceableTokenData> equippedItems)
        {
            List<PlaceableTokenData> executionItems = new();
            if (fixedItemPlacement == SpellBookFixedItemPlacement.BeforeEquipped)
            {
                AppendFixedItems(executionItems);
                AppendEquippedItems(executionItems, equippedItems);
            }
            else
            {
                AppendEquippedItems(executionItems, equippedItems);
                AppendFixedItems(executionItems);
            }

            return executionItems;
        }

        public string GetSelectionDescription()
        {
            if (!string.IsNullOrWhiteSpace(selectionDescription))
            {
                return selectionDescription;
            }

            string activationText = CastsPerActivation > 1
                ? $"每次激活 {CastsPerActivation} 次，激活扇形 {ActivationSpreadAngleStep:0.##} 度"
                : "每次激活 1 次";
            string energyText = UsesEnergy
                ? $"，能量 {EnergyCapacity:0.##}，每次消耗 {EnergyCostPerActivation:0.##}，每秒恢复 {EnergyRegenPerSecond:0.##}"
                : string.Empty;
            string bonusText = HasExecutorModifiers
                ? $"，内建强化 {executorModifiers.Count} 项"
                : string.Empty;
            return $"{SlotCount} 槽，冷却 {CastCooldownSeconds:0.##} 秒，{activationText}{energyText}{bonusText}。";
        }

        private void AppendFixedItems(ICollection<PlaceableTokenData> buffer)
        {
            if (buffer == null || fixedCastItems == null)
            {
                return;
            }

            for (int i = 0; i < fixedCastItems.Count; i++)
            {
                if (fixedCastItems[i] != null)
                {
                    buffer.Add(fixedCastItems[i]);
                }
            }
        }

        private void AppendEquippedItems(ICollection<PlaceableTokenData> buffer, IReadOnlyList<PlaceableTokenData> equippedItems)
        {
            if (buffer == null || equippedItems == null)
            {
                return;
            }

            int acceptedSlots = 0;
            int maxSlots = SlotCount;
            for (int i = 0; i < equippedItems.Count && acceptedSlots < maxSlots; i++)
            {
                PlaceableTokenData item = equippedItems[i];
                if (item == null)
                {
                    continue;
                }

                int slotSpan = Mathf.Max(1, item.SlotSpan);
                if (acceptedSlots + slotSpan > maxSlots)
                {
                    continue;
                }

                buffer.Add(item);
                acceptedSlots += slotSpan;
            }
        }

        private void OnValidate()
        {
            spellBookId = spellBookId != null ? spellBookId.Trim() : string.Empty;
            displayName = displayName != null ? displayName.Trim() : string.Empty;
            selectionDescription ??= string.Empty;
            slotCount = Mathf.Max(MinimumSlotCount, slotCount);
            castCooldownSeconds = Mathf.Max(0f, castCooldownSeconds);
            castsPerActivation = Mathf.Max(1, castsPerActivation);
            activationSpreadAngleStep = Mathf.Max(0f, activationSpreadAngleStep);
            energyCapacity = Mathf.Max(0f, energyCapacity);
            energyRegenPerSecond = Mathf.Max(0f, energyRegenPerSecond);
            energyCostPerActivation = Mathf.Max(0f, energyCostPerActivation);
            executorModifiers ??= new List<TokenModifierDefinition>();
            for (int i = 0; i < executorModifiers.Count; i++)
            {
                executorModifiers[i] = executorModifiers[i].GetSanitized();
            }

            fixedCastItems ??= new List<PlaceableTokenData>();
        }
    }
}
