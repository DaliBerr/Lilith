using System.Collections.Generic;

namespace Kernel.Bullet
{
    /// <summary>
    /// 把法术书执行序列编译成 SpellProgram；单个 projectile 由 SpellProjectileCompiler 生成内部 compile result。
    /// </summary>
    public static class SpellProgramCompiler
    {
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
            int triggerIndex = FindFirstTriggerIndex(tokens);
            if (triggerIndex >= 0)
            {
                return CompileTriggeredProgram(tokens, triggerIndex, messages, executorModifiers);
            }

            int multicastIndex = FindFirstMulticastIndex(tokens);
            if (multicastIndex >= 0)
            {
                return CompileMulticast(tokens, multicastIndex, messages, executorModifiers);
            }

            SpellProjectileCompileResult projectileResult = SpellProjectileCompiler.Compile(items);
            ApplyExecutorModifiers(projectileResult, executorModifiers);
            return CompiledSpellProgram.CreateFromProjectileResult(projectileResult);
        }

        private static CompiledSpellProgram CompileTriggeredProgram(
            IReadOnlyList<BaseTokenData> tokens,
            int triggerIndex,
            IReadOnlyList<AttackCompileMessage> initialMessages,
            ExecutorModifierContext executorModifiers)
        {
            List<AttackCompileMessage> messages = new();
            CopyMessages(initialMessages, messages);

            TriggerTokenData triggerToken = tokens[triggerIndex] as TriggerTokenData;
            SpellTriggerType triggerType = triggerToken != null ? triggerToken.TriggerType : SpellTriggerType.OnHit;
            if (triggerType == SpellTriggerType.None)
            {
                triggerType = SpellTriggerType.OnHit;
            }

            List<BaseTokenData> outerTokens = CopyRange(tokens, 0, triggerIndex);
            CompiledSpellProgram program = CompileProgramFromTokens(outerTokens, executorModifiers);
            int payloadStartIndex = FindPayloadStartIndex(tokens, triggerIndex + 1);
            if (payloadStartIndex < 0)
            {
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored trigger token because it is not followed by a payload start token.", triggerToken);
                CopyMessages(messages, program);
                return program;
            }

            WarnIgnoredTokens(tokens, triggerIndex + 1, payloadStartIndex, messages, "Ignored token between trigger and payload start.");

            int payloadEndIndex = FindPayloadEndIndex(tokens, payloadStartIndex + 1);
            if (payloadEndIndex < 0)
            {
                payloadEndIndex = tokens.Count;
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Payload start token did not find a matching payload end token; consuming tokens to the end of the formula.", tokens[payloadStartIndex]);
            }

            List<BaseTokenData> payloadTokens = CopyRange(tokens, payloadStartIndex + 1, payloadEndIndex);
            List<ResolvedModifierTokenData> inheritedPayloadModifiers = CollectGlobalProgramModifiers(outerTokens);
            SpellPayloadBlock payload = CompilePayloadBlock(payloadTokens, triggerType, messages, inheritedPayloadModifiers, executorModifiers);
            if (payload != null && program.CanCast)
            {
                program.AttachPayloadToPrimaryBlockProjectiles(payload);
            }
            else if (payload != null)
            {
                AddMessage(messages, AttackCompileMessageSeverity.Warning, "Compiled payload was not attached because the outer trigger formula cannot cast.", triggerToken);
            }

            if (payloadEndIndex < tokens.Count)
            {
                WarnIgnoredTokens(tokens, payloadEndIndex + 1, tokens.Count, messages, "Ignored token after payload end.");
            }

            CopyMessages(messages, program);
            return program;
        }

        private static CompiledSpellProgram CompileMulticast(
            IReadOnlyList<BaseTokenData> tokens,
            int multicastIndex,
            IReadOnlyList<AttackCompileMessage> initialMessages,
            ExecutorModifierContext executorModifiers)
        {
            MulticastCompileState state = new();
            CopyMessages(initialMessages, state.messages);

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

            List<SpellProjectileCompileResult> projectileResults = new(segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                SpellProjectileCompileResult projectileResult = SpellProjectileCompiler.Compile(segments[i]);
                ApplyResolvedProjectileModifiers(projectileResult, state.blockModifierTokens, state.messages, "multicast block modifier");
                ApplyExecutorModifiers(projectileResult, executorModifiers);
                projectileResults.Add(projectileResult);
            }

            return CompiledSpellProgram.CreateFromProjectileResults(projectileResults, state.blockModifiers, state.messages);
        }

