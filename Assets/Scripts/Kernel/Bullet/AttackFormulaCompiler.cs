using System.Collections.Generic;
using System.Text;
using Kernel.Upgrade;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 把一串有序词元编译成最终可执行的攻击结果。
    /// </summary>
    public static class AttackFormulaCompiler
    {
        private enum PendingValueTarget
        {
            None = 0,
            Behavior = 1,
            Result = 2,
        }

        private sealed class CompileItemInstance
        {
            public PlaceableTokenData item;
            public int totalTokenCount;
            public int acceptedTokenCount;
        }

        private readonly struct CompileTokenEntry
        {
            public CompileTokenEntry(BaseTokenData token, CompileItemInstance itemInstance)
            {
                Token = token;
                ItemInstance = itemInstance;
            }

            public BaseTokenData Token { get; }
            public CompileItemInstance ItemInstance { get; }
        }

        /// <summary>
        /// summary: 从左到右编译一组单格基础词元，兼容旧调用方。
        /// param: tokens 当前装备槽中的有序词元列表
        /// returns: 编译后的攻击结果；缺少核心词元时会返回不可发射状态
        /// </summary>
        public static CompiledAttack Compile(IReadOnlyList<BaseTokenData> tokens)
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

        /// <summary>
        /// summary: 从左到右编译一组可放置 token 物件，尽力生成一个可执行的攻击结果。
        /// param: items 当前装备槽中的有序可放置 token 物件列表
        /// returns: 编译后的攻击结果；缺少核心词元时会返回不可发射状态
        /// </summary>
        public static CompiledAttack Compile(IReadOnlyList<PlaceableTokenData> items)
        {
            CompiledAttack compiledAttack = new();
            List<string> acceptedDisplays = new();
            List<BaseTokenData> acceptedTokens = new();

            List<CompileItemInstance> itemInstances = new();
            List<CompileTokenEntry> tokens = ExpandItems(items, compiledAttack, itemInstances);

            CoreTokenData coreToken = null;
            BehaviorTokenData behaviorToken = null;
            ResultTokenData resultToken = null;
            PendingValueTarget pendingValueTarget = PendingValueTarget.None;
            bool hasExplicitResult = false;

            int spreadProjectileCount = 1;
            int bounceCount = 0;
            int pierceCount = 0;
            float spreadAngleStep = 0f;
            CoreEffectPayload coreEffects = default;
            ResultEffectPayload resultEffects = default;

            for (int i = 0; i < tokens.Count; i++)
            {
                CompileTokenEntry entry = tokens[i];
                BaseTokenData token = entry.Token;
                if (token == null)
                {
                    compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored null token in formula.");
                    continue;
                }

                switch (token.TokenType)
                {
                    case TokenType.Pre:
                        if (coreToken != null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored pre token that appears after the core token.", token);
                            break;
                        }

                        compiledAttack.AddPreToken(token);
                        AcceptToken(entry, acceptedDisplays, acceptedTokens);
                        break;

                    case TokenType.Core:
                        if (token is not CoreTokenData candidateCore)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored core token with an unexpected asset type.", token);
                            break;
                        }

                        if (coreToken != null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored duplicate core token; the first core token already established the attack base.", token);
                            break;
                        }

                        coreToken = candidateCore;
                        coreEffects = candidateCore.CreateCoreEffects();
                        AcceptToken(entry, acceptedDisplays, acceptedTokens);
                        break;

                    case TokenType.Behavior:
                        if (token is not BehaviorTokenData candidateBehavior)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored behavior token with an unexpected asset type.", token);
                            break;
                        }

                        if (coreToken == null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored behavior token that appears before the core token.", token);
                            break;
                        }

                        if (hasExplicitResult)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored behavior token that appears after the result token.", token);
                            break;
                        }

                        if (behaviorToken != null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored duplicate behavior token; the first behavior token already decided the projectile pattern.", token);
                            break;
                        }

                        behaviorToken = candidateBehavior;
                        pendingValueTarget = ShouldBehaviorConsumeNumericValue(candidateBehavior)
                            ? PendingValueTarget.Behavior
                            : PendingValueTarget.None;
                        spreadProjectileCount = Mathf.Max(1, candidateBehavior.DefaultProjectileCount);
                        bounceCount = Mathf.Max(0, candidateBehavior.DefaultProjectileCount);
                        pierceCount = Mathf.Max(0, candidateBehavior.DefaultProjectileCount);
                        spreadAngleStep = Mathf.Max(0f, candidateBehavior.SpreadAngleStep);
                        AcceptToken(entry, acceptedDisplays, acceptedTokens);
                        break;

                    case TokenType.Value:
                        if (token is not ValueTokenData valueToken)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored value token with an unexpected asset type.", token);
                            break;
                        }

                        if (pendingValueTarget == PendingValueTarget.Behavior && behaviorToken != null)
                        {
                            ApplyBehaviorValue(behaviorToken, valueToken, ref spreadProjectileCount, ref bounceCount, ref pierceCount);
                            pendingValueTarget = PendingValueTarget.None;
                            AcceptToken(entry, acceptedDisplays, acceptedTokens);
                            break;
                        }

                        if (pendingValueTarget == PendingValueTarget.Result && resultToken != null)
                        {
                            ApplyResultValue(resultToken, valueToken, ref resultEffects);
                            pendingValueTarget = PendingValueTarget.None;
                            AcceptToken(entry, acceptedDisplays, acceptedTokens);
                            break;
                        }

                        compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored value token because no behavior or result token is waiting for a numeric parameter.", token);
                        break;

                    case TokenType.Result:
                        if (token is not ResultTokenData candidateResult)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored result token with an unexpected asset type.", token);
                            break;
                        }

                        if (coreToken == null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored result token that appears before the core token.", token);
                            break;
                        }

                        if (resultToken != null)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored duplicate result token; the first result token already decided the impact effect.", token);
                            break;
                        }

                        resultToken = candidateResult;
                        hasExplicitResult = true;
                        pendingValueTarget = ShouldResultConsumeNumericValue(candidateResult)
                            ? PendingValueTarget.Result
                            : PendingValueTarget.None;
                        resultEffects = candidateResult.CreateResultEffects();
                        AcceptToken(entry, acceptedDisplays, acceptedTokens);
                        break;

                    case TokenType.Post:
                        if (!hasExplicitResult)
                        {
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored post token that appears before an explicit result token.", token);
                            break;
                        }

                        compiledAttack.AddPostToken(token);
                        AcceptToken(entry, acceptedDisplays, acceptedTokens);
                        break;

                    default:
                        compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored token with an unknown token type.", token);
                        break;
                }
            }

            if (coreToken == null)
            {
                compiledAttack.AttackSpec = AttackSpec.CreateDefault();
                compiledAttack.CoreType = AttackCoreType.None;
                compiledAttack.BehaviorType = AttackBehaviorType.None;
                compiledAttack.ResultType = AttackResultType.None;
                compiledAttack.DisplayText = string.Empty;
                compiledAttack.CanFire = false;
                compiledAttack.SpreadProjectileCount = 1;
                compiledAttack.SpreadAngleStep = 0f;
                compiledAttack.ExplosionRadius = 0f;
                compiledAttack.HasExplosion = false;
                compiledAttack.ScaleMultiplier = 1f;
                compiledAttack.ImpactRadiusMultiplier = 1f;
                compiledAttack.HasTextColorOverride = false;
                compiledAttack.HasFontSizeOverride = false;
                compiledAttack.ClearFontSizeModifiers();
                compiledAttack.CoreEffects = default;
                compiledAttack.ResultEffects = default;
                compiledAttack.AddMessage(AttackCompileMessageSeverity.Error, "Formula cannot fire because it does not contain a core token.");
                return compiledAttack;
            }

            AttackSpec attackSpec = coreToken.CreateBaseAttackSpec();
            AttackBehaviorType behaviorType = behaviorToken != null ? behaviorToken.BehaviorType : AttackBehaviorType.Straight;
            AttackResultType resultType = resultToken != null ? resultToken.ResultType : AttackResultType.DirectDamage;

            if (behaviorType == AttackBehaviorType.Spread)
            {
                spreadProjectileCount = Mathf.Max(1, spreadProjectileCount);
                if (behaviorToken != null)
                {
                    attackSpec.damage *= Mathf.Max(0f, behaviorToken.ProjectileDamageMultiplier);
                }
            }
            else
            {
                spreadProjectileCount = 1;
                spreadAngleStep = 0f;
            }

            if (behaviorType == AttackBehaviorType.Bounce)
            {
                attackSpec.bounceCount = Mathf.Max(0, bounceCount);
                attackSpec.pierceCount = 0;
            }
            else if (behaviorType == AttackBehaviorType.Pierce)
            {
                pierceCount = Mathf.Max(0, pierceCount);
                attackSpec.bounceCount = 0;
                attackSpec.pierceCount = pierceCount;

                if (behaviorToken != null && pierceCount > 0)
                {
                    float extensionMultiplier = 1f + (pierceCount * Mathf.Max(0f, behaviorToken.PierceLifetimeDistanceScalePerCount));
                    attackSpec.maxLifetime *= extensionMultiplier;
                    attackSpec.maxTravelDistance *= extensionMultiplier;
                }
            }
            else
            {
                attackSpec.bounceCount = 0;
                attackSpec.pierceCount = 0;
            }

            attackSpec.behaviorType = behaviorType;
            attackSpec.resultType = resultType;
            attackSpec.projectileCount = 1;
            attackSpec = attackSpec.GetSanitized();

            compiledAttack.AttackSpec = attackSpec;
            compiledAttack.CoreType = attackSpec.coreType;
            compiledAttack.BehaviorType = behaviorType;
            compiledAttack.ResultType = resultType;
            compiledAttack.DisplayText = BuildDisplayText(acceptedDisplays, coreToken);
            compiledAttack.CanFire = true;
            compiledAttack.SpreadProjectileCount = Mathf.Max(1, spreadProjectileCount);
            compiledAttack.SpreadAngleStep = Mathf.Max(0f, spreadAngleStep);
            compiledAttack.ExplosionRadius = resultType == AttackResultType.Explosion ? Mathf.Max(0f, resultEffects.explosionRadius) : 0f;
            compiledAttack.HasExplosion = resultType == AttackResultType.Explosion && resultEffects.HasExplosion;
            compiledAttack.ScaleMultiplier = 1f;
            compiledAttack.ImpactRadiusMultiplier = 1f;
            compiledAttack.HasTextColorOverride = false;
            compiledAttack.TextColor = Color.white;
            compiledAttack.HasFontSizeOverride = false;
            compiledAttack.FontSize = 0f;
            compiledAttack.ClearFontSizeModifiers();
            compiledAttack.CoreEffects = coreEffects.GetSanitized();
            compiledAttack.ResultEffects = resultEffects.GetSanitized();

            ApplyAcceptedTokenTextOverrides(compiledAttack, acceptedTokens);
            ApplyAcceptedTokenModifiers(compiledAttack, acceptedTokens);
            ApplyAcceptedItemDamageMultipliers(compiledAttack, itemInstances);
            ApplyPermanentDamageMultiplier(compiledAttack);
            compiledAttack.AttackSpec = compiledAttack.AttackSpec.GetSanitized();
            return compiledAttack;
        }

        private static List<CompileTokenEntry> ExpandItems(
            IReadOnlyList<PlaceableTokenData> items,
            CompiledAttack compiledAttack,
            ICollection<CompileItemInstance> itemInstances)
        {
            List<CompileTokenEntry> expandedTokens = new();
            if (items == null)
            {
                return expandedTokens;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PlaceableTokenData item = items[i];
                if (item == null)
                {
                    compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored null item in formula.");
                    continue;
                }

                List<BaseTokenData> itemTokens = new();
                item.AppendCompileTokens(itemTokens);
                if (itemTokens.Count <= 0)
                {
                    compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, $"Ignored item '{item.name}' because it does not contribute any compile tokens.");
                    continue;
                }

                CompileItemInstance itemInstance = new()
                {
                    item = item,
                    totalTokenCount = itemTokens.Count,
                    acceptedTokenCount = 0,
                };
                itemInstances?.Add(itemInstance);

                for (int j = 0; j < itemTokens.Count; j++)
                {
                    if (itemTokens[j] == null)
                    {
                        compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, $"Ignored null member token inside item '{item.name}'.");
                        continue;
                    }

                    expandedTokens.Add(new CompileTokenEntry(itemTokens[j], itemInstance));
                }
            }

            return expandedTokens;
        }

        private static void ApplyBehaviorValue(
            BehaviorTokenData behaviorToken,
            ValueTokenData valueToken,
            ref int spreadProjectileCount,
            ref int bounceCount,
            ref int pierceCount)
        {
            int resolvedValue = Mathf.Max(1, valueToken.GetRoundedIntValue());
            if (behaviorToken.BehaviorType == AttackBehaviorType.Spread)
            {
                spreadProjectileCount = resolvedValue;
            }
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Bounce)
            {
                bounceCount = resolvedValue;
            }
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Pierce)
            {
                pierceCount = resolvedValue;
            }
        }

        private static void ApplyResultValue(ResultTokenData resultToken, ValueTokenData valueToken, ref ResultEffectPayload resultEffects)
        {
            int resolvedValue = Mathf.Max(1, valueToken.GetRoundedIntValue());
            if (resultToken.ResultType == AttackResultType.Split)
            {
                resultEffects.splitProjectileCount = resolvedValue;
            }
            else if (resultToken.ResultType == AttackResultType.StatusEffect)
            {
                resultEffects.controlTriggerCount = resolvedValue;
            }
        }

        private static bool ShouldBehaviorConsumeNumericValue(BehaviorTokenData behaviorToken)
        {
            return behaviorToken != null &&
                   behaviorToken.AcceptsNumericValue &&
                   (behaviorToken.BehaviorType == AttackBehaviorType.Spread ||
                    behaviorToken.BehaviorType == AttackBehaviorType.Bounce ||
                    behaviorToken.BehaviorType == AttackBehaviorType.Pierce);
        }

        private static bool ShouldResultConsumeNumericValue(ResultTokenData resultToken)
        {
            return resultToken != null &&
                   resultToken.AcceptsNumericValue &&
                   (resultToken.ResultType == AttackResultType.Split ||
                    resultToken.ResultType == AttackResultType.StatusEffect);
        }

        private static string BuildDisplayText(IReadOnlyList<string> acceptedDisplays, CoreTokenData coreToken)
        {
            if (acceptedDisplays != null && acceptedDisplays.Count > 0)
            {
                StringBuilder builder = new();
                for (int i = 0; i < acceptedDisplays.Count; i++)
                {
                    string entry = acceptedDisplays[i];
                    if (!string.IsNullOrEmpty(entry))
                    {
                        builder.Append(entry);
                    }
                }

                if (builder.Length > 0)
                {
                    return builder.ToString();
                }
            }

            return coreToken != null ? coreToken.GetResolvedDisplayText() : string.Empty;
        }

        private static void AcceptToken(CompileTokenEntry entry, ICollection<string> acceptedDisplays, ICollection<BaseTokenData> acceptedTokens)
        {
            if (entry.Token == null)
            {
                return;
            }

            acceptedDisplays?.Add(entry.Token.GetResolvedDisplayText());
            acceptedTokens?.Add(entry.Token);
            if (entry.ItemInstance != null)
            {
                entry.ItemInstance.acceptedTokenCount++;
            }
        }

        private static void ApplyAcceptedTokenModifiers(CompiledAttack compiledAttack, IReadOnlyList<BaseTokenData> acceptedTokens)
        {
            if (compiledAttack == null || acceptedTokens == null)
            {
                return;
            }

            for (int i = 0; i < acceptedTokens.Count; i++)
            {
                BaseTokenData token = acceptedTokens[i];
                if (token == null || token.Modifiers == null)
                {
                    continue;
                }

                for (int j = 0; j < token.Modifiers.Count; j++)
                {
                    TokenModifierDefinition modifier = token.Modifiers[j];
                    if (!TokenModifierExpressionUtility.TryApplyModifier(compiledAttack, modifier, out string errorMessage))
                    {
                        compiledAttack.AddMessage(
                            AttackCompileMessageSeverity.Warning,
                            $"Ignored modifier '{modifier.expression}' on target '{modifier.target}': {errorMessage}",
                            token);
                    }
                }
            }
        }

        private static void ApplyAcceptedTokenTextOverrides(CompiledAttack compiledAttack, IReadOnlyList<BaseTokenData> acceptedTokens)
        {
            if (compiledAttack == null || acceptedTokens == null)
            {
                return;
            }

            for (int i = 0; i < acceptedTokens.Count; i++)
            {
                BaseTokenData token = acceptedTokens[i];
                if (token != null && token.HasBulletTextOverride)
                {
                    compiledAttack.DisplayText = token.BulletTextOverride;
                }
            }
        }

        private static void ApplyAcceptedItemDamageMultipliers(CompiledAttack compiledAttack, IReadOnlyList<CompileItemInstance> itemInstances)
        {
            if (compiledAttack == null || itemInstances == null)
            {
                return;
            }

            AttackSpec spec = compiledAttack.AttackSpec;
            bool hasChanged = false;
            for (int i = 0; i < itemInstances.Count; i++)
            {
                CompileItemInstance itemInstance = itemInstances[i];
                if (itemInstance == null ||
                    itemInstance.item == null ||
                    itemInstance.totalTokenCount <= 0 ||
                    itemInstance.acceptedTokenCount != itemInstance.totalTokenCount)
                {
                    continue;
                }

                float multiplier = Mathf.Max(1f, itemInstance.item.DamageMultiplier);
                if (Mathf.Approximately(multiplier, 1f))
                {
                    continue;
                }

                spec.damage *= multiplier;
                hasChanged = true;
            }

            if (hasChanged)
            {
                compiledAttack.AttackSpec = spec;
            }
        }

        private static void ApplyPermanentDamageMultiplier(CompiledAttack compiledAttack)
        {
            if (compiledAttack == null)
            {
                return;
            }

            float permanentDamageMultiplier = PermanentUpgradeService.GetCurrentDamageMultiplier();
            if (Mathf.Approximately(permanentDamageMultiplier, 1f))
            {
                return;
            }

            AttackSpec spec = compiledAttack.AttackSpec;
            spec.damage *= Mathf.Max(0f, permanentDamageMultiplier);
            compiledAttack.AttackSpec = spec;
        }
    }
}
