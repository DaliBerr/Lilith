using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    public enum SpellNodeKind
    {
        Projectile = 0,
        Modifier = 1,
        Payload = 2,
        PayloadEffect = 3,
    }

    public enum SpellModifierScope
    {
        NextToken = 0,
        NextN = 1,
        CurrentBlock = 2,
        CurrentPayload = 3,
        GlobalProgram = 4,
    }

    public enum SpellModifierOrigin
    {
        Unknown = 0,
        ModifierToken = 1,
    }

    public enum SpellTriggerType
    {
        None = 0,
        OnHit = 1,
        OnTimer = 2,
        OnExpire = 3,
        OnKill = 4,
        OnDistance = 5,
        OnProximity = 6,
    }

    public enum SpellTriggerParameterKind
    {
        None = 0,
        TimeSeconds = 1,
        Distance = 2,
        Radius = 3,
    }

    public enum SpellTriggerPointKind
    {
        ImpactPoint = 0,
        ProjectilePosition = 1,
        ExpirePoint = 2,
        DeathTargetPosition = 3,
        NearestTargetPoint = 4,
    }

    public enum SpellCastPattern
    {
        Simultaneous = 0,
        Sequential = 1,
        Fork = 2,
        Orbit = 3,
    }

    [Serializable]
    public abstract class SpellNode
    {
        protected SpellNode(SpellNodeKind kind, string nodeId)
        {
            Kind = kind;
            NodeId = nodeId ?? string.Empty;
        }

        public SpellNodeKind Kind { get; }
        public string NodeId { get; }
    }

    [Serializable]
    public sealed class SpellModifierNode : SpellNode
    {
        public SpellModifierNode(BaseTokenData sourceToken, SpellModifierScope scope, SpellModifierOrigin origin, int targetCount = 1)
            : base(SpellNodeKind.Modifier, sourceToken != null ? sourceToken.TokenId : string.Empty)
        {
            SourceToken = sourceToken;
            Scope = scope;
            Origin = origin;
            TargetCount = Mathf.Max(1, targetCount);
        }

        public BaseTokenData SourceToken { get; }
        public SpellModifierScope Scope { get; }
        public SpellModifierOrigin Origin { get; }
        public int TargetCount { get; }
    }

    [Serializable]
    public sealed class SpellProjectileNode : SpellNode
    {
        private readonly List<SpellPayloadBlock> payloads = new();
        private readonly List<RuntimeNumericModifier> fontSizeModifiers = new();

        private SpellProjectileNode(SpellProjectileCompileResult projectileResult)
            : base(SpellNodeKind.Projectile, projectileResult != null ? projectileResult.DisplayText : string.Empty)
        {
            if (projectileResult == null)
            {
                AttackSpec = AttackSpec.CreateDefault();
                CoreType = AttackCoreType.None;
                BehaviorType = AttackBehaviorType.None;
                ResultType = AttackResultType.None;
                DisplayText = string.Empty;
                ProjectileCount = 0;
                SpreadAngleStep = 0f;
                CanFire = false;
                CoreEffects = default;
                ResultEffects = default;
                HasExplosion = false;
                ExplosionRadius = 0f;
                ScaleMultiplier = 1f;
                ImpactRadiusMultiplier = 1f;
                HasTextColorOverride = false;
                TextColor = Color.white;
                HasFontSizeOverride = false;
                FontSize = 0f;
                RuntimeModifiers = SpellCastRuntimeModifiers.Identity;
                return;
            }

            AttackSpec = projectileResult.AttackSpec;
            CoreType = projectileResult.CoreType;
            BehaviorType = projectileResult.BehaviorType;
            ResultType = projectileResult.ResultType;
            DisplayText = projectileResult.DisplayText ?? string.Empty;
            ProjectileCount = projectileResult.GetProjectileCount();
            SpreadAngleStep = projectileResult.SpreadAngleStep;
            CanFire = projectileResult.CanFire;
            CoreEffects = projectileResult.CoreEffects.GetSanitized();
            ResultEffects = projectileResult.ResultEffects.GetSanitized();
            HasExplosion = projectileResult.HasExplosion;
            ExplosionRadius = Mathf.Max(0f, projectileResult.ExplosionRadius);
            ScaleMultiplier = Mathf.Max(0f, projectileResult.ScaleMultiplier);
            ImpactRadiusMultiplier = Mathf.Max(0f, projectileResult.ImpactRadiusMultiplier);
            HasTextColorOverride = projectileResult.HasTextColorOverride;
            TextColor = projectileResult.TextColor;
            HasFontSizeOverride = projectileResult.HasFontSizeOverride;
            FontSize = Mathf.Max(0f, projectileResult.FontSize);
            RuntimeModifiers = projectileResult.RuntimeModifiers.GetSanitized();
            for (int i = 0; i < projectileResult.FontSizeModifiers.Count; i++)
            {
                fontSizeModifiers.Add(projectileResult.FontSizeModifiers[i]);
            }
        }

        private SpellProjectileNode(
            SpellProjectileNode source,
            AttackSpec attackSpec,
            AttackResultType resultType,
            ResultEffectPayload resultEffects,
            bool hasExplosion,
            float explosionRadius,
            bool copyPayloads,
            AttackCoreType? coreTypeOverride = null,
            CoreEffectPayload? coreEffectsOverride = null,
            string displayTextOverride = null)
            : base(SpellNodeKind.Projectile, source != null ? source.NodeId : string.Empty)
        {
            AttackSpec = attackSpec.GetSanitized();
            CoreType = coreTypeOverride ?? source.CoreType;
            BehaviorType = AttackSpec.behaviorType;
            ResultType = resultType;
            DisplayText = displayTextOverride ?? source.DisplayText ?? string.Empty;
            ProjectileCount = BehaviorType == AttackBehaviorType.Spread ? Mathf.Max(1, AttackSpec.projectileCount) : 1;
            SpreadAngleStep = BehaviorType == source.BehaviorType ? source.SpreadAngleStep : 0f;
            CanFire = source.CanFire;
            CoreEffects = (coreEffectsOverride ?? source.CoreEffects).GetSanitized();
            ResultEffects = resultEffects.GetSanitized();
            HasExplosion = hasExplosion && ResultEffects.HasExplosion;
            ExplosionRadius = Mathf.Max(0f, explosionRadius);
            ScaleMultiplier = Mathf.Max(0f, source.ScaleMultiplier);
            ImpactRadiusMultiplier = Mathf.Max(0f, source.ImpactRadiusMultiplier);
            HasTextColorOverride = source.HasTextColorOverride;
            TextColor = source.TextColor;
            HasFontSizeOverride = source.HasFontSizeOverride;
            FontSize = Mathf.Max(0f, source.FontSize);
            RuntimeModifiers = source.RuntimeModifiers.GetSanitized();
            for (int i = 0; i < source.FontSizeModifiers.Count; i++)
            {
                fontSizeModifiers.Add(source.FontSizeModifiers[i]);
            }

            if (!copyPayloads)
            {
                return;
            }

            for (int i = 0; i < source.Payloads.Count; i++)
            {
                if (source.Payloads[i] != null)
                {
                    payloads.Add(source.Payloads[i]);
                }
            }
        }

        public AttackSpec AttackSpec { get; }
        public AttackCoreType CoreType { get; }
        public AttackBehaviorType BehaviorType { get; }
        public AttackResultType ResultType { get; }
        public string DisplayText { get; }
        public int ProjectileCount { get; }
        public float SpreadAngleStep { get; }
        public bool CanFire { get; }
        public CoreEffectPayload CoreEffects { get; }
        public ResultEffectPayload ResultEffects { get; }
        public bool HasExplosion { get; }
        public float ExplosionRadius { get; }
        public float ScaleMultiplier { get; }
        public float ImpactRadiusMultiplier { get; }
        public bool HasTextColorOverride { get; }
        public Color TextColor { get; }
        public bool HasFontSizeOverride { get; }
        public float FontSize { get; }
        public SpellCastRuntimeModifiers RuntimeModifiers { get; }
        public IReadOnlyList<SpellPayloadBlock> Payloads => payloads;
        public IReadOnlyList<RuntimeNumericModifier> FontSizeModifiers => fontSizeModifiers;

        internal static SpellProjectileNode CreateFromProjectileResult(SpellProjectileCompileResult projectileResult)
        {
            return new SpellProjectileNode(projectileResult);
        }

        public static SpellProjectileNode CreateDirectDamageChild(SpellProjectileNode source, float damage)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec childSpec = source.AttackSpec;
            childSpec.damage = Mathf.Max(0f, damage);
            childSpec.resultType = AttackResultType.DirectDamage;
            return new SpellProjectileNode(
                source,
                childSpec,
                AttackResultType.DirectDamage,
                default,
                hasExplosion: false,
                explosionRadius: 0f,
                copyPayloads: false);
        }

        public static SpellProjectileNode CreateDerivedCoreStatusChild(SpellProjectileNode source, float damage)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec childSpec = source.AttackSpec;
            childSpec.damage = Mathf.Max(0f, damage);
            childSpec.behaviorType = AttackBehaviorType.Straight;
            childSpec.resultType = AttackResultType.DirectDamage;
            childSpec.projectileCount = 1;
            childSpec.bounceCount = 0;
            childSpec.chainCount = 0;
            childSpec.pierceCount = 0;
            childSpec.projectileLife = 1;
            childSpec.behaviorParameter = 0f;
            return new SpellProjectileNode(
                source,
                childSpec,
                AttackResultType.DirectDamage,
                default,
                hasExplosion: false,
                explosionRadius: 0f,
                copyPayloads: false);
        }

        public static SpellProjectileNode CreateWithAttackSpecOverride(SpellProjectileNode source, AttackSpec attackSpec)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec overriddenSpec = attackSpec.GetSanitized();
            overriddenSpec.coreType = source.CoreType;
            overriddenSpec.behaviorType = source.BehaviorType;
            overriddenSpec.resultType = source.ResultType;
            return new SpellProjectileNode(
                source,
                overriddenSpec,
                source.ResultType,
                source.ResultEffects,
                source.HasExplosion,
                source.ExplosionRadius,
                copyPayloads: true);
        }

        public static SpellProjectileNode CreateWithRuntimeMovementOverride(SpellProjectileNode source, AttackSpec attackSpec)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec overriddenSpec = attackSpec.GetSanitized();
            overriddenSpec.coreType = source.CoreType;
            overriddenSpec.resultType = source.ResultType;
            return new SpellProjectileNode(
                source,
                overriddenSpec,
                source.ResultType,
                source.ResultEffects,
                source.HasExplosion,
                source.ExplosionRadius,
                copyPayloads: true);
        }

        public static SpellProjectileNode CreateWithRuntimeCoreOverride(
            SpellProjectileNode source,
            AttackSpec attackSpec,
            AttackCoreType coreType,
            CoreEffectPayload coreEffects,
            string displayText)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec overriddenSpec = attackSpec.GetSanitized();
            overriddenSpec.coreType = coreType;
            overriddenSpec.behaviorType = source.BehaviorType;
            overriddenSpec.resultType = source.ResultType;
            return new SpellProjectileNode(
                source,
                overriddenSpec,
                source.ResultType,
                source.ResultEffects,
                source.HasExplosion,
                source.ExplosionRadius,
                copyPayloads: true,
                coreTypeOverride: coreType,
                coreEffectsOverride: coreEffects,
                displayTextOverride: displayText);
        }

        internal void AddPayload(SpellPayloadBlock payload)
        {
            if (payload != null)
            {
                payloads.Add(payload);
            }
        }

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

    [Serializable]
    public sealed class SpellPayloadBlock : SpellNode
    {
        public SpellPayloadBlock(string payloadId, SpellTriggerType triggerType, SpellCastBlock innerBlock)
            : this(
                payloadId,
                triggerType,
                SpellTriggerParameterKind.None,
                0f,
                ResolveDefaultTriggerPoint(triggerType),
                innerBlock)
        {
        }

        public SpellPayloadBlock(
            string payloadId,
            SpellTriggerType triggerType,
            SpellTriggerParameterKind parameterKind,
            float parameterValue,
            SpellTriggerPointKind triggerPointKind,
            SpellCastBlock innerBlock)
            : base(SpellNodeKind.Payload, payloadId)
        {
            TriggerType = triggerType == SpellTriggerType.None ? SpellTriggerType.OnHit : triggerType;
            ParameterKind = parameterKind;
            ParameterValue = Mathf.Max(0f, parameterValue);
            TriggerPointKind = triggerPointKind;
            InnerBlock = innerBlock;
        }

        public SpellTriggerType TriggerType { get; }
        public SpellTriggerParameterKind ParameterKind { get; }
        public float ParameterValue { get; }
        public SpellTriggerPointKind TriggerPointKind { get; }
        public SpellCastBlock InnerBlock { get; }

        private static SpellTriggerPointKind ResolveDefaultTriggerPoint(SpellTriggerType triggerType)
        {
            return triggerType switch
            {
                SpellTriggerType.OnTimer => SpellTriggerPointKind.ProjectilePosition,
                SpellTriggerType.OnExpire => SpellTriggerPointKind.ExpirePoint,
                SpellTriggerType.OnKill => SpellTriggerPointKind.DeathTargetPosition,
                SpellTriggerType.OnDistance => SpellTriggerPointKind.ProjectilePosition,
                SpellTriggerType.OnProximity => SpellTriggerPointKind.ProjectilePosition,
                _ => SpellTriggerPointKind.ImpactPoint,
            };
        }
    }

    [Serializable]
    public sealed class SpellPayloadEffectNode : SpellNode
    {
        public SpellPayloadEffectNode(ResultTokenData sourceToken, ResultEffectPayload resultEffects)
            : base(SpellNodeKind.PayloadEffect, sourceToken != null ? sourceToken.TokenId : string.Empty)
        {
            SourceToken = sourceToken;
            ResultType = sourceToken != null ? sourceToken.ResultType : AttackResultType.None;
            ResultEffects = resultEffects.GetSanitized();
            DisplayText = sourceToken != null ? sourceToken.GetResolvedDisplayText() : string.Empty;
        }

        public ResultTokenData SourceToken { get; }
        public AttackResultType ResultType { get; }
        public ResultEffectPayload ResultEffects { get; }
        public string DisplayText { get; }
    }

    [Serializable]
    public sealed class SpellCastBlock
    {
        private readonly List<SpellModifierNode> modifiers = new();
        private readonly List<SpellProjectileNode> projectiles = new();
        private readonly List<SpellPayloadBlock> payloads = new();
        private readonly List<SpellPayloadEffectNode> payloadEffects = new();

        public SpellCastBlock(string blockId, int depth)
            : this(blockId, depth, SpellCastPattern.Simultaneous, 0.12f, 15f)
        {
        }

        public SpellCastBlock(
            string blockId,
            int depth,
            SpellCastPattern castPattern,
            float sequentialIntervalSeconds,
            float patternAngleStep)
        {
            BlockId = blockId ?? string.Empty;
            Depth = Mathf.Max(0, depth);
            CastPattern = castPattern;
            SequentialIntervalSeconds = Mathf.Max(0f, sequentialIntervalSeconds);
            PatternAngleStep = Mathf.Max(0f, patternAngleStep);
        }

        public string BlockId { get; }
        public int Depth { get; }
        public SpellCastPattern CastPattern { get; }
        public float SequentialIntervalSeconds { get; }
        public float PatternAngleStep { get; }
        public IReadOnlyList<SpellModifierNode> Modifiers => modifiers;
        public IReadOnlyList<SpellProjectileNode> Projectiles => projectiles;
        public IReadOnlyList<SpellPayloadBlock> Payloads => payloads;
        public IReadOnlyList<SpellPayloadEffectNode> PayloadEffects => payloadEffects;

        internal void AddModifier(SpellModifierNode modifier)
        {
            if (modifier != null)
            {
                modifiers.Add(modifier);
            }
        }

        internal void AddProjectile(SpellProjectileNode projectile)
        {
            if (projectile != null)
            {
                projectiles.Add(projectile);
            }
        }

        internal void AddPayload(SpellPayloadBlock payload)
        {
            if (payload != null)
            {
                payloads.Add(payload);
            }
        }

        internal void AddPayloadEffect(SpellPayloadEffectNode payloadEffect)
        {
            if (payloadEffect != null)
            {
                payloadEffects.Add(payloadEffect);
            }
        }
    }

    [Serializable]
    public sealed class CompiledSpellProgram
    {
        private readonly List<SpellCastBlock> castBlocks = new();
        private readonly List<AttackCompileMessage> messages = new();

        public IReadOnlyList<SpellCastBlock> CastBlocks => castBlocks;
        public IReadOnlyList<AttackCompileMessage> Messages => messages;
        public SpellCastBlock PrimaryCastBlock => castBlocks.Count > 0 ? castBlocks[0] : null;
        public bool CanCast { get; private set; }
        public SpellCastRuntimeModifiers RuntimeModifiers { get; private set; } = SpellCastRuntimeModifiers.Identity;
        public int MaxPayloadDepth { get; private set; } = 4;
        public int MaxPayloadNodeCount { get; private set; } = 32;
        public int MaxDerivedProjectileCount { get; private set; } = 64;

        internal static CompiledSpellProgram CreateFromProjectileResult(SpellProjectileCompileResult projectileResult)
        {
            CompiledSpellProgram program = new()
            {
                CanCast = projectileResult != null && projectileResult.CanFire,
                RuntimeModifiers = projectileResult != null
                    ? projectileResult.RuntimeModifiers.GetSanitized()
                    : SpellCastRuntimeModifiers.Identity,
            };
            program.CopyMessages(projectileResult);

            if (projectileResult == null || !projectileResult.CanFire)
            {
                return program;
            }

            SpellCastBlock block = new("outer", 0);
            program.AddModifierTokens(block, projectileResult.ModifierTokens);
            block.AddProjectile(SpellProjectileNode.CreateFromProjectileResult(projectileResult));
            program.castBlocks.Add(block);
            return program;
        }

        internal static CompiledSpellProgram CreateFromProjectileResults(
            IReadOnlyList<SpellProjectileCompileResult> projectileResults,
            IReadOnlyList<SpellModifierNode> blockModifiers,
            IReadOnlyList<AttackCompileMessage> extraMessages)
        {
            CompiledSpellProgram program = new();
            program.CopyMessages(extraMessages);

            SpellCastBlock block = new("outer", 0);
            if (blockModifiers != null)
            {
                for (int i = 0; i < blockModifiers.Count; i++)
                {
                    block.AddModifier(blockModifiers[i]);
                }
            }

            if (projectileResults != null)
            {
                for (int i = 0; i < projectileResults.Count; i++)
                {
                    SpellProjectileCompileResult projectileResult = projectileResults[i];
                    program.CopyMessages(projectileResult);
                    if (projectileResult == null || !projectileResult.CanFire)
                    {
                        continue;
                    }

                    block.AddProjectile(SpellProjectileNode.CreateFromProjectileResult(projectileResult));
                    program.AddModifierTokens(block, projectileResult.ModifierTokens);
                }
            }

            if (block.Projectiles.Count > 0)
            {
                program.castBlocks.Add(block);
            }

            program.CanCast = block.Projectiles.Count > 0;
            return program;
        }

        internal static CompiledSpellProgram CreateFromCastBlock(SpellCastBlock block, IReadOnlyList<AttackCompileMessage> extraMessages)
        {
            CompiledSpellProgram program = new();
            program.CopyMessages(extraMessages);
            if (block == null || block.Projectiles.Count <= 0)
            {
                program.CanCast = false;
                return program;
            }

            program.castBlocks.Add(block);
            program.CanCast = true;
            return program;
        }

        internal void SetRuntimeModifiers(SpellCastRuntimeModifiers modifiers)
        {
            RuntimeModifiers = modifiers.GetSanitized();
        }

        internal void AttachPayloadToPrimaryBlockProjectiles(SpellPayloadBlock payload)
        {
            SpellCastBlock block = PrimaryCastBlock;
            if (payload == null || block == null)
            {
                return;
            }

            block.AddPayload(payload);
            for (int i = 0; i < block.Projectiles.Count; i++)
            {
                block.Projectiles[i]?.AddPayload(payload);
            }
        }

        internal void AddMessage(AttackCompileMessage message)
        {
            messages.Add(message);
        }

        public bool TryGetPrimaryProjectile(out SpellProjectileNode projectile)
        {
            projectile = null;
            SpellCastBlock block = PrimaryCastBlock;
            if (block == null || block.Projectiles.Count <= 0)
            {
                return false;
            }

            projectile = block.Projectiles[0];
            return projectile != null && projectile.CanFire;
        }

        private void CopyMessages(SpellProjectileCompileResult projectileResult)
        {
            if (projectileResult == null)
            {
                return;
            }

            for (int i = 0; i < projectileResult.Messages.Count; i++)
            {
                messages.Add(projectileResult.Messages[i]);
            }
        }

        private void CopyMessages(IReadOnlyList<AttackCompileMessage> sourceMessages)
        {
            if (sourceMessages == null)
            {
                return;
            }

            for (int i = 0; i < sourceMessages.Count; i++)
            {
                messages.Add(sourceMessages[i]);
            }
        }

        private void AddModifierTokens(SpellCastBlock block, IReadOnlyList<ResolvedModifierTokenData> tokens)
        {
            if (block == null || tokens == null)
            {
                return;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                ResolvedModifierTokenData token = tokens[i];
                if (token.SourceToken != null)
                {
                    AddModifierIfMissing(
                        block,
                        new SpellModifierNode(
                            token.SourceToken,
                            token.Scope,
                            SpellModifierOrigin.ModifierToken,
                            token.TargetCount));
                }
            }
        }

        private static void AddModifierIfMissing(SpellCastBlock block, SpellModifierNode modifier)
        {
            if (block == null || modifier == null)
            {
                return;
            }

            for (int i = 0; i < block.Modifiers.Count; i++)
            {
                SpellModifierNode existing = block.Modifiers[i];
                if (existing != null &&
                    existing.SourceToken == modifier.SourceToken &&
                    existing.Scope == modifier.Scope &&
                    existing.Origin == modifier.Origin)
                {
                    return;
                }
            }

            block.AddModifier(modifier);
        }
    }
}
