using System;
using System.Collections.Generic;

namespace Kernel.Bullet
{
    /// <summary>
    /// 把法术书执行序列编译成 SpellProgram；单个 projectile 由 SpellProjectileCompiler 生成内部 compile result。
    /// </summary>
    public static class SpellProgramCompiler
    {
        internal static Func<int, int> ChaosCandidateIndexResolver { get; set; } = count => UnityEngine.Random.Range(0, count);

        private sealed class MulticastCompileState
        {
            public readonly List<ResolvedModifierTokenData> blockModifierTokens = new();
            public readonly List<SpellModifierNode> blockModifiers = new();
            public readonly List<AttackCompileMessage> messages = new();
        }

        private sealed class PendingPayloadModifierEntry
        {
            public ModifierTokenData modifierToken;
            public ValueTokenData countValueToken;
            public int remainingTargetCount;
            public bool hasResolvedTarget;
        }

        private sealed class ExecutorModifierContext
        {
            public ExecutorModifierContext(SpellBookData spellBook)
            {
                SpellBook = spellBook;
            }

            public SpellBookData SpellBook { get; }
            public bool HasModifiers => SpellBook != null && SpellBook.HasExecutorModifiers;
            public IReadOnlyList<TokenModifierDefinition> Modifiers => SpellBook != null ? SpellBook.ExecutorModifiers : null;
            public string SourceLabel => SpellBook != null ? SpellBook.SpellBookId : "spell book";
        }

        private readonly struct ResolvedTriggerParameter
        {
            public ResolvedTriggerParameter(
                SpellTriggerParameterKind kind,
                float value,
                SpellTriggerPointKind triggerPointKind,
                int consumedTokenCount)
            {
                Kind = kind;
                Value = value;
                TriggerPointKind = triggerPointKind;
                ConsumedTokenCount = consumedTokenCount;
            }

            public SpellTriggerParameterKind Kind { get; }
            public float Value { get; }
            public SpellTriggerPointKind TriggerPointKind { get; }
            public int ConsumedTokenCount { get; }
        }

        public static CompiledSpellProgram Compile(IReadOnlyList<BaseTokenData> tokens)
        {
            if (tokens == null)
            {
                return Compile((IReadOnlyList<PlaceableTokenData>)null);
            }

            List<PlaceableTokenData> items = new(tokens.Count);
            for (int i = 0; i < tokens.Count; i++)
            {
                items.Add(tokens[i]);
            }

            return Compile(items);
        }

        public static CompiledSpellProgram Compile(IReadOnlyList<PlaceableTokenData> items)
        {
            return Compile(items, (ExecutorModifierContext)null);
        }

        public static CompiledSpellProgram Compile(IReadOnlyList<PlaceableTokenData> items, SpellBookData spellBook)
        {
            return Compile(items, CreateExecutorModifierContext(spellBook));
        }

        private static CompiledSpellProgram Compile(IReadOnlyList<PlaceableTokenData> items, ExecutorModifierContext executorModifiers)
        {
            List<AttackCompileMessage> messages = new();
            List<BaseTokenData> tokens = ExpandItems(items, messages);
            return CompileProgramFromTokens(tokens, messages, executorModifiers, null, SpellCastRuntimeModifiers.Identity, items);
        }

        public static CompiledSpellProgram CompileForActivation(IReadOnlyList<PlaceableTokenData> items, SpellBookData spellBook)
        {
            List<AttackCompileMessage> messages = new();
            List<BaseTokenData> tokens = ExpandItems(items, messages);
            tokens = ResolveRandomModifierTokens(tokens, messages);
            return CompileProgramFromTokens(
                tokens,
                messages,
                CreateExecutorModifierContext(spellBook),
                null,
                SpellCastRuntimeModifiers.Identity,
                null);
        }

        public static bool ContainsRandomModifier(IReadOnlyList<PlaceableTokenData> items)
        {
            List<BaseTokenData> tokens = ExpandItems(items, null);
            if (tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] is ModifierTokenData modifierToken && modifierToken.IsRandomModifier)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<BaseTokenData> ResolveRandomModifierTokens(
            IReadOnlyList<BaseTokenData> tokens,
            ICollection<AttackCompileMessage> messages)
        {
            List<BaseTokenData> resolvedTokens = new(tokens != null ? tokens.Count : 0);
            if (tokens == null)
            {
                return resolvedTokens;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                BaseTokenData token = tokens[i];
                if (token is not ModifierTokenData modifierToken || !modifierToken.IsRandomModifier)
                {
                    resolvedTokens.Add(token);
                    continue;
                }

                ModifierTokenData candidate = ResolveRandomModifierCandidate(modifierToken);
                if (candidate == null)
                {
                    AddMessage(
                        messages,
                        AttackCompileMessageSeverity.Warning,
                        "Ignored random modifier token because it has no valid candidates.",
                        modifierToken);
                    continue;
                }

                resolvedTokens.Add(candidate);
            }

            return resolvedTokens;
        }

        private static ModifierTokenData ResolveRandomModifierCandidate(ModifierTokenData modifierToken)
        {
            IReadOnlyList<ModifierTokenData> candidates = modifierToken != null ? modifierToken.RandomModifierCandidates : null;
            if (candidates == null || candidates.Count <= 0)
            {
                return null;
            }

            int rawIndex = ChaosCandidateIndexResolver != null
                ? ChaosCandidateIndexResolver(candidates.Count)
                : UnityEngine.Random.Range(0, candidates.Count);
            int resolvedIndex = Clamp(rawIndex, 0, candidates.Count - 1);
            return candidates[resolvedIndex];
        }

        private static CompiledSpellProgram CompileTriggeredProgram(
            IReadOnlyList<BaseTokenData> tokens,
            int triggerIndex,
            IReadOnlyList<AttackCompileMessage> initialMessages,
            ExecutorModifierContext executorModifiers,
            IReadOnlyList<ResolvedModifierTokenData> inheritedProjectileModifiers,
            SpellCastRuntimeModifiers inheritedRuntimeModifiers)
        {
            List<AttackCompileMessage> messages = new();
            CopyMessages(initialMessages, messages);

            TriggerTokenData triggerToken = tokens[triggerIndex] as TriggerTokenData;
            SpellTriggerType triggerType = triggerToken != null ? triggerToken.TriggerType : SpellTriggerType.OnHit;
            if (triggerType == SpellTriggerType.None)
            {
                triggerType = SpellTriggerType.OnHit;
            }

            ResolvedTriggerParameter triggerParameter = ResolveTriggerParameter(tokens, triggerIndex, triggerToken, messages);
            List<BaseTokenData> outerTokens = CopyRange(tokens, 0, triggerIndex);
            CompiledSpellProgram program = CompileProgramFromTokens(
                outerTokens,
                null,
                executorModifiers,
                inheritedProjectileModifiers,
                inheritedRuntimeModifiers);
            int payloadStartIndex = triggerIndex + 1 + triggerParameter.ConsumedTokenCount;
            List<BaseTokenData> payloadTokens = CopyRange(tokens, payloadStartIndex, tokens.Count);
            List<ResolvedModifierTokenData> inheritedPayloadModifiers = CollectGlobalProgramModifiers(outerTokens);
            SpellPayloadBlock payload = CompilePayloadBlock(
                payloadTokens,
                triggerType,
                triggerParameter,
                messages,
                inheritedPayloadModifiers,
                executorModifiers,
                program.RuntimeModifiers);
            if (payload != null && program.CanCast)
            {
                program.AttachPayloadToPrimaryBlockProjectiles(payload);
            }
            else if (payload != null)
            {
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Compiled payload was not attached because the outer trigger formula cannot cast.", triggerToken);
            }

            CopyMessages(messages, program);
            return program;
        }

