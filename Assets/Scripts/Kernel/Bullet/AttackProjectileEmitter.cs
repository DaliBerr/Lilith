using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 负责把编译后的攻击结果落地成一批实际发射的文字子弹。
    /// </summary>
    public static class AttackProjectileEmitter
    {
        public static int Emit(CharBullet bulletPrefab, Transform owner, Vector3 spawnPosition, Vector3 baseDirection, CompiledSpellProgram spellProgram)
        {
            return Emit(bulletPrefab, owner, spawnPosition, baseDirection, spellProgram, BulletTargetPolicy.EnemiesOnly);
        }

        public static int Emit(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            CompiledSpellProgram spellProgram,
            BulletTargetPolicy targetPolicy)
        {
            return Emit(bulletPrefab, owner, spawnPosition, baseDirection, spellProgram, targetPolicy, null, null);
        }

        public static int Emit(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            CompiledSpellProgram spellProgram,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            return Emit(
                bulletPrefab,
                owner,
                spawnPosition,
                baseDirection,
                spellProgram,
                BulletTargetPolicy.EnemiesOnly,
                parentOverride,
                spawnedBullets);
        }

        public static int Emit(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            CompiledSpellProgram spellProgram,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            return Emit(
                bulletPrefab,
                owner,
                spawnPosition,
                baseDirection,
                spellProgram,
                targetPolicy,
                parentOverride,
                spawnedBullets,
                activationCastCount: 1,
                activationSpreadAngleStep: 0f);
        }

        public static int Emit(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            CompiledSpellProgram spellProgram,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets,
            int activationCastCount,
            float activationSpreadAngleStep)
        {
            if (spellProgram == null || !spellProgram.CanCast || spellProgram.PrimaryCastBlock == null)
            {
                return 0;
            }

            int emittedCount = 0;
            IReadOnlyList<SpellProjectileNode> projectiles = spellProgram.PrimaryCastBlock.Projectiles;
            int castCount = Mathf.Max(1, activationCastCount);
            for (int castIndex = 0; castIndex < castCount; castIndex++)
            {
                Vector3 castDirection = ResolveActivationCastDirection(
                    baseDirection,
                    castIndex,
                    castCount,
                    activationSpreadAngleStep);
                for (int i = 0; i < projectiles.Count; i++)
                {
                    SpellProjectileNode projectile = projectiles[i];
                    emittedCount += EmitProjectileNode(
                        bulletPrefab,
                        owner,
                        spawnPosition,
                        castDirection,
                        projectile,
                        targetPolicy,
                        parentOverride,
                        spawnedBullets);
                }
            }

            return emittedCount;
        }

        public static int Emit(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            SpellProjectileNode projectile,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            return EmitProjectileNode(
                bulletPrefab,
                owner,
                spawnPosition,
                baseDirection,
                projectile,
                targetPolicy,
                parentOverride,
                spawnedBullets);
        }

        public static Vector3 ResolveActivationCastDirection(
            Vector3 baseDirection,
            int castIndex,
            int castCount,
            float activationSpreadAngleStep)
        {
            Vector3 normalizedDirection = baseDirection.normalized;
            if (normalizedDirection.sqrMagnitude <= 0f)
            {
                return Vector3.zero;
            }

            int resolvedCastCount = Mathf.Max(1, castCount);
            float resolvedAngleStep = Mathf.Max(0f, activationSpreadAngleStep);
            if (resolvedCastCount <= 1 || resolvedAngleStep <= 0f)
            {
                return normalizedDirection;
            }

            int resolvedCastIndex = Mathf.Clamp(castIndex, 0, resolvedCastCount - 1);
            float totalAngle = resolvedAngleStep * (resolvedCastCount - 1);
            float startAngle = -totalAngle * 0.5f;
            float currentAngle = startAngle + (resolvedAngleStep * resolvedCastIndex);
            return Quaternion.AngleAxis(currentAngle, Vector3.up) * normalizedDirection;
        }

        private static int EmitProjectileNode(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            SpellProjectileNode projectile,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            if (bulletPrefab == null || owner == null || projectile == null || !projectile.CanFire)
            {
                return 0;
            }

            Vector3 normalizedDirection = baseDirection.normalized;
            if (normalizedDirection.sqrMagnitude <= 0f)
            {
                return 0;
            }

            int emittedCount = 0;
            int projectileCount = Mathf.Max(1, projectile.ProjectileCount);
            if (projectile.BehaviorType == AttackBehaviorType.Spread && projectileCount > 1)
            {
                float spreadAngleStep = projectile.SpreadAngleStep;
                float totalAngle = spreadAngleStep * (projectileCount - 1);
                float startAngle = -totalAngle * 0.5f;
                for (int i = 0; i < projectileCount; i++)
                {
                    float currentAngle = startAngle + (spreadAngleStep * i);
                    Vector3 shotDirection = Quaternion.AngleAxis(currentAngle, Vector3.up) * normalizedDirection;
                    emittedCount += EmitProjectileNodeSingle(
                        bulletPrefab,
                        owner,
                        spawnPosition,
                        shotDirection,
                        projectile,
                        targetPolicy,
                        parentOverride,
                        spawnedBullets);
                }

                return emittedCount;
            }

            return EmitProjectileNodeSingle(
                bulletPrefab,
                owner,
                spawnPosition,
                normalizedDirection,
                projectile,
                targetPolicy,
                parentOverride,
                spawnedBullets);
        }

        private static int EmitProjectileNodeSingle(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 shotDirection,
            SpellProjectileNode projectile,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            CharBullet bulletInstance = parentOverride != null
                ? Object.Instantiate(bulletPrefab, spawnPosition, Quaternion.identity, parentOverride)
                : Object.Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
            bulletInstance.SetSpawnTemplate(bulletPrefab);
            bulletInstance.InitializeShot(owner, spawnPosition, shotDirection, projectile.AttackSpec, projectile, targetPolicy);
            spawnedBullets?.Add(bulletInstance);
            return 1;
        }

    }
}
