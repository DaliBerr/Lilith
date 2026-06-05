using UnityEngine;

namespace Kernel.Bullet
{
    public static class SpellValueParameterUtility
    {
        public static bool CanConsumeValue(BehaviorTokenData behaviorToken)
        {
            if (behaviorToken == null)
            {
                return false;
            }

            return behaviorToken.BehaviorType switch
            {
                AttackBehaviorType.Spread => behaviorToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackBehaviorType.Bounce => behaviorToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackBehaviorType.Pierce => behaviorToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackBehaviorType.Chain => behaviorToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackBehaviorType.Stasis => behaviorToken.ValueParameterKind == SpellValueParameterKind.Duration,
                AttackBehaviorType.Rush => behaviorToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackBehaviorType.Slow => behaviorToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackBehaviorType.Snake => behaviorToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackBehaviorType.Wander => behaviorToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackBehaviorType.Split => behaviorToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackBehaviorType.Spin => behaviorToken.ValueParameterKind == SpellValueParameterKind.Radius,
                _ => false,
            };
        }

        public static bool CanConsumeValue(ResultTokenData resultToken)
        {
            if (resultToken == null)
            {
                return false;
            }

            return resultToken.ResultType switch
            {
                AttackResultType.Explosion => resultToken.ValueParameterKind == SpellValueParameterKind.Radius ||
                                              resultToken.ValueParameterKind == SpellValueParameterKind.Duration,
                AttackResultType.Split => resultToken.ValueParameterKind == SpellValueParameterKind.Count,
                AttackResultType.StatusEffect => resultToken.ValueParameterKind == SpellValueParameterKind.Count ||
                                                 resultToken.ValueParameterKind == SpellValueParameterKind.Duration,
                AttackResultType.Healing => resultToken.ValueParameterKind == SpellValueParameterKind.Radius,
                AttackResultType.Drain => resultToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackResultType.Shield => resultToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackResultType.Push => resultToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackResultType.Pull => resultToken.ValueParameterKind == SpellValueParameterKind.Strength,
                AttackResultType.Leave => resultToken.ValueParameterKind == SpellValueParameterKind.Duration,
                _ => false,
            };
        }

        public static void ApplyBehaviorValue(
            BehaviorTokenData behaviorToken,
            ValueTokenData valueToken,
            ref int spreadProjectileCount,
            ref int bounceCount,
            ref int chainCount,
            ref int pierceCount,
            ref float behaviorParameter)
        {
            if (!CanConsumeValue(behaviorToken) || valueToken == null)
            {
                return;
            }

            int resolvedValue = valueToken.ResolveCountValue();
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
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Chain)
            {
                chainCount = resolvedValue;
            }
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Split)
            {
                behaviorParameter = resolvedValue;
            }
            else
            {
                float currentValue = Mathf.Max(0f, behaviorParameter);
                bool allowZero = AllowsZero(behaviorToken.ValueParameterKind);
                behaviorParameter = valueToken.ResolveNumericValue(behaviorToken.ValueParameterKind, currentValue, allowZero);
            }
        }

        public static void ApplyResultValue(
            ResultTokenData resultToken,
            ValueTokenData valueToken,
            ref ResultEffectPayload resultEffects)
        {
            if (!CanConsumeValue(resultToken) || valueToken == null)
            {
                return;
            }

            float currentValue = ResolveCurrentResultParameterValue(resultToken, resultEffects);
            float numericValue = valueToken.ResolveNumericValue(resultToken.ValueParameterKind, currentValue, AllowsZero(resultToken.ValueParameterKind));
            int countValue = valueToken.ResolveCountValue(Mathf.RoundToInt(currentValue));
            switch (resultToken.ValueParameterKind)
            {
                case SpellValueParameterKind.Count:
                    if (resultToken.ResultType == AttackResultType.Split)
                    {
                        resultEffects.splitProjectileCount = countValue;
                    }
                    else if (resultToken.ResultType == AttackResultType.StatusEffect)
                    {
                        resultEffects.controlTriggerCount = countValue;
                    }

                    break;

                case SpellValueParameterKind.Radius:
                    if (resultToken.ResultType == AttackResultType.Explosion)
                    {
                        resultEffects.explosionRadius = numericValue;
                    }
                    else if (resultToken.ResultType == AttackResultType.Healing)
                    {
                        resultEffects.effectRadius = numericValue;
                    }

                    break;

                case SpellValueParameterKind.Duration:
                    if (resultToken.ResultType == AttackResultType.Explosion)
                    {
                        resultEffects.explosionDelaySeconds = numericValue;
                    }
                    else if (resultToken.ResultType == AttackResultType.StatusEffect)
                    {
                        resultEffects.controlDuration = numericValue;
                    }
                    else if (resultToken.ResultType == AttackResultType.Leave)
                    {
                        resultEffects.effectDuration = numericValue;
                    }

                    break;

                case SpellValueParameterKind.Strength:
                    if (resultToken.ResultType == AttackResultType.Drain ||
                        resultToken.ResultType == AttackResultType.Shield ||
                        resultToken.ResultType == AttackResultType.Push ||
                        resultToken.ResultType == AttackResultType.Pull)
                    {
                        resultEffects.effectStrength = numericValue;
                    }

                    break;
            }
        }

        private static float ResolveCurrentResultParameterValue(ResultTokenData resultToken, ResultEffectPayload resultEffects)
        {
            if (resultToken == null)
            {
                return 0f;
            }

            return resultToken.ValueParameterKind switch
            {
                SpellValueParameterKind.Count when resultToken.ResultType == AttackResultType.Split => resultEffects.splitProjectileCount,
                SpellValueParameterKind.Count when resultToken.ResultType == AttackResultType.StatusEffect => resultEffects.controlTriggerCount,
                SpellValueParameterKind.Radius when resultToken.ResultType == AttackResultType.Explosion => resultEffects.explosionRadius,
                SpellValueParameterKind.Radius when resultToken.ResultType == AttackResultType.Healing => resultEffects.effectRadius,
                SpellValueParameterKind.Duration when resultToken.ResultType == AttackResultType.Explosion => resultEffects.explosionDelaySeconds,
                SpellValueParameterKind.Duration when resultToken.ResultType == AttackResultType.StatusEffect => resultEffects.controlDuration,
                SpellValueParameterKind.Duration when resultToken.ResultType == AttackResultType.Leave => resultEffects.effectDuration,
                SpellValueParameterKind.Strength when resultToken.ResultType == AttackResultType.Drain => resultEffects.effectStrength,
                SpellValueParameterKind.Strength when resultToken.ResultType == AttackResultType.Shield => resultEffects.effectStrength,
                SpellValueParameterKind.Strength when resultToken.ResultType == AttackResultType.Push => resultEffects.effectStrength,
                SpellValueParameterKind.Strength when resultToken.ResultType == AttackResultType.Pull => resultEffects.effectStrength,
                _ => 0f,
            };
        }

        private static bool AllowsZero(SpellValueParameterKind parameterKind)
        {
            return parameterKind == SpellValueParameterKind.Radius ||
                   parameterKind == SpellValueParameterKind.Duration;
        }
    }
}