        private static ResolvedTriggerParameter ResolveTriggerParameter(
            IReadOnlyList<BaseTokenData> tokens,
            int triggerIndex,
            TriggerTokenData triggerToken,
            ICollection<AttackCompileMessage> messages)
        {
            SpellTriggerType triggerType = triggerToken != null ? triggerToken.TriggerType : SpellTriggerType.OnHit;
            SpellTriggerParameterKind parameterKind = triggerToken != null
                ? triggerToken.ParameterKind
                : ResolveDefaultTriggerParameterKind(triggerType);
            SpellTriggerPointKind triggerPointKind = triggerToken != null
                ? triggerToken.TriggerPointKind
                : ResolveDefaultTriggerPointKind(triggerType);

            if (parameterKind == SpellTriggerParameterKind.None)
            {
                return new ResolvedTriggerParameter(parameterKind, 0f, triggerPointKind, 0);
            }

            float defaultValue = triggerToken != null ? triggerToken.DefaultParameterValue : 1f;
            int valueIndex = triggerIndex + 1;
            if (tokens != null &&
                valueIndex < tokens.Count &&
                tokens[valueIndex] is ValueTokenData valueToken)
            {
                float resolvedValue = valueToken.ResolveNumericValue(
                    SpellValueParameterKind.TriggerParameter,
                    defaultValue,
                    allowZero: false);
                return new ResolvedTriggerParameter(parameterKind, MathfMax(0.01f, resolvedValue), triggerPointKind, 1);
            }

            AddMessage(
                messages,
                AttackCompileMessageSeverity.Warning,
                $"Trigger '{ResolveTriggerLabel(triggerType)}' expected a value parameter but none was provided; using default {defaultValue:0.##}.",
                triggerToken);
            return new ResolvedTriggerParameter(parameterKind, MathfMax(0.01f, defaultValue), triggerPointKind, 0);
        }

        private static SpellTriggerParameterKind ResolveDefaultTriggerParameterKind(SpellTriggerType triggerType)
        {
            return triggerType switch
            {
                SpellTriggerType.OnTimer => SpellTriggerParameterKind.TimeSeconds,
                SpellTriggerType.OnDistance => SpellTriggerParameterKind.Distance,
                SpellTriggerType.OnProximity => SpellTriggerParameterKind.Radius,
                _ => SpellTriggerParameterKind.None,
            };
        }

        private static SpellTriggerPointKind ResolveDefaultTriggerPointKind(SpellTriggerType triggerType)
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

        private static string ResolveTriggerLabel(SpellTriggerType triggerType)
        {
            return triggerType switch
            {
                SpellTriggerType.OnTimer => "OnTimer",
                SpellTriggerType.OnExpire => "OnExpire",
                SpellTriggerType.OnKill => "OnKill",
                SpellTriggerType.OnDistance => "OnDistance",
                SpellTriggerType.OnProximity => "OnProximity",
                _ => "OnHit",
            };
        }

        private static CompiledSpellProgram CompileMulticast(
            IReadOnlyList<BaseTokenData> tokens,
            int multicastIndex,
            IReadOnlyList<AttackCompileMessage> initialMessages,
            ExecutorModifierContext executorModifiers,
            IReadOnlyList<ResolvedModifierTokenData> inheritedBlockModifierTokens,
            SpellCastRuntimeModifiers inheritedRuntimeModifiers)
        {
            MulticastCompileState state = new();
            CopyMessages(initialMessages, state.messages);
            ExtractCastRuntimeModifierTokens(
                tokens,
                state.messages,
                inheritedRuntimeModifiers,
                out List<BaseTokenData> filteredTokens,
                out SpellCastRuntimeModifiers runtimeModifiers,
                state.blockModifiers);
            tokens = filteredTokens;
            AddInheritedBlockModifiers(inheritedBlockModifierTokens, state);

            MulticastTokenData multicastToken = tokens[multicastIndex] as MulticastTokenData;
            int requestedCount = multicastToken != null ? multicastToken.CastCount : 2;
            CollectBlockPrefix(tokens, multicastIndex, state);

            List<List<BaseTokenData>> segments = new();
            int index = multicastIndex + 1;
            for (int i = 0; i < requestedCount; i++)
            {
                if (!TryReadProjectileSegment(tokens, ref index, state, out List<BaseTokenData> segmentTokens))
                {
                    AddMessage(
                        state.messages,
                        AttackCompileMessageSeverity.Warning,
                        $"Multicast requested {requestedCount} projectile nodes but only found {segments.Count}.",
                        multicastToken);
                    break;
                }

                segments.Add(segmentTokens);
            }

            WarnIgnoredTrailingTokens(tokens, index, state);

            SpellCastBlock block = new(
                "outer",
                0,
                multicastToken != null ? multicastToken.CastPattern : SpellCastPattern.Simultaneous,
                multicastToken != null ? multicastToken.SequentialIntervalSeconds : 0.12f,
                (multicastToken != null ? multicastToken.PatternAngleStep : 18f) * runtimeModifiers.GetSanitized().angleSpreadMultiplier);
            AddBlockModifiersToCastBlock(block, state.blockModifiers);
            for (int i = 0; i < segments.Count; i++)
            {
                CompiledSpellProgram segmentProgram = CompileProgramFromTokens(
                    segments[i],
                    null,
                    executorModifiers,
                    state.blockModifierTokens,
                    runtimeModifiers);
                CopyMessages(segmentProgram.Messages, state.messages);
                CopyOuterBlockContents(segmentProgram.PrimaryCastBlock, block);
            }

            CompiledSpellProgram program = CompiledSpellProgram.CreateFromCastBlock(block, state.messages);
            program.SetRuntimeModifiers(runtimeModifiers);
            return program;
        }

