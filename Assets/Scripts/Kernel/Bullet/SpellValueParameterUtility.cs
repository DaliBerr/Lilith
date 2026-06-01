using UnityEngine;

namespace Kernel.Bullet
{
    public static class SpellValueParameterUtility
    {
        public static bool CanConsumeValue(BehaviorTokenData behaviorToken)
        {
            return behaviorToken != null &&
                   behaviorToken.ValueParameterKind == SpellValueParameterKind.Count &&
                   (behaviorToken.BehaviorType == AttackBehaviorType.Spread ||
                    behaviorToken.BehaviorType == AttackBehaviorType.Bounce ||
                    behaviorToken.BehaviorType == AttackBehaviorType.Pierce);
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
                _ => false,
            };
        }

        public static void ApplyBehaviorValue(
            BehaviorTokenData behaviorToken,
            ValueTokenData valueToken,
            ref int spreadProjectileCount,
            ref int bounceCount,
            ref int pierceCount)
        {
            if (!CanConsumeValue(behaviorToken) || valueToken == null)
            {
                return;
            }

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

        public static void ApplyResultValue(
            ResultTokenData resultToken,
            ValueTokenData valueToken,
            ref ResultEffectPayload resultEffects)
        {
            if (!CanConsumeValue(resultToken) || valueToken == null)
            {
                return;
            }

            float numericValue = Mathf.Max(0f, valueToken.NumericValue);
            int countValue = Mathf.Max(1, valueToken.GetRoundedIntValue());
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

                    break;
            }
        }
    }
}
