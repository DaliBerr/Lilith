using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 负责把编译后的攻击结果落地成一批实际发射的文字子弹。
    /// </summary>
    public static class AttackProjectileEmitter
    {
        /// <summary>
        /// summary: 根据编译结果生成一批实际子弹；支持直射和散射两种行为。
        /// param: bulletPrefab 需要实例化的子弹 prefab
        /// param: owner 发射者根节点
        /// param: spawnPosition 子弹出生点
        /// param: baseDirection 主要发射方向
        /// param: compiledAttack 本次发射使用的编译结果
        /// returns: 实际成功生成的子弹数量
        /// </summary>
        public static int Emit(CharBullet bulletPrefab, Transform owner, Vector3 spawnPosition, Vector3 baseDirection, CompiledAttack compiledAttack)
        {
            if (bulletPrefab == null || owner == null || compiledAttack == null || !compiledAttack.CanFire)
            {
                return 0;
            }

            Vector3 normalizedDirection = baseDirection.normalized;
            if (normalizedDirection.sqrMagnitude <= 0f)
            {
                return 0;
            }

            int emittedCount = 0;
            int projectileCount = compiledAttack.GetProjectileCount();
            if (compiledAttack.BehaviorType == AttackBehaviorType.Spread && projectileCount > 1)
            {
                float totalAngle = compiledAttack.SpreadAngleStep * (projectileCount - 1);
                float startAngle = -totalAngle * 0.5f;
                for (int i = 0; i < projectileCount; i++)
                {
                    float currentAngle = startAngle + (compiledAttack.SpreadAngleStep * i);
                    Vector3 shotDirection = Quaternion.AngleAxis(currentAngle, Vector3.up) * normalizedDirection;
                    emittedCount += EmitSingle(bulletPrefab, owner, spawnPosition, shotDirection, compiledAttack);
                }

                return emittedCount;
            }

            return EmitSingle(bulletPrefab, owner, spawnPosition, normalizedDirection, compiledAttack);
        }

        private static int EmitSingle(CharBullet bulletPrefab, Transform owner, Vector3 spawnPosition, Vector3 shotDirection, CompiledAttack compiledAttack)
        {
            CharBullet bulletInstance = Object.Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
            bulletInstance.InitializeShot(owner, spawnPosition, shotDirection, compiledAttack.AttackSpec, compiledAttack);
            return 1;
        }
    }
}