        private static CompiledSpellProgram CompileProgramFromTokens(
            IReadOnlyList<BaseTokenData> tokens,
            IReadOnlyList<AttackCompileMessage> initialMessages,
            ExecutorModifierContext executorModifiers,
            IReadOnlyList<ResolvedModifierTokenData> inheritedProjectileModifiers,
            SpellCastRuntimeModifiers inheritedRuntimeModifiers,
            IReadOnlyList<PlaceableTokenData> sourceItems = null)
        {
            List<AttackCompileMessage> messages = new();
            CopyMessages(initialMessages, messages);
            int originalTokenCount = tokens != null ? tokens.Count : 0;
            List<SpellModifierNode> runtimeModifierNodes = new();
            ExtractCastRuntimeModifierTokens(
                tokens,
                messages,
                inheritedRuntimeModifiers,
                out List<BaseTokenData> filteredTokens,
                out SpellCastRuntimeModifiers runtimeModifiers,
                runtimeModifierNodes);
            tokens = filteredTokens;

            int triggerIndex = FindFirstTriggerIndex(tokens);
            int multicastIndex = FindFirstMulticastIndex(tokens);
            if (triggerIndex >= 0 && (multicastIndex < 0 || triggerIndex < multicastIndex))
            {
                CompiledSpellProgram triggeredProgram = CompileTriggeredProgram(tokens, triggerIndex, messages, executorModifiers, inheritedProjectileModifiers, runtimeModifiers);
                AddBlockModifiersToCastBlock(triggeredProgram.PrimaryCastBlock, runtimeModifierNodes);
                return triggeredProgram;
            }

            if (multicastIndex >= 0)
            {
                CompiledSpellProgram multicastProgram = CompileMulticast(tokens, multicastIndex, messages, executorModifiers, inheritedProjectileModifiers, runtimeModifiers);
                AddBlockModifiersToCastBlock(multicastProgram.PrimaryCastBlock, runtimeModifierNodes);
                return multicastProgram;
            }

            bool removedRuntimeModifierTokens = filteredTokens != null && originalTokenCount != filteredTokens.Count;
            SpellProjectileCompileResult projectileResult = sourceItems != null && !removedRuntimeModifierTokens
                ? SpellProjectileCompiler.Compile(sourceItems)
                : SpellProjectileCompiler.Compile(tokens);
            ApplyResolvedProjectileModifiers(projectileResult, inheritedProjectileModifiers, messages, "inherited modifier");
            ApplyExecutorModifiers(projectileResult, executorModifiers);
            ApplyCastRuntimeModifiers(projectileResult, runtimeModifiers);
            CompiledSpellProgram program = CompiledSpellProgram.CreateFromProjectileResult(projectileResult);
            program.SetRuntimeModifiers(runtimeModifiers);
            AddBlockModifiersToCastBlock(program.PrimaryCastBlock, runtimeModifierNodes);
            CopyMessages(messages, program);
            return program;
        }

        private static SpellPayloadBlock CompilePayloadBlock(
            IReadOnlyList<BaseTokenData> payloadTokens,
            SpellTriggerType triggerType,
            ResolvedTriggerParameter triggerParameter,
            ICollection<AttackCompileMessage> messages,
            IReadOnlyList<ResolvedModifierTokenData> inheritedPayloadModifiers,
            ExecutorModifierContext executorModifiers,
            SpellCastRuntimeModifiers runtimeModifiers)
        {
            SpellCastBlock innerBlock = new("payload_0", 1);
            if (payloadTokens == null || payloadTokens.Count <= 0)
            {
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored empty trigger payload.", null);
                return null;
            }

            bool hasProjectileCore = false;
            List<BaseTokenData> projectileTokens = new(payloadTokens.Count);
            List<ResolvedModifierTokenData> payloadModifierTokens = new();
            AddInheritedPayloadModifiers(innerBlock, payloadModifierTokens, inheritedPayloadModifiers);
            for (int i = 0; i < payloadTokens.Count; i++)
            {
                BaseTokenData token = payloadTokens[i];
                if (token == null)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored null token inside trigger payload.", null);
                    continue;
                }

                if (token.TokenType == TokenType.Core)
                {
                    hasProjectileCore = true;
                }

                projectileTokens.Add(token);
            }

            if (hasProjectileCore)
            {
                CompiledSpellProgram payloadProgram = CompileProgramFromTokens(
                    projectileTokens,
                    null,
                    executorModifiers,
                    null,
                    runtimeModifiers);
                CopyMessages(payloadProgram.Messages, messages);
                if (payloadProgram.PrimaryCastBlock != null)
                {
                    CopyCompiledBlockContents(payloadProgram.PrimaryCastBlock, innerBlock, messages);
                }
            }
            else
            {
                AddPayloadEffects(payloadTokens, payloadModifierTokens, innerBlock, messages, executorModifiers);
            }

            if (innerBlock.Projectiles.Count <= 0 && innerBlock.PayloadEffects.Count <= 0)
            {
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Trigger payload did not compile any executable projectile or effect node.", null);
                return null;
            }

            return new SpellPayloadBlock(
                "payload_0",
                triggerType,
                triggerParameter.Kind,
                triggerParameter.Value,
                triggerParameter.TriggerPointKind,
                innerBlock);
        }

        private static ExecutorModifierContext CreateExecutorModifierContext(SpellBookData spellBook)
        {
            return spellBook != null && spellBook.HasExecutorModifiers
                ? new ExecutorModifierContext(spellBook)
                : null;
        }

