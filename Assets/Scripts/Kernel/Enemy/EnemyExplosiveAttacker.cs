using UnityEngine;

/// <summary>
/// 让敌人在接近玩家后开始蓄力，并在倒计时结束时自爆。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyExplosiveAttacker : MonoBehaviour
{
    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;

    private bool isChargingExplosion;
    private float scheduledExplosionTime;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        if (TryResolveStatusEffects() && statusEffects.IsStunned)
        {
            if (isChargingExplosion)
            {
                scheduledExplosionTime += Time.deltaTime;
            }

            return;
        }

        TryTickExplosion(Time.time);
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
    }

    /// <summary>
    /// summary: 显式设置自爆逻辑使用的玩家目标，并同步解析生命组件。
    /// param: player 当前敌人应追击和判定爆炸伤害的玩家 Transform
    /// returns: 成功绑定到有效玩家目标时返回 true
    /// </summary>
    public bool TrySetTarget(Transform player)
    {
        if (player == null || IsOwnTransform(player))
        {
            return false;
        }

        targetPlayer = player;
        targetPlayerHealth = ResolvePlayerHealth(player);
        return targetPlayerHealth != null;
    }

    /// <summary>
    /// summary: 推进当前自爆状态机；未蓄力时检查触发距离，蓄力完成后结算一次自爆。
    /// param: currentTime 当前逻辑时钟
    /// returns: 当前帧实际完成一次自爆时返回 true
    /// </summary>
    private bool TryTickExplosion(float currentTime)
    {
        if (TryResolveStatusEffects() && statusEffects.IsStunned)
        {
            return false;
        }

        if (!TryResolveEnemyData())
        {
            return false;
        }

        if (!isChargingExplosion)
        {
            if (!TryResolveTargetPlayer() || targetPlayerHealth == null || targetPlayerHealth.IsDead)
            {
                return false;
            }

            float triggerRange = enemyData.AttackRange;
            if (triggerRange <= 0f || enemyData.AttackDamage <= 0f || !IsTargetWithinRange(triggerRange))
            {
                return false;
            }

            StartExplosionCharge(currentTime);
            return false;
        }

        if (currentTime < scheduledExplosionTime)
        {
            return false;
        }

        return TryExplode();
    }

    /// <summary>
    /// summary: 基于当前定义里的蓄力前摇开始一次自爆倒计时。
    /// param: currentTime 当前逻辑时钟
    /// returns: 无
    /// </summary>
    private void StartExplosionCharge(float currentTime)
    {
        EnemyDefinition.ExplosiveAttackDefinition explosiveAttack = ResolveExplosiveAttackDefinition();
        isChargingExplosion = true;
        scheduledExplosionTime = currentTime + explosiveAttack.windupSeconds;
    }

    /// <summary>
    /// summary: 在当前敌人位置结算一次爆炸伤害，并在结算后让敌人自毁。
    /// param: 无
    /// returns: 当前敌人成功进入自毁流程时返回 true
    /// </summary>
    private bool TryExplode()
    {
        isChargingExplosion = false;
        EnemyDefinition.ExplosiveAttackDefinition explosiveAttack = ResolveExplosiveAttackDefinition();
        if (explosiveAttack.explosionRadius > 0f &&
            TryResolveTargetPlayer() &&
            targetPlayerHealth != null &&
            !targetPlayerHealth.IsDead &&
            IsTargetWithinRange(explosiveAttack.explosionRadius))
        {
            targetPlayerHealth.TryApplyDamage(enemyData.AttackDamage, out _, out _);
        }

        return enemyData.TryApplyDamage(float.MaxValue, out _, out _);
    }

    /// <summary>
    /// summary: 判断当前玩家是否仍处于指定的平面范围内。
    /// param: range 当前要检查的平面半径
    /// returns: 平面距离不大于给定半径时返回 true
    /// </summary>
    private bool IsTargetWithinRange(float range)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }

    /// <summary>
    /// summary: 解析当前物体上的敌人数据组件，保证自爆逻辑读取的是根节点上的同一份数据。
    /// param: 无
    /// returns: 成功拿到 Enemy 数据组件时返回 true
    /// </summary>
    private bool TryResolveEnemyData()
    {
        if (enemyData != null && enemyData.transform == transform)
        {
            return true;
        }

        enemyData = null;
        return TryGetComponent(out enemyData);
    }

    /// <summary>
    /// summary: 解析当前敌人根节点上的状态效果控制器，供眩晕阻断自爆蓄力使用。
    /// param: 无
    /// returns: 成功拿到状态控制器时返回 true
    /// </summary>
    private bool TryResolveStatusEffects()
    {
        if (statusEffects != null && statusEffects.transform == transform)
        {
            return true;
        }

        statusEffects = null;
        return TryGetComponent(out statusEffects);
    }

    /// <summary>
    /// summary: 优先使用显式绑定的玩家目标，否则自动查找当前场景中的玩家与生命组件。
    /// param: 无
    /// returns: 成功拿到有效玩家目标和生命组件时返回 true
    /// </summary>
    private bool TryResolveTargetPlayer()
    {
        if (targetPlayer != null && !IsOwnTransform(targetPlayer))
        {
            targetPlayerHealth = ResolvePlayerHealth(targetPlayer);
            return targetPlayerHealth != null;
        }

        PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
        if (playerMovement == null)
        {
            return false;
        }

        targetPlayer = playerMovement.transform;
        targetPlayerHealth = ResolvePlayerHealth(targetPlayer);
        return targetPlayerHealth != null;
    }

    /// <summary>
    /// summary: 解析当前定义上的自爆配置；若定义暂未绑定，则回退到一组可运行的默认值。
    /// param: 无
    /// returns: 当前敌人的自爆攻击配置
    /// </summary>
    private EnemyDefinition.ExplosiveAttackDefinition ResolveExplosiveAttackDefinition()
    {
        return enemyData != null && enemyData.Definition != null
            ? enemyData.Definition.ExplosiveAttack
            : new EnemyDefinition.ExplosiveAttackDefinition
            {
                explosionRadius = enemyData != null ? enemyData.AttackRange : 0f,
                windupSeconds = 0.8f,
            };
    }

    /// <summary>
    /// summary: 解析指定玩家 Transform 上的生命组件，兼容组件直接挂在根节点或父节点。
    /// param: player 当前玩家根节点
    /// returns: 成功找到玩家生命组件时返回对应引用
    /// </summary>
    private static PlayerHealth ResolvePlayerHealth(Transform player)
    {
        if (player == null)
        {
            return null;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth;
        }

        return player.GetComponentInParent<PlayerHealth>();
    }

    /// <summary>
    /// summary: 判断一个 Transform 是否属于当前敌人自身或其子节点，避免把自己错误识别为玩家。
    /// param: candidate 需要判断的 Transform
    /// returns: 属于当前敌人层级时返回 true
    /// </summary>
    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
