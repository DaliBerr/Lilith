using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    public enum AttackCompileMessageSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    [Serializable]
    public struct AttackCompileMessage
    {
        public AttackCompileMessageSeverity severity;
        public string message;
        public string tokenId;

        public AttackCompileMessage(AttackCompileMessageSeverity severity, string message, string tokenId)
        {
            this.severity = severity;
            this.message = message ?? string.Empty;
            this.tokenId = tokenId ?? string.Empty;
        }
    }

    [Serializable]
    public struct RuntimeNumericModifier
    {
        public TokenModifierOperator operation;
        public float value;

        public RuntimeNumericModifier(TokenModifierOperator operation, float value)
        {
            this.operation = operation;
            this.value = value;
        }
    }

    /// <summary>
    /// 表示一串词元被编译后的可执行攻击结果。
    /// </summary>
    [Serializable]
    public sealed class CompiledAttack
    {
        private readonly List<AttackCompileMessage> messages = new();
        private readonly List<BaseTokenData> preTokens = new();
        private readonly List<BaseTokenData> postTokens = new();
        private readonly List<RuntimeNumericModifier> fontSizeModifiers = new();

        public AttackSpec AttackSpec { get; set; }
        public AttackCoreType CoreType { get; set; }
        public AttackBehaviorType BehaviorType { get; set; }
        public AttackResultType ResultType { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public bool CanFire { get; set; }
        public int SpreadProjectileCount { get; set; } = 1;
        public float SpreadAngleStep { get; set; }
        public float ExplosionRadius { get; set; }
        public bool HasExplosion { get; set; }
        public float ScaleMultiplier { get; set; } = 1f;
        public float ImpactRadiusMultiplier { get; set; } = 1f;
        public bool HasTextColorOverride { get; set; }
        public Color TextColor { get; set; } = Color.white;
        public bool HasFontSizeOverride { get; set; }
        public float FontSize { get; set; }

        public IReadOnlyList<AttackCompileMessage> Messages => messages;
        public IReadOnlyList<BaseTokenData> PreTokens => preTokens;
        public IReadOnlyList<BaseTokenData> PostTokens => postTokens;
        public IReadOnlyList<RuntimeNumericModifier> FontSizeModifiers => fontSizeModifiers;

        /// <summary>
        /// summary: 判断当前编译结果是否包含错误级别消息。
        /// param: 无
        /// returns: 至少存在一条 error 消息时返回 true
        /// </summary>
        public bool HasErrors()
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].severity == AttackCompileMessageSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// summary: 获取当前行为对应的实际投射物数量。
        /// param: 无
        /// returns: 直射时为 1，散射时返回散射配置的数量
        /// </summary>
        public int GetProjectileCount()
        {
            return BehaviorType == AttackBehaviorType.Spread ? Mathf.Max(1, SpreadProjectileCount) : 1;
        }

        /// <summary>
        /// summary: 向当前编译结果追加一条消息。
        /// param: severity 消息严重级别
        /// param: message 需要记录的提示文本
        /// param: token 当前相关的词元
        /// returns: 无
        /// </summary>
        public void AddMessage(AttackCompileMessageSeverity severity, string message, BaseTokenData token = null)
        {
            messages.Add(new AttackCompileMessage(severity, message, token != null ? token.TokenId : string.Empty));
        }

        /// <summary>
        /// summary: 记录一个前置词元，供后续 UI 或运行时扩展继续消费。
        /// param: token 需要记录的前置词元
        /// returns: 无
        /// </summary>
        public void AddPreToken(BaseTokenData token)
        {
            if (token != null)
            {
                preTokens.Add(token);
            }
        }

        /// <summary>
        /// summary: 记录一个后置词元，供后续 UI 或运行时扩展继续消费。
        /// param: token 需要记录的后置词元
        /// returns: 无
        /// </summary>
        public void AddPostToken(BaseTokenData token)
        {
            if (token != null)
            {
                postTokens.Add(token);
            }
        }

        /// <summary>
        /// summary: 清空当前编译结果里记录的字体尺寸修饰步骤。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ClearFontSizeModifiers()
        {
            fontSizeModifiers.Clear();
            HasFontSizeOverride = false;
            FontSize = 0f;
        }

        /// <summary>
        /// summary: 记录一条将在运行时基于文字容器尺寸执行的字体尺寸修饰步骤。
        /// param: operation 需要应用的操作符
        /// param: value 需要应用的数值载荷
        /// returns: 无
        /// </summary>
        public void AddFontSizeModifier(TokenModifierOperator operation, float value)
        {
            fontSizeModifiers.Add(new RuntimeNumericModifier(operation, value));
            HasFontSizeOverride = true;
            FontSize = value;
        }

        /// <summary>
        /// summary: 基于当前文字容器边长解析最终应应用的方形尺寸。
        /// param: baseSize 运行时文字容器当前的基础边长
        /// returns: 按已记录的修饰步骤累计后的最终边长
        /// </summary>
        public float ResolveFontSize(float baseSize)
        {
            float resolvedSize = Mathf.Max(0f, baseSize);
            if (fontSizeModifiers.Count <= 0)
            {
                return Mathf.Max(0f, FontSize);
            }

            for (int i = 0; i < fontSizeModifiers.Count; i++)
            {
                RuntimeNumericModifier modifier = fontSizeModifiers[i];
                switch (modifier.operation)
                {
                    case TokenModifierOperator.Set:
                        resolvedSize = modifier.value;
                        break;
                    case TokenModifierOperator.Add:
                        resolvedSize += modifier.value;
                        break;
                    case TokenModifierOperator.Subtract:
                        resolvedSize -= modifier.value;
                        break;
                    case TokenModifierOperator.Multiply:
                        resolvedSize *= modifier.value;
                        break;
                    case TokenModifierOperator.Divide:
                        if (!Mathf.Approximately(modifier.value, 0f))
                        {
                            resolvedSize /= modifier.value;
                        }
                        break;
                }
            }

            return Mathf.Max(0f, resolvedSize);
        }

    }
}