        private static void ApplyExecutorModifiers(
            SpellProjectileCompileResult projectileResult,
            ExecutorModifierContext executorModifiers)
        {
            if (projectileResult == null ||
                executorModifiers == null ||
                !executorModifiers.HasModifiers ||
                !projectileResult.CanFire)
            {
                return;
            }

            IReadOnlyList<TokenModifierDefinition> modifiers = executorModifiers.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                TokenModifierDefinition modifier = modifiers[i].GetSanitized();
                if (!CanApplyExecutorModifierToProjectile(modifier.target))
                {
                    continue;
                }

                if (!TokenModifierExpressionUtility.TryApplyModifier(projectileResult, modifier, out string errorMessage))
                {
                    projectileResult.AddMessage(
                        AttackCompileMessageSeverity.Warning,
                        $"Ignored spell book modifier '{modifier.expression}' on target '{modifier.target}' from '{executorModifiers.SourceLabel}': {errorMessage}");
                }
            }
        }

        private static bool CanApplyExecutorModifierToProjectile(TokenModifierTarget target)
        {
            switch (target)
            {
                case TokenModifierTarget.TextColor:
                case TokenModifierTarget.FontSize:
                case TokenModifierTarget.ScaleMultiplier:
                case TokenModifierTarget.ProjectileSpeed:
                case TokenModifierTarget.MaxLifetime:
                case TokenModifierTarget.MaxTravelDistance:
                case TokenModifierTarget.ImpactRadiusMultiplier:
                case TokenModifierTarget.Damage:
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanApplyExecutorModifierToPayloadEffect(TokenModifierTarget target)
        {
            switch (target)
            {
                case TokenModifierTarget.ImpactRadiusMultiplier:
                case TokenModifierTarget.ResultCount:
                case TokenModifierTarget.ResultDuration:
                case TokenModifierTarget.ResultMultiplier:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddPayloadEffects(
            IReadOnlyList<BaseTokenData> payloadTokens,
            IReadOnlyList<ResolvedModifierTokenData> payloadModifierTokens,
            SpellCastBlock innerBlock,
            ICollection<AttackCompileMessage> messages,
            ExecutorModifierContext executorModifiers)
        {
            List<PendingPayloadModifierEntry> pendingModifiers = new();
            for (int i = 0; i < payloadTokens.Count; i++)
            {
                BaseTokenData token = payloadTokens[i];
                if (token == null)
                {
                    continue;
                }

                if (token is ModifierTokenData modifierToken)
                {
                    int targetCount = 1;
                    ValueTokenData consumedCountValue = null;
                    if (TryConsumeModifierTargetCount(payloadTokens, i, out consumedCountValue))
                    {
                        targetCount = consumedCountValue.ResolveCountValue();
                        i++;
                    }

                    pendingModifiers.Add(new PendingPayloadModifierEntry
                    {
                        modifierToken = modifierToken,
                        countValueToken = consumedCountValue,
                        remainingTargetCount = targetCount,
                        hasResolvedTarget = false,
                    });
                    continue;
                }

                if (token is ResultTokenData resultToken)
                {
                    ResultEffectPayload effects = resultToken.CreateResultEffects();
                    if (i + 1 < payloadTokens.Count &&
                        payloadTokens[i + 1] is ValueTokenData valueToken &&
                        SpellValueParameterUtility.CanConsumeValue(resultToken))
                    {
                        ApplyPayloadResultValue(resultToken, valueToken, ref effects);
                        i++;
                    }

                    ApplyPayloadEffectModifiers(resultToken, payloadModifierTokens, ref effects, messages);
                    ResolvePendingPayloadModifiers(resultToken, pendingModifiers, innerBlock, ref effects, messages);
                    ApplyExecutorPayloadEffectModifiers(resultToken, executorModifiers, ref effects, messages);
                    innerBlock.AddPayloadEffect(new SpellPayloadEffectNode(resultToken, effects));
                    continue;
                }

                if (token.TokenType == TokenType.Value)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored payload value token because no payload result token consumed it.", token);
                    continue;
                }

                if (token.TokenType == TokenType.Trigger)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored nested trigger token; nested payloads are not enabled yet.", token);
                    continue;
                }

                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored token inside result-only payload because payload projectile cores are absent.", token);
            }

            WarnUnusedPayloadModifiers(pendingModifiers, messages);
        }

        private static List<ResolvedModifierTokenData> CollectGlobalProgramModifiers(IReadOnlyList<BaseTokenData> tokens)
        {
            return new List<ResolvedModifierTokenData>();
        }

        private static void ExtractCastRuntimeModifierTokens(
            IReadOnlyList<BaseTokenData> tokens,
            ICollection<AttackCompileMessage> messages,
            SpellCastRuntimeModifiers inheritedRuntimeModifiers,
            out List<BaseTokenData> filteredTokens,
            out SpellCastRuntimeModifiers runtimeModifiers,
            ICollection<SpellModifierNode> modifierNodes = null)
        {
            runtimeModifiers = inheritedRuntimeModifiers.GetSanitized();
            filteredTokens = new List<BaseTokenData>();
            if (tokens == null)
            {
                return;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                BaseTokenData token = tokens[i];
                if (token is ModifierTokenData modifierToken && IsCastRuntimeModifierToken(modifierToken))
                {
                    ApplyCastRuntimeModifierToken(modifierToken, ref runtimeModifiers, messages);
                    modifierNodes?.Add(new SpellModifierNode(
                        modifierToken,
                        SpellModifierScope.GlobalProgram,
                        SpellModifierOrigin.ModifierToken));
                    continue;
                }

                filteredTokens.Add(token);
            }
        }

        private static bool IsCastRuntimeModifierToken(ModifierTokenData modifierToken)
        {
            if (modifierToken == null || modifierToken.Modifiers == null)
            {
                return false;
            }

            for (int i = 0; i < modifierToken.Modifiers.Count; i++)
            {
                if (IsCastRuntimeModifierTarget(modifierToken.Modifiers[i].target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCastRuntimeModifierTarget(TokenModifierTarget target)
        {
            switch (target)
            {
                case TokenModifierTarget.CastCooldownMultiplier:
                case TokenModifierTarget.EnergyCostMultiplier:
                case TokenModifierTarget.CasterHealthCost:
                case TokenModifierTarget.DropChanceMultiplierOnKill:
                case TokenModifierTarget.AngleSpreadMultiplier:
                case TokenModifierTarget.MovementVarianceMultiplier:
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyCastRuntimeModifierToken(
            ModifierTokenData modifierToken,
            ref SpellCastRuntimeModifiers runtimeModifiers,
            ICollection<AttackCompileMessage> messages)
        {
            if (modifierToken == null || modifierToken.Modifiers == null)
            {
                return;
            }

            runtimeModifiers = runtimeModifiers.GetSanitized();
            runtimeModifiers.hasValues = true;
            for (int i = 0; i < modifierToken.Modifiers.Count; i++)
            {
                TokenModifierDefinition modifier = modifierToken.Modifiers[i].GetSanitized();
                switch (modifier.target)
                {
                    case TokenModifierTarget.Damage:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.damageMultiplier, "damage multiplier", messages, out runtimeModifiers.damageMultiplier);
                        break;
                    case TokenModifierTarget.CastCooldownMultiplier:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.castCooldownMultiplier, "cast cooldown multiplier", messages, out runtimeModifiers.castCooldownMultiplier);
                        break;
                    case TokenModifierTarget.EnergyCostMultiplier:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.energyCostMultiplier, "energy cost multiplier", messages, out runtimeModifiers.energyCostMultiplier);
                        break;
                    case TokenModifierTarget.CasterHealthCost:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.casterHealthCost, "caster health cost", messages, out runtimeModifiers.casterHealthCost);
                        break;
                    case TokenModifierTarget.DropChanceMultiplierOnKill:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.dropChanceMultiplierOnKill, "drop chance multiplier", messages, out runtimeModifiers.dropChanceMultiplierOnKill);
                        break;
                    case TokenModifierTarget.AngleSpreadMultiplier:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.angleSpreadMultiplier, "angle spread multiplier", messages, out runtimeModifiers.angleSpreadMultiplier);
                        break;
                    case TokenModifierTarget.MovementVarianceMultiplier:
                        TryApplyRuntimeModifier(modifierToken, modifier, runtimeModifiers.movementVarianceMultiplier, "movement variance multiplier", messages, out runtimeModifiers.movementVarianceMultiplier);
                        break;
                }
            }

            runtimeModifiers = runtimeModifiers.GetSanitized();
        }

        private static bool TryApplyRuntimeModifier(
            BaseTokenData sourceToken,
            TokenModifierDefinition modifier,
            float currentValue,
            string targetLabel,
            ICollection<AttackCompileMessage> messages,
            out float result)
        {
            if (TokenModifierExpressionUtility.TryApplyNumericExpression(
                    currentValue,
                    modifier.expression,
                    out result,
                    out string errorMessage))
            {
                return true;
            }

            AddMessage(
                messages,
                AttackCompileMessageSeverity.Warning,
                $"Ignored cast runtime modifier '{modifier.expression}' on {targetLabel}: {errorMessage}",
                sourceToken);
            return false;
        }

        private static void ApplyCastRuntimeModifiers(
            SpellProjectileCompileResult projectileResult,
            SpellCastRuntimeModifiers runtimeModifiers)
        {
            if (projectileResult == null)
            {
                return;
            }

            SpellCastRuntimeModifiers sanitizedModifiers = runtimeModifiers.GetSanitized();
            AttackSpec spec = projectileResult.AttackSpec;
            spec.damage *= sanitizedModifiers.damageMultiplier;
            if (spec.behaviorType == AttackBehaviorType.Snake ||
                spec.behaviorType == AttackBehaviorType.Wander)
            {
                spec.behaviorParameter *= sanitizedModifiers.movementVarianceMultiplier;
            }

            projectileResult.AttackSpec = spec.GetSanitized();
            projectileResult.SpreadAngleStep = MathfMax(0f, projectileResult.SpreadAngleStep * sanitizedModifiers.angleSpreadMultiplier);
            projectileResult.RuntimeModifiers = sanitizedModifiers;
        }

        private static void AddInheritedPayloadModifiers(
            SpellCastBlock innerBlock,
            ICollection<ResolvedModifierTokenData> payloadModifierTokens,
            IReadOnlyList<ResolvedModifierTokenData> inheritedPayloadModifiers)
        {
            if (inheritedPayloadModifiers == null)
            {
                return;
            }

            for (int i = 0; i < inheritedPayloadModifiers.Count; i++)
            {
                AddPayloadModifier(innerBlock, payloadModifierTokens, inheritedPayloadModifiers[i]);
            }
        }

        private static void AddPayloadModifier(
            SpellCastBlock innerBlock,
            ICollection<ResolvedModifierTokenData> payloadModifierTokens,
            ResolvedModifierTokenData modifierToken)
        {
            if (innerBlock == null || payloadModifierTokens == null || modifierToken.SourceToken == null)
            {
                return;
            }

            payloadModifierTokens.Add(modifierToken);
            innerBlock.AddModifier(new SpellModifierNode(
                modifierToken.SourceToken,
                modifierToken.Scope,
                SpellModifierOrigin.ModifierToken,
                modifierToken.TargetCount));
        }

        private static void AddModifierNodeIfMissing(
            SpellCastBlock block,
            SpellModifierNode modifier)
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
                    existing.Origin == modifier.Origin &&
                    existing.TargetCount == modifier.TargetCount)
                {
                    return;
                }
            }

            block.AddModifier(modifier);
        }

        private static void AddInheritedBlockModifiers(
            IReadOnlyList<ResolvedModifierTokenData> inheritedModifiers,
            MulticastCompileState state)
        {
            if (inheritedModifiers == null || state == null)
            {
                return;
            }

            for (int i = 0; i < inheritedModifiers.Count; i++)
            {
                ResolvedModifierTokenData modifier = inheritedModifiers[i];
                if (modifier.SourceToken == null)
                {
                    continue;
                }

                state.blockModifierTokens.Add(modifier);
                AddBlockModifierNode(
                    state,
                    modifier.SourceToken,
                    modifier.Scope,
                    SpellModifierOrigin.ModifierToken,
                    modifier.TargetCount);
            }
        }

        private static void AddBlockModifiersToCastBlock(
            SpellCastBlock block,
            IReadOnlyList<SpellModifierNode> modifiers)
        {
            if (block == null || modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                AddModifierNodeIfMissing(block, modifiers[i]);
            }
        }

        private static void CopyOuterBlockContents(SpellCastBlock sourceBlock, SpellCastBlock targetBlock)
        {
            if (sourceBlock == null || targetBlock == null)
            {
                return;
            }

            AddBlockModifiersToCastBlock(targetBlock, sourceBlock.Modifiers);
            for (int i = 0; i < sourceBlock.Projectiles.Count; i++)
            {
                targetBlock.AddProjectile(sourceBlock.Projectiles[i]);
            }

            for (int i = 0; i < sourceBlock.Payloads.Count; i++)
            {
                targetBlock.AddPayload(sourceBlock.Payloads[i]);
            }

            for (int i = 0; i < sourceBlock.PayloadEffects.Count; i++)
            {
                targetBlock.AddPayloadEffect(sourceBlock.PayloadEffects[i]);
            }
        }

        private static void ApplyResolvedProjectileModifiers(
            SpellProjectileCompileResult projectileResult,
            IReadOnlyList<ResolvedModifierTokenData> modifierTokens,
            ICollection<AttackCompileMessage> messages,
            string sourceLabel)
        {
            if (projectileResult == null || modifierTokens == null)
            {
                return;
            }

            for (int i = 0; i < modifierTokens.Count; i++)
            {
                ResolvedModifierTokenData modifierToken = modifierTokens[i];
                if (modifierToken.SourceToken == null)
                {
                    continue;
                }

                for (int j = 0; j < modifierToken.SourceToken.Modifiers.Count; j++)
                {
                    TokenModifierDefinition modifier = modifierToken.SourceToken.Modifiers[j];
                    if (!TokenModifierExpressionUtility.TryApplyModifier(projectileResult, modifier, out string errorMessage))
                    {
                        AddMessage(
                            messages,
                            AttackCompileMessageSeverity.Warning,
                            $"Ignored {sourceLabel} '{modifier.expression}' on target '{modifier.target}': {errorMessage}",
                            modifierToken.SourceToken);
                    }
                }
            }
        }

        private static void ApplyPayloadEffectModifiers(
            ResultTokenData resultToken,
            IReadOnlyList<ResolvedModifierTokenData> payloadModifierTokens,
            ref ResultEffectPayload effects,
            ICollection<AttackCompileMessage> messages)
        {
            if (resultToken == null || payloadModifierTokens == null)
            {
                return;
            }

            for (int i = 0; i < payloadModifierTokens.Count; i++)
            {
                ResolvedModifierTokenData modifierToken = payloadModifierTokens[i];
                if (modifierToken.SourceToken == null)
                {
                    continue;
                }

                for (int j = 0; j < modifierToken.SourceToken.Modifiers.Count; j++)
                {
                    ApplyPayloadEffectModifier(
                        resultToken,
                        modifierToken.SourceToken,
                        modifierToken.SourceToken.Modifiers[j].GetSanitized(),
                        ref effects,
                        messages,
                        "payload modifier");
                }
            }

            effects = effects.GetSanitized();
        }

        private static void ApplyExecutorPayloadEffectModifiers(
            ResultTokenData resultToken,
            ExecutorModifierContext executorModifiers,
            ref ResultEffectPayload effects,
            ICollection<AttackCompileMessage> messages)
        {
            if (resultToken == null ||
                executorModifiers == null ||
                !executorModifiers.HasModifiers)
            {
                return;
            }

            IReadOnlyList<TokenModifierDefinition> modifiers = executorModifiers.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                TokenModifierDefinition modifier = modifiers[i].GetSanitized();
                if (!CanApplyExecutorModifierToPayloadEffect(modifier.target))
                {
                    continue;
                }

                ApplyPayloadEffectModifier(
                    resultToken,
                    null,
                    modifier,
                    ref effects,
                    messages,
                    $"spell book modifier from '{executorModifiers.SourceLabel}'");
            }

            effects = effects.GetSanitized();
        }

        private static void ApplyPayloadEffectModifier(
            ResultTokenData resultToken,
            BaseTokenData sourceToken,
            TokenModifierDefinition modifier,
            ref ResultEffectPayload effects,
            ICollection<AttackCompileMessage> messages,
            string sourceLabel)
        {
            if (modifier.target == TokenModifierTarget.ImpactRadiusMultiplier)
            {
                if (resultToken.ResultType == AttackResultType.Explosion &&
                    TryApplyPayloadEffectModifier(
                        sourceToken,
                        modifier,
                        effects.explosionRadius,
                        "explosion radius",
                        sourceLabel,
                        messages,
                        out float explosionRadius))
                {
                    effects.explosionRadius = explosionRadius > 0f ? explosionRadius : 0f;
                }
                else if (resultToken.ResultType == AttackResultType.Healing &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.effectRadius,
                             "healing radius",
                             sourceLabel,
                             messages,
                             out float effectRadius))
                {
                    effects.effectRadius = effectRadius > 0f ? effectRadius : 0f;
                }
                else if (resultToken.ResultType == AttackResultType.StatusEffect &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.effectRadius,
                             "control radius",
                             sourceLabel,
                             messages,
                             out float controlRadius))
                {
                    effects.effectRadius = controlRadius > 0f ? controlRadius : 0f;
                }
                else if ((resultToken.ResultType == AttackResultType.Leave ||
                          resultToken.ResultType == AttackResultType.Push ||
                          resultToken.ResultType == AttackResultType.Pull) &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.effectRadius,
                             "result radius",
                             sourceLabel,
                             messages,
                             out float resultRadius))
                {
                    effects.effectRadius = resultRadius > 0f ? resultRadius : 0f;
                }

                return;
            }

            if (modifier.target == TokenModifierTarget.ResultCount)
            {
                if (resultToken.ResultType == AttackResultType.Split &&
                    TryApplyPayloadEffectModifier(
                        sourceToken,
                        modifier,
                        effects.splitProjectileCount,
                        "split count",
                        sourceLabel,
                        messages,
                        out float splitCount))
                {
                    effects.splitProjectileCount = MathfMax(0, MathfRoundToInt(splitCount));
                }
                else if (resultToken.ResultType == AttackResultType.StatusEffect &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.controlTriggerCount,
                             "control trigger count",
                             sourceLabel,
                             messages,
                             out float controlTriggerCount))
                {
                    effects.controlTriggerCount = MathfMax(0, MathfRoundToInt(controlTriggerCount));
                }

                return;
            }

            if (modifier.target == TokenModifierTarget.ResultDuration)
            {
                if (resultToken.ResultType == AttackResultType.Explosion &&
                    TryApplyPayloadEffectModifier(
                        sourceToken,
                        modifier,
                        effects.explosionDelaySeconds,
                        "explosion delay",
                        sourceLabel,
                        messages,
                        out float explosionDelay))
                {
                    effects.explosionDelaySeconds = MathfMax(0f, explosionDelay);
                }
                else if (resultToken.ResultType == AttackResultType.StatusEffect &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.controlDuration,
                             "control duration",
                             sourceLabel,
                             messages,
                             out float controlDuration))
                {
                    effects.controlDuration = MathfMax(0f, controlDuration);
                }
                else if (resultToken.ResultType == AttackResultType.Leave &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.effectDuration,
                             "linger duration",
                             sourceLabel,
                             messages,
                             out float lingerDuration))
                {
                    effects.effectDuration = MathfMax(0f, lingerDuration);
                }

                return;
            }

            if (modifier.target == TokenModifierTarget.ResultMultiplier)
            {
                if (resultToken.ResultType == AttackResultType.Explosion &&
                    TryApplyPayloadEffectModifier(
                        sourceToken,
                        modifier,
                        effects.explosionDamageMultiplier,
                        "explosion damage multiplier",
                        sourceLabel,
                        messages,
                        out float explosionDamageMultiplier))
                {
                    effects.explosionDamageMultiplier = MathfClamp01(explosionDamageMultiplier);
                }
                else if (resultToken.ResultType == AttackResultType.Split &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.splitDamageMultiplier,
                             "split damage multiplier",
                             sourceLabel,
                             messages,
                             out float splitDamageMultiplier))
                {
                    effects.splitDamageMultiplier = MathfClamp01(splitDamageMultiplier);
                }
                else if (resultToken.ResultType == AttackResultType.Healing &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.healingMultiplier,
                             "healing multiplier",
                             sourceLabel,
                             messages,
                             out float healingMultiplier))
                {
                    effects.healingMultiplier = MathfMax(0f, healingMultiplier);
                }
                else if ((resultToken.ResultType == AttackResultType.Drain ||
                          resultToken.ResultType == AttackResultType.Shield ||
                          resultToken.ResultType == AttackResultType.Push ||
                          resultToken.ResultType == AttackResultType.Pull) &&
                         TryApplyPayloadEffectModifier(
                             sourceToken,
                             modifier,
                             effects.effectStrength,
                             "result strength",
                             sourceLabel,
                             messages,
                             out float resultStrength))
                {
                    effects.effectStrength = MathfMax(0f, resultStrength);
                }
            }
        }

        private static bool TryApplyPayloadEffectModifier(
            BaseTokenData sourceToken,
            TokenModifierDefinition modifier,
            float currentValue,
            string targetLabel,
            string sourceLabel,
            ICollection<AttackCompileMessage> messages,
            out float result)
        {
            if (TokenModifierExpressionUtility.TryApplyNumericExpression(
                    currentValue,
                    modifier.expression,
                    out result,
                    out string errorMessage))
            {
                return true;
            }

            AddMessage(
                messages,
                AttackCompileMessageSeverity.Warning,
                $"Ignored {sourceLabel} '{modifier.expression}' on {targetLabel}: {errorMessage}",
                sourceToken);
            return false;
        }

        private static List<BaseTokenData> ExpandItems(IReadOnlyList<PlaceableTokenData> items, ICollection<AttackCompileMessage> messages)
        {
            List<BaseTokenData> tokens = new();
            if (items == null)
            {
                return tokens;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PlaceableTokenData item = items[i];
                if (item == null)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored null item in spell program.", null);
                    continue;
                }

                int beforeCount = tokens.Count;
                item.AppendCompileTokens(tokens);
                if (tokens.Count == beforeCount)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, $"Ignored item '{item.name}' because it does not contribute any compile tokens.", null);
                }
            }

            return tokens;
        }

        private static int FindFirstMulticastIndex(IReadOnlyList<BaseTokenData> tokens)
        {
            if (tokens == null)
            {
                return -1;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] is MulticastTokenData || tokens[i]?.TokenType == TokenType.Multicast)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFirstTriggerIndex(IReadOnlyList<BaseTokenData> tokens)
        {
            if (tokens == null)
            {
                return -1;
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] is TriggerTokenData || tokens[i]?.TokenType == TokenType.Trigger)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void CollectBlockPrefix(IReadOnlyList<BaseTokenData> tokens, int multicastIndex, MulticastCompileState state)
        {
            for (int i = 0; i < multicastIndex; i++)
            {
                BaseTokenData token = tokens[i];
                if (token == null)
                {
                    AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored null token before multicast.", null);
                    continue;
                }

                if (TryAcceptBlockModifier(token, state))
                {
                    continue;
                }

                AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored token before multicast because only block modifiers can prefix a multicast CastBlock.", token);
            }
        }

        private static bool TryReadProjectileSegment(
            IReadOnlyList<BaseTokenData> tokens,
            ref int index,
            MulticastCompileState state,
            out List<BaseTokenData> segmentTokens)
        {
            segmentTokens = new List<BaseTokenData>();
            List<BaseTokenData> pendingPrefixTokens = new();
            bool hasStarted = false;
            bool hasEnteredPayload = false;

            while (tokens != null && index < tokens.Count)
            {
                BaseTokenData token = tokens[index];
                if (token == null)
                {
                    AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored null token inside multicast.", null);
                    index++;
                    continue;
                }

                if (token is MulticastTokenData || token.TokenType == TokenType.Multicast)
                {
                    AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored nested multicast token; wrapping is not enabled.", token);
                    index++;
                    continue;
                }

                if (!hasStarted && token is ModifierTokenData)
                {
                    pendingPrefixTokens.Add(token);
                    index++;
                    continue;
                }

                if (!hasStarted &&
                    token is ValueTokenData &&
                    pendingPrefixTokens.Count > 0 &&
                    pendingPrefixTokens[pendingPrefixTokens.Count - 1] is ModifierTokenData)
                {
                    pendingPrefixTokens.Add(token);
                    index++;
                    continue;
                }

                if (token.TokenType == TokenType.Trigger)
                {
                    segmentTokens.AddRange(pendingPrefixTokens);
                    pendingPrefixTokens.Clear();
                    segmentTokens.Add(token);
                    hasStarted = true;
                    hasEnteredPayload = true;
                    index++;
                    continue;
                }

                if (token.TokenType == TokenType.Core)
                {
                    if (hasStarted && !hasEnteredPayload)
                    {
                        return segmentTokens.Count > 0;
                    }

                    segmentTokens.AddRange(pendingPrefixTokens);
                    pendingPrefixTokens.Clear();
                    segmentTokens.Add(token);
                    hasStarted = true;
                    index++;
                    continue;
                }

                if (!hasStarted)
                {
                    segmentTokens.AddRange(pendingPrefixTokens);
                    pendingPrefixTokens.Clear();
                    segmentTokens.Add(token);
                    index++;
                    return true;
                }

                segmentTokens.Add(token);
                index++;
            }

            if (!hasStarted && pendingPrefixTokens.Count > 0)
            {
                AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored multicast segment modifier because it did not find a projectile core.", pendingPrefixTokens[0]);
            }

            return segmentTokens.Count > 0;
        }

        private static bool TryAcceptBlockModifier(BaseTokenData token, MulticastCompileState state)
        {
            if (token is not ModifierTokenData modifierToken)
            {
                return false;
            }

            state.blockModifierTokens.Add(new ResolvedModifierTokenData(
                modifierToken,
                SpellModifierScope.CurrentBlock,
                1));
            AddBlockModifierNode(
                state,
                modifierToken,
                SpellModifierScope.CurrentBlock,
                SpellModifierOrigin.ModifierToken,
                1);
            return true;
        }

        private static void WarnIgnoredTrailingTokens(IReadOnlyList<BaseTokenData> tokens, int startIndex, MulticastCompileState state)
        {
            if (tokens == null)
            {
                return;
            }

            for (int i = startIndex; i < tokens.Count; i++)
            {
                BaseTokenData token = tokens[i];
                if (token != null)
                {
                    AddMessage(state.messages, AttackCompileMessageSeverity.Warning, "Ignored token after multicast had collected its requested projectile nodes.", token);
                }
            }
        }

        private static void AddBlockModifierNode(
            MulticastCompileState state,
            BaseTokenData sourceToken,
            SpellModifierScope scope,
            SpellModifierOrigin origin,
            int targetCount)
        {
            for (int i = 0; i < state.blockModifiers.Count; i++)
            {
                SpellModifierNode existing = state.blockModifiers[i];
                if (existing != null &&
                    existing.SourceToken == sourceToken &&
                    existing.Scope == scope &&
                    existing.Origin == origin)
                {
                    return;
                }
            }

            state.blockModifiers.Add(new SpellModifierNode(sourceToken, scope, origin, targetCount));
        }

        private static List<BaseTokenData> CopyRange(IReadOnlyList<BaseTokenData> tokens, int startIndex, int endExclusive)
        {
            List<BaseTokenData> range = new();
            if (tokens == null)
            {
                return range;
            }

            int start = MathfMax(0, startIndex);
            int end = MathfMin(tokens.Count, MathfMax(start, endExclusive));
            for (int i = start; i < end; i++)
            {
                range.Add(tokens[i]);
            }

            return range;
        }

        private static void ApplyPayloadResultValue(ResultTokenData resultToken, ValueTokenData valueToken, ref ResultEffectPayload resultEffects)
        {
            SpellValueParameterUtility.ApplyResultValue(resultToken, valueToken, ref resultEffects);
        }

        private static bool TryConsumeModifierTargetCount(
            IReadOnlyList<BaseTokenData> tokens,
            int modifierIndex,
            out ValueTokenData valueToken)
        {
            valueToken = null;
            if (tokens == null)
            {
                return false;
            }

            int valueIndex = modifierIndex + 1;
            if (valueIndex >= tokens.Count || tokens[valueIndex] is not ValueTokenData candidateValue)
            {
                return false;
            }

            int targetIndex = valueIndex + 1;
            if (targetIndex >= tokens.Count)
            {
                return false;
            }

            BaseTokenData targetToken = tokens[targetIndex];
            if (targetToken == null ||
                (targetToken.TokenType != TokenType.Core &&
                 targetToken.TokenType != TokenType.Behavior &&
                 targetToken.TokenType != TokenType.Result))
            {
                return false;
            }

            if (candidateValue.ResolveCountValue() < 1)
            {
                return false;
            }

            valueToken = candidateValue;
            return true;
        }

        private static void ResolvePendingPayloadModifiers(
            ResultTokenData resultToken,
            IList<PendingPayloadModifierEntry> pendingModifiers,
            SpellCastBlock innerBlock,
            ref ResultEffectPayload effects,
            ICollection<AttackCompileMessage> messages)
        {
            if (resultToken == null || pendingModifiers == null || pendingModifiers.Count <= 0)
            {
                return;
            }

            for (int i = pendingModifiers.Count - 1; i >= 0; i--)
            {
                PendingPayloadModifierEntry pendingModifier = pendingModifiers[i];
                if (pendingModifier?.modifierToken == null)
                {
                    continue;
                }

                if (!pendingModifier.hasResolvedTarget)
                {
                    SpellModifierScope resolvedScope = pendingModifier.remainingTargetCount > 1
                        ? SpellModifierScope.NextN
                        : SpellModifierScope.NextToken;
                    AddModifierNodeIfMissing(
                        innerBlock,
                        new SpellModifierNode(
                            pendingModifier.modifierToken,
                            resolvedScope,
                            SpellModifierOrigin.ModifierToken,
                            pendingModifier.remainingTargetCount));
                    pendingModifier.hasResolvedTarget = true;
                }

                for (int j = 0; j < pendingModifier.modifierToken.Modifiers.Count; j++)
                {
                    ApplyPayloadEffectModifier(
                        resultToken,
                        pendingModifier.modifierToken,
                        pendingModifier.modifierToken.Modifiers[j].GetSanitized(),
                        ref effects,
                        messages,
                        "payload modifier");
                }

                pendingModifier.remainingTargetCount--;
                if (pendingModifier.remainingTargetCount <= 0)
                {
                    pendingModifiers.RemoveAt(i);
                    continue;
                }

                pendingModifiers[i] = pendingModifier;
            }

            effects = effects.GetSanitized();
        }

        private static void WarnUnusedPayloadModifiers(
            IEnumerable<PendingPayloadModifierEntry> pendingModifiers,
            ICollection<AttackCompileMessage> messages)
        {
            if (pendingModifiers == null)
            {
                return;
            }

            foreach (PendingPayloadModifierEntry pendingModifier in pendingModifiers)
            {
                if (pendingModifier?.modifierToken == null)
                {
                    continue;
                }

                if (!pendingModifier.hasResolvedTarget)
                {
                    AddMessage(
                        messages,
                        AttackCompileMessageSeverity.Warning,
                        "Ignored payload modifier token because it did not find a valid payload result token.",
                        pendingModifier.modifierToken);
                }
                else if (pendingModifier.remainingTargetCount > 0)
                {
                    AddMessage(
                        messages,
                        AttackCompileMessageSeverity.Warning,
                        $"Payload modifier target count ended with {pendingModifier.remainingTargetCount} unresolved payload result token(s).",
                        pendingModifier.modifierToken);
                }
            }
        }

        private static void CopyCompiledBlockContents(
            SpellCastBlock sourceBlock,
            SpellCastBlock targetBlock,
            ICollection<AttackCompileMessage> messages)
        {
            if (sourceBlock == null || targetBlock == null)
            {
                return;
            }

            for (int i = 0; i < sourceBlock.Modifiers.Count; i++)
            {
                targetBlock.AddModifier(sourceBlock.Modifiers[i]);
            }

            for (int i = 0; i < sourceBlock.Projectiles.Count; i++)
            {
                SpellProjectileNode projectile = sourceBlock.Projectiles[i];
                if (projectile != null && projectile.Payloads.Count > 0)
                {
                    AddMessage(
                        messages,
                        AttackCompileMessageSeverity.Warning,
                        "Ignored nested payload attachments inside payload projectile; nested payloads are not enabled yet.",
                        null);
                }

                targetBlock.AddProjectile(projectile);
            }

            for (int i = 0; i < sourceBlock.PayloadEffects.Count; i++)
            {
                targetBlock.AddPayloadEffect(sourceBlock.PayloadEffects[i]);
            }

            if (sourceBlock.Payloads.Count > 0)
            {
                AddMessage(
                    messages,
                    AttackCompileMessageSeverity.Warning,
                    "Ignored nested payload blocks inside payload segment; nested payloads are not enabled yet.",
                    null);
            }
        }

        private static int MathfMax(int a, int b)
        {
            return a > b ? a : b;
        }

        private static float MathfMax(float a, float b)
        {
            return a > b ? a : b;
        }

        private static int MathfMin(int a, int b)
        {
            return a < b ? a : b;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static int MathfRoundToInt(float value)
        {
            return value >= 0f
                ? (int)System.Math.Floor(value + 0.5f)
                : (int)System.Math.Ceiling(value - 0.5f);
        }

        private static float MathfClamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }

        private static void AddMessage(
            ICollection<AttackCompileMessage> messages,
            AttackCompileMessageSeverity severity,
            string message,
            BaseTokenData token)
        {
            messages?.Add(new AttackCompileMessage(severity, message, token != null ? token.TokenId : string.Empty));
        }

        private static void CopyMessages(IReadOnlyList<AttackCompileMessage> source, ICollection<AttackCompileMessage> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private static void CopyMessages(IReadOnlyList<AttackCompileMessage> source, CompiledSpellProgram target)
        {
            if (source == null || target == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.AddMessage(source[i]);
            }
        }
    }
}
