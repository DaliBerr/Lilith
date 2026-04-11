using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 所有攻击词元资产的基础类型，统一承载显示文本与稳定标识。
    /// </summary>
    public abstract class BaseTokenData : PlaceableTokenData
    {
        [SerializeField] private string tokenId = string.Empty;
        [SerializeField] private string displayText = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private TokenType tokenType = TokenType.None;
        [SerializeField] private bool hasBulletTextOverride;
        [SerializeField, TextArea] private string bulletTextOverride = string.Empty;
        [SerializeField] private List<TokenModifierDefinition> modifiers = new();

        public override int SlotSpan => 1;
        public override float DamageMultiplier => 1f;

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

        public string Description
        {
            get => description;
            set => description = value ?? string.Empty;
        }

        public TokenType TokenType => tokenType;
        public bool HasBulletTextOverride => hasBulletTextOverride;
        public string BulletTextOverride => bulletTextOverride ?? string.Empty;
        public IReadOnlyList<TokenModifierDefinition> Modifiers => modifiers;

        /// <summary>
        /// summary: 返回当前词元用于公式显示的文本；若未显式配置则回退到稳定标识。
        /// param: 无
        /// returns: 当前词元可读的显示文本
        /// </summary>
        public string GetResolvedDisplayText()
        {
            return string.IsNullOrWhiteSpace(displayText) ? tokenId : displayText;
        }

        public override BaseTokenData GetVisualToken(int localOffset)
        {
            return localOffset == 0 ? this : null;
        }

        public override void AppendCompileTokens(List<BaseTokenData> buffer)
        {
            if (buffer != null)
            {
                buffer.Add(this);
            }
        }

        public override string GetPickupDisplayText()
        {
            return GetResolvedDisplayText();
        }

        /// <summary>
        /// summary: 返回当前基础 token 在选择弹窗中的说明文本。
        /// param: 无
        /// returns: 当前 token 的 description
        /// </summary>
        public override string GetSelectionDescription()
        {
            return Description;
        }

        /// <summary>
        /// summary: 由派生类写入固定的词元类型，避免在 Inspector 中被误改。
        /// param: value 需要写入的词元分类
        /// returns: 无
        /// </summary>
        protected void SetTokenType(TokenType value)
        {
            tokenType = value;
        }

        /// <summary>
        /// summary: 用一组新的修饰表达式替换当前词元的全部视觉和运行时修饰配置。
        /// param: definitions 需要写入的新修饰定义集合
        /// returns: 无
        /// </summary>
        public void SetModifiers(IEnumerable<TokenModifierDefinition> definitions)
        {
            modifiers ??= new List<TokenModifierDefinition>();
            modifiers.Clear();
            if (definitions == null)
            {
                return;
            }

            foreach (TokenModifierDefinition definition in definitions)
            {
                modifiers.Add(definition.GetSanitized());
            }
        }

        /// <summary>
        /// summary: 设置当前词元在被接受时是否要覆盖最终子弹显示文本。
        /// param: enabled 是否启用文本覆盖
        /// param: content 需要写入的最终子弹文本
        /// returns: 无
        /// </summary>
        public void SetBulletTextOverride(bool enabled, string content)
        {
            hasBulletTextOverride = enabled;
            bulletTextOverride = content ?? string.Empty;
        }

        protected virtual void OnValidate()
        {
            tokenId = tokenId != null ? tokenId.Trim() : string.Empty;
            displayText ??= string.Empty;
            description ??= string.Empty;
            bulletTextOverride ??= string.Empty;
            modifiers ??= new List<TokenModifierDefinition>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i] = modifiers[i].GetSanitized();
            }
        }

        protected virtual void OnEnable()
        {
        }
    }

    public enum TokenType
    {
        None = 0,
        Pre = 1,
        Core = 2,
        Behavior = 3,
        Value = 4,
        Result = 5,
        Post = 6,
    }

    /// <summary>
    /// 指定一条 token 修饰表达式要命中的最终属性。
    /// </summary>
    public enum TokenModifierTarget
    {
        TextColor = 0,
        FontSize = 1,
        ScaleMultiplier = 2,
        ProjectileSpeed = 3,
        MaxLifetime = 4,
        MaxTravelDistance = 5,
        ImpactRadiusMultiplier = 6,
    }

    /// <summary>
    /// 描述一条 token 修饰表达式的目标属性与 DSL 文本。
    /// </summary>
    [Serializable]
    public struct TokenModifierDefinition
    {
        public TokenModifierTarget target;
        public string expression;

        public TokenModifierDefinition(TokenModifierTarget target, string expression)
        {
            this.target = target;
            this.expression = expression ?? string.Empty;
        }

        /// <summary>
        /// summary: 清理当前修饰定义中的可规范化字段，避免空引用和多余空白进入编译流程。
        /// param: 无
        /// returns: 经过规范化后的修饰定义副本
        /// </summary>
        public TokenModifierDefinition GetSanitized()
        {
            TokenModifierDefinition sanitized = this;
            sanitized.expression = sanitized.expression != null ? sanitized.expression.Trim() : string.Empty;
            return sanitized;
        }
    }
}
