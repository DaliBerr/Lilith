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
    public struct SpellCastRuntimeModifiers
    {
        public bool hasValues;
        public float damageMultiplier;
        public float castCooldownMultiplier;
        public float energyCostMultiplier;
        public float casterHealthCost;
        public float dropChanceMultiplierOnKill;
        public float angleSpreadMultiplier;
        public float movementVarianceMultiplier;

        public static SpellCastRuntimeModifiers Identity => new()
        {
            hasValues = true,
            damageMultiplier = 1f,
            castCooldownMultiplier = 1f,
            energyCostMultiplier = 1f,
            casterHealthCost = 0f,
            dropChanceMultiplierOnKill = 1f,
            angleSpreadMultiplier = 1f,
            movementVarianceMultiplier = 1f,
        };

        public SpellCastRuntimeModifiers GetSanitized()
        {
            if (!hasValues)
            {
                return Identity;
            }

            SpellCastRuntimeModifiers sanitized = this;
            sanitized.hasValues = true;
            sanitized.damageMultiplier = Mathf.Max(0f, sanitized.damageMultiplier);
            sanitized.castCooldownMultiplier = Mathf.Max(0f, sanitized.castCooldownMultiplier);
            sanitized.energyCostMultiplier = Mathf.Max(0f, sanitized.energyCostMultiplier);
            sanitized.casterHealthCost = Mathf.Max(0f, sanitized.casterHealthCost);
            sanitized.dropChanceMultiplierOnKill = Mathf.Max(0f, sanitized.dropChanceMultiplierOnKill);
            sanitized.angleSpreadMultiplier = Mathf.Max(0f, sanitized.angleSpreadMultiplier);
            sanitized.movementVarianceMultiplier = Mathf.Max(0f, sanitized.movementVarianceMultiplier);
            return sanitized;
        }
    }

    public enum SpellStatusSlot
    {
        None = 0,
        Ignite = 1,
        Freeze = 2,
        Wet = 3,
        Corrosion = 4,
        Disable = 5,
        Bind = 6,
        Mark = 7,
        Polymorph = 8,
        PuppetMark = 9,
    }

    public enum SpellElementReactionType
    {
        None = 0,
        ThermalCrack = 1,
        ElectroCharged = 2,
        ConductiveThunder = 3,
        ToxicBurst = 4,
        ToxicSpread = 5,
        Shatter = 6,
        LightCorrosion = 7,
        ShadowDevour = 8,
        MirrorReturn = 9,
    }

    [Serializable]
    public struct SpellStatusApplication
    {
        public SpellStatusSlot slot;
        public float amount;
        public float threshold;
        public float duration;
        public float strength;

        public SpellStatusApplication(
            SpellStatusSlot slot,
            float amount,
            float threshold = 1f,
            float duration = 0f,
            float strength = 0f)
        {
            this.slot = slot;
            this.amount = amount;
            this.threshold = threshold;
            this.duration = duration;
            this.strength = strength;
        }

        public SpellStatusApplication GetSanitized()
        {
            SpellStatusApplication sanitized = this;
            sanitized.amount = Mathf.Max(0f, sanitized.amount);
            sanitized.threshold = Mathf.Max(0f, sanitized.threshold);
            sanitized.duration = Mathf.Max(0f, sanitized.duration);
            sanitized.strength = Mathf.Max(0f, sanitized.strength);
            return sanitized;
        }
    }

    public readonly struct SpellElementReactionResult
    {
        public SpellElementReactionResult(SpellElementReactionType reactionType, SpellStatusSlot firstSlot, SpellStatusSlot secondSlot)
        {
            ReactionType = reactionType;
            FirstSlot = firstSlot;
            SecondSlot = secondSlot;
        }

        public SpellElementReactionType ReactionType { get; }
        public SpellStatusSlot FirstSlot { get; }
        public SpellStatusSlot SecondSlot { get; }
        public bool HasReaction => ReactionType != SpellElementReactionType.None;
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
        public bool piercesActorsAndEnvironment;
        public float penetrationDamageMultiplier;
        public bool suppressImpactEffects;
        public float windPressureRadius;
        public float windPressureDistance;
        public float windDisplacementWeightLimit;
        public SpellStatusApplication[] statusApplications;

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
            sanitized.penetrationDamageMultiplier = Mathf.Clamp01(sanitized.penetrationDamageMultiplier);
            sanitized.windPressureRadius = Mathf.Max(0f, sanitized.windPressureRadius);
            sanitized.windPressureDistance = Mathf.Max(0f, sanitized.windPressureDistance);
            sanitized.windDisplacementWeightLimit = Mathf.Max(0f, sanitized.windDisplacementWeightLimit);
            sanitized.statusApplications = SpellStatusApplicationUtility.Sanitize(sanitized.statusApplications);
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

        public bool HasPiercingSuppression =>
            piercesActorsAndEnvironment &&
            penetrationDamageMultiplier > 0f;

        public bool HasWindPressure =>
            windPressureRadius > 0f &&
            windPressureDistance > 0f;

        public bool HasStatusApplications => statusApplications != null && statusApplications.Length > 0;
    }

    [Serializable]
    public struct RandomResultCandidatePayload
    {
        public AttackResultType resultType;
        public ResultEffectPayload resultEffects;

        public RandomResultCandidatePayload(AttackResultType resultType, ResultEffectPayload resultEffects)
        {
            this.resultType = resultType;
            this.resultEffects = resultEffects.GetSanitized();
        }

        public RandomResultCandidatePayload GetSanitized()
        {
            RandomResultCandidatePayload sanitized = this;
            sanitized.resultEffects = sanitized.resultEffects.GetSanitized();
            if (sanitized.resultType == AttackResultType.None ||
                sanitized.resultType == AttackResultType.Confuse)
            {
                sanitized.resultType = AttackResultType.None;
                sanitized.resultEffects = default;
            }

            return sanitized;
        }

        public bool IsValid => resultType != AttackResultType.None && resultType != AttackResultType.Confuse;
    }

    [Serializable]
    public struct ResultEffectPayload
    {
        public float explosionRadius;
        public float explosionDamageMultiplier;
        public float explosionDelaySeconds;
        public float effectRadius;
        public int splitProjectileCount;
        public float splitDamageMultiplier;
        public int controlTriggerCount;
        public float controlDuration;
        public float healingMultiplier;
        public float effectDuration;
        public float effectStrength;
        public float areaTickSeconds;
        public float areaDamageMultiplier;
        public float shieldDuration;
        public SpellStatusApplication[] statusApplications;
        public RandomResultCandidatePayload[] randomResultCandidates;

        public ResultEffectPayload GetSanitized()
        {
            ResultEffectPayload sanitized = this;
            sanitized.explosionRadius = Mathf.Max(0f, sanitized.explosionRadius);
            sanitized.explosionDamageMultiplier = Mathf.Clamp01(sanitized.explosionDamageMultiplier);
            sanitized.explosionDelaySeconds = Mathf.Max(0f, sanitized.explosionDelaySeconds);
            sanitized.effectRadius = Mathf.Max(0f, sanitized.effectRadius);
            sanitized.splitProjectileCount = Mathf.Max(0, sanitized.splitProjectileCount);
            sanitized.splitDamageMultiplier = Mathf.Clamp01(sanitized.splitDamageMultiplier);
            sanitized.controlTriggerCount = Mathf.Max(0, sanitized.controlTriggerCount);
            sanitized.controlDuration = Mathf.Max(0f, sanitized.controlDuration);
            sanitized.healingMultiplier = Mathf.Max(0f, sanitized.healingMultiplier);
            sanitized.effectDuration = Mathf.Max(0f, sanitized.effectDuration);
            sanitized.effectStrength = Mathf.Max(0f, sanitized.effectStrength);
            sanitized.areaTickSeconds = Mathf.Max(0f, sanitized.areaTickSeconds);
            sanitized.areaDamageMultiplier = Mathf.Max(0f, sanitized.areaDamageMultiplier);
            sanitized.shieldDuration = Mathf.Max(0f, sanitized.shieldDuration);
            sanitized.statusApplications = SpellStatusApplicationUtility.Sanitize(sanitized.statusApplications);
            sanitized.randomResultCandidates = SanitizeRandomResultCandidates(sanitized.randomResultCandidates);
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

        public bool HasHealingArea =>
            effectRadius > 0f &&
            healingMultiplier > 0f;

        public bool HasLingeringArea =>
            effectRadius > 0f &&
            effectDuration > 0f &&
            areaTickSeconds > 0f &&
            areaDamageMultiplier > 0f;

        public bool HasDisplacement =>
            effectRadius > 0f &&
            effectStrength > 0f;

        public bool HasRandomResultCandidates => randomResultCandidates != null && randomResultCandidates.Length > 0;

        public bool HasStatusApplications => statusApplications != null && statusApplications.Length > 0;

        private static RandomResultCandidatePayload[] SanitizeRandomResultCandidates(RandomResultCandidatePayload[] candidates)
        {
            if (candidates == null || candidates.Length <= 0)
            {
                return Array.Empty<RandomResultCandidatePayload>();
            }

            List<RandomResultCandidatePayload> sanitizedCandidates = new(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                RandomResultCandidatePayload candidate = candidates[i].GetSanitized();
                if (!candidate.IsValid)
                {
                    continue;
                }

                sanitizedCandidates.Add(candidate);
            }

            return sanitizedCandidates.ToArray();
        }
    }

    internal static class SpellStatusApplicationUtility
    {
        public static SpellStatusApplication[] Sanitize(SpellStatusApplication[] applications)
        {
            if (applications == null || applications.Length <= 0)
            {
                return Array.Empty<SpellStatusApplication>();
            }

            List<SpellStatusApplication> sanitizedApplications = new(applications.Length);
            for (int i = 0; i < applications.Length; i++)
            {
                SpellStatusApplication application = applications[i].GetSanitized();
                if (application.slot == SpellStatusSlot.None || application.amount <= 0f)
                {
                    continue;
                }

                sanitizedApplications.Add(application);
            }

            return sanitizedApplications.ToArray();
        }
    }

    /// <summary>
    /// 表示 SpellProgram 内部单个 projectile node 的编译结果。
    /// </summary>
    [Serializable]
    internal readonly struct ResolvedModifierTokenData
    {
        public ResolvedModifierTokenData(ModifierTokenData sourceToken, SpellModifierScope scope, int targetCount)
        {
            SourceToken = sourceToken;
            Scope = scope;
            TargetCount = Mathf.Max(1, targetCount);
        }

        public ModifierTokenData SourceToken { get; }
        public SpellModifierScope Scope { get; }
        public int TargetCount { get; }
    }

    [Serializable]
    internal sealed class SpellProjectileCompileResult
    {
        private readonly List<AttackCompileMessage> messages = new();
        private readonly List<ResolvedModifierTokenData> modifierTokens = new();
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
        public SpellCastRuntimeModifiers RuntimeModifiers { get; set; } = SpellCastRuntimeModifiers.Identity;

        public IReadOnlyList<AttackCompileMessage> Messages => messages;
        public IReadOnlyList<ResolvedModifierTokenData> ModifierTokens => modifierTokens;
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

        public void AddMessage(AttackCompileMessage message)
        {
            messages.Add(message);
        }

        /// <summary>
        /// summary: 记录一个正式 modifier token，供 SpellProgram IR 表达作用域。
        /// param: token 需要记录的 modifier 词元
        /// returns: 无
        /// </summary>
        public void AddModifierToken(ModifierTokenData token, SpellModifierScope scope, int targetCount = 1)
        {
            if (token != null)
            {
                modifierTokens.Add(new ResolvedModifierTokenData(token, scope, targetCount));
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