        private static CompiledSpellProgram CompileProgramFromTokens(IReadOnlyList<BaseTokenData> tokens, ExecutorModifierContext executorModifiers)
        {
            int multicastIndex = FindFirstMulticastIndex(tokens);
            if (multicastIndex >= 0)
            {
                return CompileMulticast(tokens, multicastIndex, null, executorModifiers);
            }

            SpellProjectileCompileResult projectileResult = SpellProjectileCompiler.Compile(tokens);
            ApplyExecutorModifiers(projectileResult, executorModifiers);
            return CompiledSpellProgram.CreateFromProjectileResult(projectileResult);
        }

        private static SpellPayloadBlock CompilePayloadBlock(
            IReadOnlyList<BaseTokenData> payloadTokens,
            SpellTriggerType triggerType,
            ICollection<AttackCompileMessage> messages,
            IReadOnlyList<ResolvedModifierTokenData> inheritedPayloadModifiers,
            ExecutorModifierContext executorModifiers)
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
                CompiledSpellProgram payloadProgram = CompileProgramFromTokens(projectileTokens, executorModifiers);
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

            return new SpellPayloadBlock("payload_0", triggerType, innerBlock);
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
                        targetCount = MathfMax(1, MathfRoundToInt(consumedCountValue.NumericValue));
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

                if (token.TokenType == TokenType.Trigger ||
                    token.TokenType == TokenType.PayloadStart ||
                    token.TokenType == TokenType.PayloadEnd)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, "Ignored nested trigger or payload boundary token; nested payloads are not enabled yet.", token);
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

        private static int FindPayloadStartIndex(IReadOnlyList<BaseTokenData> tokens, int startIndex)
        {
            if (tokens == null)
            {
                return -1;
            }

            for (int i = MathfMax(0, startIndex); i < tokens.Count; i++)
            {
                if (tokens[i] is PayloadBoundaryTokenData boundaryToken &&
                    boundaryToken.BoundaryKind == PayloadBoundaryKind.Start)
                {
                    return i;
                }

                if (tokens[i]?.TokenType == TokenType.PayloadStart)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindPayloadEndIndex(IReadOnlyList<BaseTokenData> tokens, int startIndex)
        {
            if (tokens == null)
            {
                return -1;
            }

            for (int i = MathfMax(0, startIndex); i < tokens.Count; i++)
            {
                if (tokens[i] is PayloadBoundaryTokenData boundaryToken &&
                    boundaryToken.BoundaryKind == PayloadBoundaryKind.End)
                {
                    return i;
                }

                if (tokens[i]?.TokenType == TokenType.PayloadEnd)
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

                if (token.TokenType == TokenType.Core)
                {
                    if (hasStarted)
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

        private static void WarnIgnoredTokens(
            IReadOnlyList<BaseTokenData> tokens,
            int startIndex,
            int endExclusive,
            ICollection<AttackCompileMessage> messages,
            string warning)
        {
            if (tokens == null)
            {
                return;
            }

            int start = MathfMax(0, startIndex);
            int end = MathfMin(tokens.Count, MathfMax(start, endExclusive));
            for (int i = start; i < end; i++)
            {
                if (tokens[i] != null)
                {
                    AddMessage(messages, AttackCompileMessageSeverity.Warning, warning, tokens[i]);
                }
            }
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

            if (candidateValue.NumericValue < 1f)
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
                if (pendingModifier?.modifierToken == null || pendingModifier.hasResolvedTarget)
                {
                    continue;
                }

                AddMessage(
                    messages,
                    AttackCompileMessageSeverity.Warning,
                    "Ignored payload modifier token because it did not find a valid payload result token.",
                    pendingModifier.modifierToken);
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
