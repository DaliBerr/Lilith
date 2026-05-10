using System.Collections.Generic;
using UnityEngine;
using Vocalith.Localization;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示一个横向连续占据多个格子的连锁 token 物件。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Linked Token", fileName = "LinkedToken")]
    public sealed class LinkedTokenData : PlaceableTokenData
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private string descriptionKey = string.Empty;
        [SerializeField] private List<BaseTokenData> linkedTokens = new();
        [SerializeField, Min(1f)] private float damageMultiplier = 1f;
        [SerializeField] private string pickupDisplayTextOverride = string.Empty;
        [SerializeField] private string pickupDisplayTextKey = string.Empty;

        public string ItemId
        {
            get => itemId;
            set => itemId = value != null ? value.Trim() : string.Empty;
        }

        public string Description
        {
            get => description;
            set => description = value ?? string.Empty;
        }

        public string DescriptionKey
        {
            get => descriptionKey;
            set => descriptionKey = value != null ? value.Trim() : string.Empty;
        }

        public IReadOnlyList<BaseTokenData> LinkedTokens => linkedTokens;
        public override int SlotSpan => linkedTokens != null ? linkedTokens.Count : 0;
        public override float DamageMultiplier => Mathf.Max(1f, damageMultiplier);

        public float ConfiguredDamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = Mathf.Max(1f, value);
        }

        public string PickupDisplayTextOverride
        {
            get => pickupDisplayTextOverride;
            set => pickupDisplayTextOverride = value ?? string.Empty;
        }

        public string PickupDisplayTextKey
        {
            get => pickupDisplayTextKey;
            set => pickupDisplayTextKey = value != null ? value.Trim() : string.Empty;
        }

        /// <summary>
        /// summary: 用新的成员 token 序列替换当前连锁件内容。
        /// param: tokens 需要写入的新成员 token 集合
        /// returns: 无
        /// </summary>
        public void SetLinkedTokens(IEnumerable<BaseTokenData> tokens)
        {
            linkedTokens ??= new List<BaseTokenData>();
            linkedTokens.Clear();
            if (tokens == null)
            {
                return;
            }

            foreach (BaseTokenData token in tokens)
            {
                if (token != null)
                {
                    linkedTokens.Add(token);
                }
            }
        }

        public override BaseTokenData GetVisualToken(int localOffset)
        {
            if (linkedTokens == null || localOffset < 0 || localOffset >= linkedTokens.Count)
            {
                return null;
            }

            return linkedTokens[localOffset];
        }

        public override void AppendCompileTokens(List<BaseTokenData> buffer)
        {
            if (buffer == null || linkedTokens == null)
            {
                return;
            }

            for (int i = 0; i < linkedTokens.Count; i++)
            {
                if (linkedTokens[i] != null)
                {
                    buffer.Add(linkedTokens[i]);
                }
            }
        }

        public override string GetPickupDisplayText()
        {
            string fallback = !string.IsNullOrWhiteSpace(pickupDisplayTextOverride)
                ? pickupDisplayTextOverride
                : base.GetPickupDisplayText();
            return ResolveLocalizedText(pickupDisplayTextKey, fallback);
        }

        /// <summary>
        /// summary: 返回当前连锁 token 在选择弹窗中的说明文本。
        /// param: 无
        /// returns: 当前连锁 token 的 description
        /// </summary>
        public override string GetSelectionDescription()
        {
            return ResolveLocalizedText(descriptionKey, Description);
        }

        private void OnValidate()
        {
            itemId = itemId != null ? itemId.Trim() : string.Empty;
            description ??= string.Empty;
            descriptionKey = descriptionKey != null ? descriptionKey.Trim() : string.Empty;
            pickupDisplayTextOverride ??= string.Empty;
            pickupDisplayTextKey = pickupDisplayTextKey != null ? pickupDisplayTextKey.Trim() : string.Empty;
            damageMultiplier = Mathf.Max(1f, damageMultiplier);
            linkedTokens ??= new List<BaseTokenData>();
        }

        private static string ResolveLocalizedText(string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(key)
                ? fallback ?? string.Empty
                : LocalizationManager.TranslateOrDefault(key, fallback);
        }
    }
}
