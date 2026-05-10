using System.Collections.Generic;
using UnityEngine;
using Vocalith.Localization;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示仅用于场景拾取结算的掉落 token，不参与攻击公式编译。
    /// </summary>
    public abstract class PickupTokenData : PlaceableTokenData
    {
        [SerializeField] private string tokenId = string.Empty;
        [SerializeField] private string displayText = string.Empty;
        [SerializeField] private string displayTextKey = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private string descriptionKey = string.Empty;

        public override int SlotSpan => 1;

        public string TokenId
        {
            get => tokenId;
            set => tokenId = value != null ? value.Trim() : string.Empty;
        }

        public string DisplayText
        {
            get => displayText;
            set => displayText = value ?? string.Empty;
        }

        public string DisplayTextKey
        {
            get => displayTextKey;
            set => displayTextKey = value != null ? value.Trim() : string.Empty;
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

        /// <summary>
        /// summary: 返回当前 pickup token 在指定局部 offset 下的视觉 token。
        /// param: localOffset 当前 item 内部的局部格索引
        /// returns: pickup token 不参与背包视觉渲染，始终返回 null
        /// </summary>
        public override BaseTokenData GetVisualToken(int localOffset)
        {
            return null;
        }

        /// <summary>
        /// summary: pickup token 不参与攻击公式编译，因此不会向编译缓冲区追加任何成员。
        /// param: buffer 攻击公式编译使用的成员 token 缓冲区
        /// returns: 无
        /// </summary>
        public override void AppendCompileTokens(List<BaseTokenData> buffer)
        {
        }

        /// <summary>
        /// summary: 返回 pickup token 在世界中的默认显示文本。
        /// param: 无
        /// returns: 若 displayText 为空则回退 tokenId
        /// </summary>
        public override string GetPickupDisplayText()
        {
            string fallback = string.IsNullOrWhiteSpace(displayText) ? tokenId : displayText;
            return ResolveLocalizedText(displayTextKey, fallback);
        }

        /// <summary>
        /// summary: 返回当前 pickup token 在选择弹窗中的说明文本。
        /// param: 无
        /// returns: 当前 pickup token 的 description
        /// </summary>
        public override string GetSelectionDescription()
        {
            return ResolveLocalizedText(descriptionKey, Description);
        }

        /// <summary>
        /// summary: 修正 pickup token 的可序列化文本字段，避免空引用进入运行时。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected virtual void OnValidate()
        {
            tokenId = tokenId != null ? tokenId.Trim() : string.Empty;
            displayText ??= string.Empty;
            displayTextKey = displayTextKey != null ? displayTextKey.Trim() : string.Empty;
            description ??= string.Empty;
            descriptionKey = descriptionKey != null ? descriptionKey.Trim() : string.Empty;
        }

        private static string ResolveLocalizedText(string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(key)
                ? fallback ?? string.Empty
                : LocalizationManager.TranslateOrDefault(key, fallback);
        }
    }
}
