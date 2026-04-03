using UnityEngine;

/// <summary>
/// 让敌人在进入攻击距离后按冷却对玩家造成近战伤害。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyMeleeAttacker : MonoBehaviour
{
    [SerializeField] private Enemy enemyData;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;

    private float nextAttackTime;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
    }

    /// <summary>
    /// summary: 每帧检查当前敌人是否已经进入攻击距离，并在冷却结束时结算一次近战伤害。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        TryPerformAttack(Time.time);
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
    }

    /// <summary>
    /// summary: 显式设置近战攻击使用的玩家目标，并同步解析玩家生命组件。
    /// param: player 当前敌人应攻击的玩家 Transform
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
    /// summary: 在满足攻击距离、攻击冷却和玩家血量条件时，对玩家结算一次伤害。
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功对玩家造成伤害时返回 true
    /// </summary>
    private bool TryPerformAttack(float currentTime)
    {
        if (currentTime < nextAttackTime || !TryResolveEnemyData() || !TryResolveTargetPlayer())
        {
            return false;
        }

        if (targetPlayerHealth == null || targetPlayerHealth.IsDead)
        {
            return false;
        }

        float attackRange = enemyData.AttackRange;
        float attackDamage = enemyData.AttackDamage;
        if (attackRange <= 0f || attackDamage <= 0f || !IsTargetWithinRange(attackRange))
        {
            return false;
        }

        if (!targetPlayerHealth.TryApplyDamage(attackDamage, out _, out _))
        {
            return false;
        }

        nextAttackTime = currentTime + Mathf.Max(0f, enemyData.AttackCooldown);
        return true;
    }

    /// <summary>
    /// summary: 判断当前目标玩家是否处于敌人的平面近战距离内。
    /// param: attackRange 当前敌人声明的攻击距离
    /// returns: 平面距离不大于攻击距离时返回 true
    /// </summary>
    private bool IsTargetWithinRange(float attackRange)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= attackRange * attackRange;
    }

    /// <summary>
    /// summary: 解析当前物体上的敌人数据组件，保证攻击逻辑和移动逻辑读取同一份数据。
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
