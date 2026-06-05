using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 仅用于隐藏设计索引的占位 token；不追加编译词元，不会形成可执行法术。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Prototype Token", fileName = "PrototypeToken")]
    public sealed class PrototypeTokenData : PlaceableTokenData
    {
        [SerializeField] private string tokenId = string.Empty;
        [SerializeField] private string displayText = string.Empty;
        [SerializeField] private string prototypeCategory = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField, TextArea] private string unimplementedReason = string.Empty;

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

        public string PrototypeCategory
        {
            get => prototypeCategory;
            set => prototypeCategory = value != null ? value.Trim() : string.Empty;
        }

        public string Description
        {
            get => description;
            set => description = value ?? string.Empty;
        }

        public string UnimplementedReason
        {
            get => unimplementedReason;
            set => unimplementedReason = value ?? string.Empty;
        }

        public override BaseTokenData GetVisualToken(int localOffset)
        {
            return null;
        }

        public override void AppendCompileTokens(List<BaseTokenData> buffer)
        {
        }

        public override string GetPickupDisplayText()
        {
            return displayText;
        }

        public override string GetSelectionDescription()
        {
            if (string.IsNullOrWhiteSpace(unimplementedReason))
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(description)
                ? unimplementedReason
                : $"{description}\n未实装原因：{unimplementedReason}";
        }

        private void OnValidate()
        {
            tokenId = tokenId != null ? tokenId.Trim() : string.Empty;
            displayText ??= string.Empty;
            prototypeCategory = prototypeCategory != null ? prototypeCategory.Trim() : string.Empty;
            description ??= string.Empty;
            unimplementedReason ??= string.Empty;
        }
    }
}
