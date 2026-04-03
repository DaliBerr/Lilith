using System.Collections.Generic;
using System.Text;
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

        /// <summary>
        /// summary: 从左到右编译一组词元，尽力生成一个可执行的攻击结果。
        /// param: tokens 当前装备槽中的有序词元列表
        /// returns: 编译后的攻击结果；缺少核心词元时会返回不可发射状态
        /// </summary>
        public static CompiledAttack Compile(IReadOnlyList<BaseTokenData> tokens)
        {
            var compiledAttack = new CompiledAttack();
            var acceptedDisplays = new List<string>();
            var acceptedTokens = new List<BaseTokenData>();

            CoreTokenData coreToken = null;
            BehaviorTokenData behaviorToken = null;
            ResultTokenData resultToken = null;
            PendingValueTarget pendingValueTarget = PendingValueTarget.None;
            bool hasExplicitResult = false;

            int spreadProjectileCount = 1;
            float spreadAngleStep = 0f;
            float explosionRadius = 0f;

            if (tokens != null)
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    BaseTokenData token = tokens[i];
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
                            AcceptToken(token, acceptedDisplays, acceptedTokens);
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
                            AcceptToken(token, acceptedDisplays, acceptedTokens);
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
                            pendingValueTarget = candidateBehavior.AcceptsNumericValue ? PendingValueTarget.Behavior : PendingValueTarget.None;
                            spreadProjectileCount = Mathf.Max(1, candidateBehavior.DefaultProjectileCount);
                            spreadAngleStep = Mathf.Max(0f, candidateBehavior.SpreadAngleStep);
                            AcceptToken(token, acceptedDisplays, acceptedTokens);
                            break;

                        case TokenType.Value:
                            if (token is not ValueTokenData valueToken)
                            {
                                compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored value token with an unexpected asset type.", token);
                                break;
                            }

                            if (pendingValueTarget == PendingValueTarget.Behavior && behaviorToken != null)
                            {
                                ApplyBehaviorValue(behaviorToken, valueToken, ref spreadProjectileCount);
                                pendingValueTarget = PendingValueTarget.None;
                                AcceptToken(token, acceptedDisplays, acceptedTokens);
                                break;
                            }

                            if (pendingValueTarget == PendingValueTarget.Result && resultToken != null)
                            {
                                ApplyResultValue(resultToken, valueToken, ref explosionRadius);
                                pendingValueTarget = PendingValueTarget.None;
                                AcceptToken(token, acceptedDisplays, acceptedTokens);
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
                            pendingValueTarget = candidateResult.AcceptsNumericValue ? PendingValueTarget.Result : PendingValueTarget.None;
                            explosionRadius = Mathf.Max(0f, candidateResult.DefaultExplosionRadius);
                            AcceptToken(token, acceptedDisplays, acceptedTokens);
                            break;

                        case TokenType.Post:
                            if (!hasExplicitResult)
                            {
                                compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored post token that appears before an explicit result token.", token);
                                break;
                            }

                            compiledAttack.AddPostToken(token);
                            AcceptToken(token, acceptedDisplays, acceptedTokens);
                            break;

                        default:
                            compiledAttack.AddMessage(AttackCompileMessageSeverity.Warning, "Ignored token with an unknown token type.", token);
                            break;
                    }
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
                compiledAttack.AddMessage(AttackCompileMessageSeverity.Error, "Formula cannot fire because it does not contain a core token.");
                return compiledAttack;
            }

            AttackSpec attackSpec = coreToken.CreateBaseAttackSpec();
            AttackBehaviorType behaviorType = behaviorToken != null ? behaviorToken.BehaviorType : AttackBehaviorType.Straight;
            AttackResultType resultType = resultToken != null ? resultToken.ResultType : AttackResultType.DirectDamage;

            if (behaviorType == AttackBehaviorType.Spread)
            {
                spreadProjectileCount = Mathf.Max(1, spreadProjectileCount);
            }
            else
            {
                spreadProjectileCount = 1;
                spreadAngleStep = 0f;
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
            compiledAttack.ExplosionRadius = resultType == AttackResultType.Explosion ? Mathf.Max(0f, explosionRadius) : 0f;
            compiledAttack.HasExplosion = resultType == AttackResultType.Explosion;
            compiledAttack.ScaleMultiplier = 1f;
            compiledAttack.ImpactRadiusMultiplier = 1f;
            compiledAttack.HasTextColorOverride = false;
            compiledAttack.TextColor = Color.white;
            compiledAttack.HasFontSizeOverride = false;
            compiledAttack.FontSize = 0f;
            compiledAttack.ClearFontSizeModifiers();

            ApplyAcceptedTokenTextOverrides(compiledAttack, acceptedTokens);
            ApplyAcceptedTokenModifiers(compiledAttack, acceptedTokens);
            compiledAttack.AttackSpec = compiledAttack.AttackSpec.GetSanitized();
            return compiledAttack;
        }

        private static void ApplyBehaviorValue(BehaviorTokenData behaviorToken, ValueTokenData valueToken, ref int spreadProjectileCount)
        {
            if (behaviorToken.BehaviorType == AttackBehaviorType.Spread)
            {
                spreadProjectileCount = Mathf.Max(1, valueToken.GetRoundedIntValue());
            }
        }

        private static void ApplyResultValue(ResultTokenData resultToken, ValueTokenData valueToken, ref float explosionRadius)
        {
            if (resultToken.ResultType == AttackResultType.Explosion)
            {
                explosionRadius = Mathf.Max(0f, valueToken.NumericValue);
            }
        }

        private static string BuildDisplayText(IReadOnlyList<string> acceptedDisplays, CoreTokenData coreToken)
        {
            if (acceptedDisplays != null && acceptedDisplays.Count > 0)
            {
                var builder = new StringBuilder();
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

        private static void AcceptToken(BaseTokenData token, ICollection<string> acceptedDisplays, ICollection<BaseTokenData> acceptedTokens)
        {
            if (token == null)
            {
                return;
            }

            acceptedDisplays?.Add(token.GetResolvedDisplayText());
            acceptedTokens?.Add(token);
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
    }
}
