using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// Runtime-only lingering result area used by the Leave result token.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LingeringResultAreaRuntime : MonoBehaviour
    {
        private const float MinimumTickIntervalSeconds = 0.05f;

        private float remainingDuration;
        private float tickIntervalSeconds;
        private float nextTickTime;
        private float tickDamage;
        private float radius;
        private LayerMask impactMask;
        private BulletTargetPolicy targetPolicy;
        private Transform ownerRoot;
        private CoreEffectPayload coreEffects;

        public static LingeringResultAreaRuntime Spawn(
            Vector3 worldPosition,
            float duration,
            float tickInterval,
            float radius,
            float tickDamage,
            LayerMask impactMask,
            BulletTargetPolicy targetPolicy,
            Transform ownerRoot,
            CoreEffectPayload coreEffects)
        {
            if (duration <= 0f || tickInterval <= 0f || radius <= 0f)
            {
                return null;
            }

            GameObject areaObject = new("Lingering Result Area");
            areaObject.transform.position = worldPosition;
            LingeringResultAreaRuntime area = areaObject.AddComponent<LingeringResultAreaRuntime>();
            area.Initialize(
                duration,
                tickInterval,
                radius,
                tickDamage,
                impactMask,
                targetPolicy,
                ownerRoot,
                coreEffects);
            return area;
        }

        private void Initialize(
            float duration,
            float tickInterval,
            float areaRadius,
            float damagePerTick,
            LayerMask mask,
            BulletTargetPolicy policy,
            Transform owner,
            CoreEffectPayload effects)
        {
            remainingDuration = Mathf.Max(0f, duration);
            tickIntervalSeconds = Mathf.Max(MinimumTickIntervalSeconds, tickInterval);
            radius = Mathf.Max(0f, areaRadius);
            tickDamage = Mathf.Max(0f, damagePerTick);
            impactMask = mask;
            targetPolicy = policy;
            ownerRoot = owner;
            coreEffects = effects.GetSanitized();
            nextTickTime = 0f;
        }

        private void Update()
        {
            if (remainingDuration <= 0f)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }

                return;
            }

            float deltaTime = Time.deltaTime;
            remainingDuration = Mathf.Max(0f, remainingDuration - deltaTime);
            nextTickTime -= deltaTime;
            if (nextTickTime > 0f)
            {
                return;
            }

            nextTickTime += tickIntervalSeconds;
            ApplyTick();
        }

        private void ApplyTick()
        {
            Collider[] overlaps = Physics.OverlapSphere(transform.position, radius, impactMask, QueryTriggerInteraction.Ignore);
            HashSet<int> visitedRoots = new();
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null || overlap.isTrigger || overlap.GetComponentInParent<CharBullet>() != null)
                {
                    continue;
                }

                Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
                if (overlapRoot == null ||
                    overlapRoot == ownerRoot ||
                    !visitedRoots.Add(overlapRoot.GetInstanceID()))
                {
                    continue;
                }

                Enemy enemy = overlap.GetComponentInParent<Enemy>();
                if (enemy != null && !enemy.Equals(null) && CanAffectEnemies())
                {
                    if (tickDamage > 0f)
                    {
                        enemy.TryApplyDamage(tickDamage, out _, out _);
                    }

                    if (enemy.TryGetComponent(out EnemyStatusEffectController statusController))
                    {
                        if (coreEffects.HasBurn)
                        {
                            statusController.RegisterFireHit(coreEffects.burnTriggerCount, coreEffects.burnDamagePerSecond, coreEffects.burnDuration);
                        }

                        if (coreEffects.HasSlow)
                        {
                            statusController.ApplySlow(coreEffects.slowPercent, coreEffects.slowDuration);
                        }

                        if (coreEffects.HasStatusApplications)
                        {
                            statusController.TryApplyStatusApplications(coreEffects.statusApplications);
                        }
                    }

                    continue;
                }

                PlayerHealth playerHealth = overlap.GetComponentInParent<PlayerHealth>();
                if (CanAffectPlayer() &&
                    playerHealth != null &&
                    tickDamage > 0f)
                {
                    playerHealth.TryApplyDamage(tickDamage, out _, out _);
                }
            }
        }

        private bool CanAffectEnemies()
        {
            return targetPolicy == BulletTargetPolicy.EnemiesOnly ||
                   targetPolicy == BulletTargetPolicy.Both;
        }

        private bool CanAffectPlayer()
        {
            return targetPolicy == BulletTargetPolicy.PlayerOnly ||
                   targetPolicy == BulletTargetPolicy.Both;
        }
    }
}
