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

    [Serializable]
    public struct CoreEffectPayload
    {
        public string armoredEnemyId;
        public float armoredDamageMultiplier;
        public int burnTriggerCount;
        public float burnDamagePerSecond;
        public float burnDuration;
        public float slowPercent;
        public float slowDuration;
        public int thunderChainTargetCount;
        public float thunderChainRadius;
        public float thunderChainDamage;

        public CoreEffectPayload GetSanitized()
        {
            CoreEffectPayload sanitized = this;
            sanitized.armoredEnemyId = sanitized.armoredEnemyId != null ? sanitized.armoredEnemyId.Trim() : string.Empty;
            sanitized.armoredDamageMultiplier = Mathf.Max(1f, sanitized.armoredDamageMultiplier);
            sanitized.burnTriggerCount = Mathf.Max(0, sanitized.burnTriggerCount);
            sanitized.burnDamagePerSecond = Mathf.Max(0f, sanitized.burnDamagePerSecond);
            sanitized.burnDuration = Mathf.Max(0f, sanitized.burnDuration);
            sanitized.slowPercent = Mathf.Clamp01(sanitized.slowPercent);
            sanitized.slowDuration = Mathf.Max(0f, sanitized.slowDuration);
            sanitized.thunderChainTargetCount = Mathf.Max(0, sanitized.thunderChainTargetCount);
            sanitized.thunderChainRadius = Mathf.Max(0f, sanitized.thunderChainRadius);
            sanitized.thunderChainDamage = Mathf.Max(0f, sanitized.thunderChainDamage);
            return sanitized;
        }

        public bool HasArmoredBonus =>
            !string.IsNullOrWhiteSpace(armoredEnemyId) &&
            armoredDamageMultiplier > 1f;

        public bool HasBurn =>
            burnTriggerCount > 0 &&
            burnDamagePerSecond > 0f &&
            burnDuration > 0f;

        public bool HasSlow =>
            slowPercent > 0f &&
            slowDuration > 0f;

        public bool HasThunderChain =>
            thunderChainTargetCount > 0 &&
            thunderChainRadius > 0f &&
            thunderChainDamage > 0f;
    }

    [Serializable]
    public struct ResultEffectPayload
    {
        public float explosionRadius;
        public float explosionDamageMultiplier;
        public int splitProjectileCount;
        public float splitDamageMultiplier;
        public int controlTriggerCount;
        public float controlDuration;

        public ResultEffectPayload GetSanitized()
        {
            ResultEffectPayload sanitized = this;
            sanitized.explosionRadius = Mathf.Max(0f, sanitized.explosionRadius);
            sanitized.explosionDamageMultiplier = Mathf.Clamp01(sanitized.explosionDamageMultiplier);
            sanitized.splitProjectileCount = Mathf.Max(0, sanitized.splitProjectileCount);
            sanitized.splitDamageMultiplier = Mathf.Clamp01(sanitized.splitDamageMultiplier);
            sanitized.controlTriggerCount = Mathf.Max(0, sanitized.controlTriggerCount);
            sanitized.controlDuration = Mathf.Max(0f, sanitized.controlDuration);
            return sanitized;
        }

        public bool HasExplosion =>
            explosionRadius > 0f &&
            explosionDamageMultiplier > 0f;

        public bool HasSplit =>
            splitProjectileCount > 0 &&
            splitDamageMultiplier > 0f;

        public bool HasControl =>
            controlTriggerCount > 0 &&
            controlDuration > 0f;
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
        public CoreEffectPayload CoreEffects { get; set; }
        public ResultEffectPayload ResultEffects { get; set; }

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

        /// <summary>
        /// summary: 复制当前编译结果，供运行时派生出子弹分裂或临时伤害覆写版本。
        /// param: 无
        /// returns: 一份与当前结果数值等价、但列表已独立拷贝的新实例
        /// </summary>
        public CompiledAttack Clone()
        {
            CompiledAttack clone = new()
            {
                AttackSpec = AttackSpec,
                CoreType = CoreType,
                BehaviorType = BehaviorType,
                ResultType = ResultType,
                DisplayText = DisplayText,
                CanFire = CanFire,
                SpreadProjectileCount = SpreadProjectileCount,
                SpreadAngleStep = SpreadAngleStep,
                ExplosionRadius = ExplosionRadius,
                HasExplosion = HasExplosion,
                ScaleMultiplier = ScaleMultiplier,
                ImpactRadiusMultiplier = ImpactRadiusMultiplier,
                HasTextColorOverride = HasTextColorOverride,
                TextColor = TextColor,
                HasFontSizeOverride = HasFontSizeOverride,
                FontSize = FontSize,
                CoreEffects = CoreEffects,
                ResultEffects = ResultEffects,
            };

            for (int i = 0; i < messages.Count; i++)
            {
                clone.messages.Add(messages[i]);
            }

            for (int i = 0; i < preTokens.Count; i++)
            {
                clone.preTokens.Add(preTokens[i]);
            }

            for (int i = 0; i < postTokens.Count; i++)
            {
                clone.postTokens.Add(postTokens[i]);
            }

            for (int i = 0; i < fontSizeModifiers.Count; i++)
            {
                clone.fontSizeModifiers.Add(fontSizeModifiers[i]);
            }

            return clone;
        }

    }
}
