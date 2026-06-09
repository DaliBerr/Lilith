using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 负责把编译后的攻击结果落地成一批实际发射的文字子弹。
    /// </summary>
    public static class AttackProjectileEmitter
    {
        private const float OrbitMulticastRadius = 3f;
        private static readonly Vocalith.Random RandomSource = new();
        internal static Func<int, int> RiddleCandidateIndexResolver { get; set; } = count => RandomSource.Next(0, count);

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
            SpellCastBlock primaryBlock = spellProgram.PrimaryCastBlock;
            IReadOnlyList<SpellProjectileNode> projectiles = primaryBlock.Projectiles;
            int castCount = Mathf.Max(1, activationCastCount);
            for (int castIndex = 0; castIndex < castCount; castIndex++)
            {
                Vector3 castDirection = ResolveActivationCastDirection(
                    baseDirection,
                    castIndex,
                    castCount,
                    activationSpreadAngleStep);
                emittedCount += EmitCastBlockProjectiles(
                    bulletPrefab,
                    owner,
                    spawnPosition,
                    castDirection,
                    primaryBlock,
                    targetPolicy,
                    parentOverride,
                    spawnedBullets);
            }

            return emittedCount;
        }

        private static int EmitCastBlockProjectiles(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            SpellCastBlock castBlock,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            if (castBlock == null || castBlock.Projectiles.Count <= 0)
            {
                return 0;
            }

            IReadOnlyList<SpellProjectileNode> projectiles = castBlock.Projectiles;
            if (castBlock.CastPattern == SpellCastPattern.Orbit)
            {
                return EmitOrbitCastBlockProjectiles(
                    bulletPrefab,
                    owner,
                    spawnPosition,
                    baseDirection,
                    projectiles,
                    targetPolicy,
                    parentOverride,
                    spawnedBullets);
            }

            if (castBlock.CastPattern == SpellCastPattern.Sequential &&
                Application.isPlaying &&
                castBlock.SequentialIntervalSeconds > 0f &&
                projectiles.Count > 1)
            {
                ScheduleSequentialEmission(
                    bulletPrefab,
                    owner,
                    spawnPosition,
                    baseDirection,
                    castBlock,
                    targetPolicy,
                    parentOverride,
                    spawnedBullets);
                return CountFireableProjectiles(projectiles);
            }

            int emittedCount = 0;
            for (int i = 0; i < projectiles.Count; i++)
            {
                SpellProjectileNode projectile = projectiles[i];
                Vector3 projectileDirection = ResolveCastBlockProjectileDirection(
                    baseDirection,
                    castBlock,
                    i,
                    projectiles.Count);
                emittedCount += EmitProjectileNode(
                    bulletPrefab,
                    owner,
                    spawnPosition,
                    projectileDirection,
                    projectile,
                    targetPolicy,
                    parentOverride,
                    spawnedBullets);
            }

            return emittedCount;
        }

        private static int EmitOrbitCastBlockProjectiles(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            IReadOnlyList<SpellProjectileNode> projectiles,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            if (projectiles == null || projectiles.Count <= 0)
            {
                return 0;
            }

            Vector3 normalizedDirection = ResolvePlanarDirection(baseDirection, Vector3.forward);
            List<CharBullet> mainSpawnedBullets = new();
            int emittedCount = EmitProjectileNode(
                bulletPrefab,
                owner,
                spawnPosition,
                normalizedDirection,
                projectiles[0],
                targetPolicy,
                parentOverride,
                mainSpawnedBullets);
            AddSpawnedBullets(spawnedBullets, mainSpawnedBullets);

            CharBullet anchorBullet = ResolveOrbitAnchor(mainSpawnedBullets);
            if (anchorBullet == null)
            {
                return emittedCount;
            }

            Transform anchorTransform = anchorBullet.MovementTarget;
            for (int i = 1; i < projectiles.Count; i++)
            {
                SpellProjectileNode orbitProjectile = CreateOrbitingProjectileNode(projectiles[i]);
                if (orbitProjectile == null || !orbitProjectile.CanFire)
                {
                    continue;
                }

                Vector3 radialDirection = ResolvePlanarRightDirection(normalizedDirection);
                Vector3 orbitSpawnPosition = anchorTransform.position + (radialDirection * OrbitMulticastRadius);
                Vector3 orbitDirection = ResolveClosestTangentDirection(radialDirection, normalizedDirection);
                List<CharBullet> orbitSpawnedBullets = new();
                emittedCount += EmitProjectileNode(
                    bulletPrefab,
                    owner,
                    orbitSpawnPosition,
                    orbitDirection,
                    orbitProjectile,
                    targetPolicy,
                    parentOverride,
                    orbitSpawnedBullets);

                for (int bulletIndex = 0; bulletIndex < orbitSpawnedBullets.Count; bulletIndex++)
                {
                    CharBullet orbitBullet = orbitSpawnedBullets[bulletIndex];
                    if (orbitBullet != null)
                    {
                        orbitBullet.SetMovementAnchor(anchorTransform, expireWhenInvalid: true);
                    }
                }

                AddSpawnedBullets(spawnedBullets, orbitSpawnedBullets);
            }

            return emittedCount;
        }

        private static SpellProjectileNode CreateOrbitingProjectileNode(SpellProjectileNode source)
        {
            if (source == null)
            {
                return null;
            }

            AttackSpec orbitSpec = source.AttackSpec;
            orbitSpec.behaviorType = AttackBehaviorType.Spin;
            orbitSpec.behaviorParameter = OrbitMulticastRadius;
            orbitSpec.projectileCount = 1;
            return SpellProjectileNode.CreateWithRuntimeMovementOverride(source, orbitSpec);
        }

        private static CharBullet ResolveOrbitAnchor(IReadOnlyList<CharBullet> spawnedBullets)
        {
            if (spawnedBullets == null)
            {
                return null;
            }

            for (int i = 0; i < spawnedBullets.Count; i++)
            {
                CharBullet bullet = spawnedBullets[i];
                if (bullet != null && bullet.IsActiveShot)
                {
                    return bullet;
                }
            }

            return null;
        }

        private static void AddSpawnedBullets(ICollection<CharBullet> target, IReadOnlyList<CharBullet> spawnedBullets)
        {
            if (target == null || spawnedBullets == null)
            {
                return;
            }

            for (int i = 0; i < spawnedBullets.Count; i++)
            {
                target.Add(spawnedBullets[i]);
            }
        }

        private static void ScheduleSequentialEmission(
            CharBullet bulletPrefab,
            Transform owner,
            Vector3 spawnPosition,
            Vector3 baseDirection,
            SpellCastBlock castBlock,
            BulletTargetPolicy targetPolicy,
            Transform parentOverride,
            ICollection<CharBullet> spawnedBullets)
        {
            GameObject runnerObject = new("SpellSequentialEmissionRunner");
            if (parentOverride != null)
            {
                runnerObject.transform.SetParent(parentOverride, worldPositionStays: true);
            }

            SpellCastBlockEmissionRunner runner = runnerObject.AddComponent<SpellCastBlockEmissionRunner>();
            runner.Initialize(
                bulletPrefab,
                owner,
                spawnPosition,
                baseDirection,
                castBlock,
                targetPolicy,
                parentOverride,
                spawnedBullets);
        }

        private static Vector3 ResolveCastBlockProjectileDirection(
            Vector3 baseDirection,
            SpellCastBlock castBlock,
            int projectileIndex,
            int projectileCount)
        {
            if (castBlock == null)
            {
                return baseDirection;
            }

            if (castBlock.CastPattern == SpellCastPattern.Fork)
            {
                return ResolveActivationCastDirection(
                    baseDirection,
                    projectileIndex,
                    projectileCount,
                    castBlock.PatternAngleStep);
            }

            if (castBlock.CastPattern == SpellCastPattern.Orbit)
            {
                return baseDirection;
            }

            return baseDirection;
        }

        private static int CountFireableProjectiles(IReadOnlyList<SpellProjectileNode> projectiles)
        {
            if (projectiles == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i] != null && projectiles[i].CanFire)
                {
                    count++;
                }
            }

            return count;
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

        private static Vector3 ResolvePlanarDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
            {
                return direction.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector3.forward;
        }

        private static Vector3 ResolvePlanarRightDirection(Vector3 direction)
        {
            Vector3 right = Vector3.Cross(Vector3.up, ResolvePlanarDirection(direction, Vector3.forward));
            return right.sqrMagnitude > 0.000001f ? right.normalized : Vector3.right;
        }

        private static Vector3 ResolveClosestTangentDirection(Vector3 radialDirection, Vector3 desiredDirection)
        {
            Vector3 positiveTangent = Quaternion.AngleAxis(90f, Vector3.up) * ResolvePlanarDirection(radialDirection, Vector3.right);
            Vector3 negativeTangent = -positiveTangent;
            Vector3 desired = ResolvePlanarDirection(desiredDirection, Vector3.forward);
            return Vector3.Dot(positiveTangent, desired) >= Vector3.Dot(negativeTangent, desired)
                ? positiveTangent
                : negativeTangent;
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
            projectile = ResolveRiddleProjectile(projectile);
            CharBullet bulletInstance = parentOverride != null
                ? UnityEngine.Object.Instantiate(bulletPrefab, spawnPosition, Quaternion.identity, parentOverride)
                : UnityEngine.Object.Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
            bulletInstance.SetSpawnTemplate(bulletPrefab);
            bulletInstance.InitializeShot(owner, spawnPosition, shotDirection, projectile.AttackSpec, projectile, targetPolicy);
            spawnedBullets?.Add(bulletInstance);
            return 1;
        }

        private static SpellProjectileNode ResolveRiddleProjectile(SpellProjectileNode projectile)
        {
            if (projectile == null || projectile.CoreType != AttackCoreType.Riddle)
            {
                return projectile;
            }

            int candidateCount = SpellCoreRuntimeCatalog.RiddleCandidateCount;
            if (candidateCount <= 0)
            {
                return projectile;
            }

            int rawIndex = RiddleCandidateIndexResolver != null
                ? RiddleCandidateIndexResolver(candidateCount)
                : RandomSource.Next(0, candidateCount);
            int resolvedIndex = Mathf.Clamp(rawIndex, 0, candidateCount - 1);
            if (!SpellCoreRuntimeCatalog.TryGetRiddleCandidate(resolvedIndex, out SpellCoreRuntimeTemplate template))
            {
                return projectile;
            }

            AttackSpec resolvedSpec = template.ApplyTo(projectile.AttackSpec);
            return SpellProjectileNode.CreateWithRuntimeCoreOverride(
                projectile,
                resolvedSpec,
                template.CoreType,
                template.CoreEffects,
                template.DisplayText);
        }

        private sealed class SpellCastBlockEmissionRunner : MonoBehaviour
        {
            private CharBullet bulletPrefab;
            private Transform owner;
            private Vector3 spawnPosition;
            private Vector3 baseDirection;
            private SpellCastBlock castBlock;
            private BulletTargetPolicy targetPolicy;
            private Transform parentOverride;
            private ICollection<CharBullet> spawnedBullets;

            public void Initialize(
                CharBullet bulletPrefab,
                Transform owner,
                Vector3 spawnPosition,
                Vector3 baseDirection,
                SpellCastBlock castBlock,
                BulletTargetPolicy targetPolicy,
                Transform parentOverride,
                ICollection<CharBullet> spawnedBullets)
            {
                this.bulletPrefab = bulletPrefab;
                this.owner = owner;
                this.spawnPosition = spawnPosition;
                this.baseDirection = baseDirection;
                this.castBlock = castBlock;
                this.targetPolicy = targetPolicy;
                this.parentOverride = parentOverride;
                this.spawnedBullets = spawnedBullets;
                StartCoroutine(EmitSequentially());
            }

            private IEnumerator EmitSequentially()
            {
                IReadOnlyList<SpellProjectileNode> projectiles = castBlock != null ? castBlock.Projectiles : null;
                if (projectiles == null)
                {
                    Destroy(gameObject);
                    yield break;
                }

                for (int i = 0; i < projectiles.Count; i++)
                {
                    if (i > 0 && castBlock.SequentialIntervalSeconds > 0f)
                    {
                        yield return new WaitForSeconds(castBlock.SequentialIntervalSeconds);
                    }

                    if (owner == null)
                    {
                        break;
                    }

                    EmitProjectileNode(
                        bulletPrefab,
                        owner,
                        spawnPosition,
                        baseDirection,
                        projectiles[i],
                        targetPolicy,
                        parentOverride,
                        spawnedBullets);
                }

                Destroy(gameObject);
            }
        }
    }

    internal readonly struct SpellCoreRuntimeTemplate
    {
        public SpellCoreRuntimeTemplate(
            AttackCoreType coreType,
            string displayText,
            float damage,
            float projectileSpeed,
            float maxTravelDistance,
            CoreEffectPayload coreEffects)
        {
            CoreType = coreType;
            DisplayText = displayText ?? string.Empty;
            Damage = Mathf.Max(0f, damage);
            ProjectileSpeed = Mathf.Max(0f, projectileSpeed);
            MaxTravelDistance = Mathf.Max(0f, maxTravelDistance);
            CoreEffects = coreEffects.GetSanitized();
        }

        public AttackCoreType CoreType { get; }
        public string DisplayText { get; }
        public float Damage { get; }
        public float ProjectileSpeed { get; }
        public float MaxTravelDistance { get; }
        public CoreEffectPayload CoreEffects { get; }

        public AttackSpec ApplyTo(AttackSpec source)
        {
            AttackSpec resolved = source;
            resolved.coreType = CoreType;
            resolved.damage = Damage;
            resolved.projectileSpeed = ProjectileSpeed;
            resolved.maxTravelDistance = MaxTravelDistance;
            resolved.maxLifetime = MaxTravelDistance / Mathf.Max(1f, ProjectileSpeed) + 0.15f;
            return resolved.GetSanitized();
        }
    }

    internal static class SpellCoreRuntimeCatalog
    {
        private static readonly SpellCoreRuntimeTemplate[] RiddleCandidates =
        {
            CreateTemplate(AttackCoreType.Arrow, "箭", 6f, 150f, 240f),
            CreateTemplate(
                AttackCoreType.Fire,
                "火",
                7f,
                128f,
                224f,
                new SpellStatusApplication(SpellStatusSlot.Ignite, 1f, duration: 2f, strength: 1f)),
            CreateTemplate(
                AttackCoreType.Ice,
                "冰",
                6f,
                120f,
                224f,
                new SpellStatusApplication(SpellStatusSlot.Freeze, 1f, duration: 1.5f, strength: 1f)),
            CreateTemplate(
                AttackCoreType.Thunder,
                "雷",
                7f,
                140f,
                232f,
                new SpellStatusApplication(SpellStatusSlot.Disable, 1f, duration: 0.6f, strength: 1f)),
            CreateTemplate(
                AttackCoreType.Rock,
                "岩",
                9f,
                104f,
                208f,
                new SpellStatusApplication(SpellStatusSlot.Disable, 0.5f, duration: 0.4f, strength: 1f)),
            CreateTemplate(AttackCoreType.Edge, "刃", 8f, 156f, 240f),
            CreateTemplate(
                AttackCoreType.Toxin,
                "毒",
                6f,
                118f,
                224f,
                new SpellStatusApplication(SpellStatusSlot.Corrosion, 1f, duration: 3f, strength: 1f)),
            CreateTemplate(
                AttackCoreType.Shadow,
                "影",
                6.5f,
                136f,
                232f,
                new SpellStatusApplication(SpellStatusSlot.Mark, 1f, duration: 3f, strength: 1f)),
        };

        public static int RiddleCandidateCount => RiddleCandidates.Length;

        public static bool TryGetRiddleCandidate(int index, out SpellCoreRuntimeTemplate template)
        {
            if (index < 0 || index >= RiddleCandidates.Length)
            {
                template = default;
                return false;
            }

            template = RiddleCandidates[index];
            return true;
        }

        private static SpellCoreRuntimeTemplate CreateTemplate(
            AttackCoreType coreType,
            string displayText,
            float damage,
            float projectileSpeed,
            float maxTravelDistance,
            params SpellStatusApplication[] statusApplications)
        {
            return new SpellCoreRuntimeTemplate(
                coreType,
                displayText,
                damage,
                projectileSpeed,
                maxTravelDistance,
                new CoreEffectPayload
                {
                    armoredDamageMultiplier = 1f,
                    statusApplications = SpellStatusApplicationUtility.Sanitize(statusApplications ?? Array.Empty<SpellStatusApplication>()),
                });
        }
    }
}
