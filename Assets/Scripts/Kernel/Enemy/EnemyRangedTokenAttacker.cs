using Kernel.Bullet;
using UnityEngine;
using Vocalith.Logging;

/// <summary>
/// 让敌人在攻击距离内按冷却编译并发射配置好的词元子弹。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyRangedTokenAttacker : MonoBehaviour
{
    private const float MinimumAimDirectionSqrMagnitude = 0.0001f;

    [SerializeField] private Enemy enemyData;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;
    [SerializeField] private Transform bulletSpawnOrigin;
    [SerializeField] private Vector3 bulletSpawnLocalOffset = new(0f, 0f, 18f);

    private float nextAttackTime;
    private EnemyDefinition compiledDefinition;
    private CompiledAttack compiledAttackCache;
    private bool hasLoggedCompileFailure;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
        InvalidateCompiledAttack();
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        TryPerformAttack(Time.time);
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
        InvalidateCompiledAttack();
    }

    /// <summary>
    /// summary: 显式设置远程攻击使用的玩家目标，并同步解析生命组件。
    /// param: player 当前敌人应瞄准的玩家 Transform
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
    /// summary: 在满足攻击距离、冷却与编译条件时发射配置好的词元子弹。
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功发射至少一发子弹时返回 true
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
        if (attackRange <= 0f || !IsTargetWithinRange(attackRange))
        {
            return false;
        }

        EnemyDefinition definition = enemyData.Definition;
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack = definition != null ? definition.RangedBulletAttack : default;
        if (rangedAttack.bulletPrefab == null || !TryGetCompiledAttack(definition, rangedAttack, out CompiledAttack compiledAttack))
        {
            return false;
        }

        if (!TryGetShotDirection(out Vector3 spawnPosition, out Vector3 shotDirection))
        {
            return false;
        }

        if (AttackProjectileEmitter.Emit(
                rangedAttack.bulletPrefab,
                transform,
                spawnPosition,
                shotDirection,
                compiledAttack,
                rangedAttack.targetPolicy) <= 0)
        {
            return false;
        }

        nextAttackTime = currentTime + Mathf.Max(0f, enemyData.AttackCooldown);
        return true;
    }

    /// <summary>
    /// summary: 读取定义资产里的词元序列并缓存对应的编译结果。
    /// param: definition 当前敌人绑定的定义资产
    /// param: rangedAttack 当前定义里的远程攻击配置
    /// param: compiledAttack 输出的可复用编译结果
    /// returns: 成功拿到可发射的编译结果时返回 true
    /// </summary>
    private bool TryGetCompiledAttack(
        EnemyDefinition definition,
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack,
        out CompiledAttack compiledAttack)
    {
        compiledAttack = null;
        if (definition == null || rangedAttack.formulaItems == null || rangedAttack.formulaItems.Count <= 0)
        {
            return false;
        }

        if (compiledAttackCache == null || compiledDefinition != definition)
        {
            compiledDefinition = definition;
            compiledAttackCache = AttackFormulaCompiler.Compile(rangedAttack.formulaItems);
            hasLoggedCompileFailure = false;
        }

        compiledAttack = compiledAttackCache;
        if (compiledAttack != null && compiledAttack.CanFire)
        {
            hasLoggedCompileFailure = false;
            return true;
        }

        if (!hasLoggedCompileFailure)
        {
            hasLoggedCompileFailure = true;
            GameDebug.LogWarning($"[EnemyRangedTokenAttacker] Enemy '{name}' failed to compile its ranged token formula.");
        }

        return false;
    }

    /// <summary>
    /// summary: 计算当前远程攻击的发射点与朝向。
    /// param: spawnPosition 输出的子弹出生点
    /// param: shotDirection 输出的平面发射方向
    /// returns: 成功得到有效平面方向时返回 true
    /// </summary>
    private bool TryGetShotDirection(out Vector3 spawnPosition, out Vector3 shotDirection)
    {
        spawnPosition = GetBulletSpawnPosition();
        shotDirection = Vector3.zero;
        Vector3 targetOffset = targetPlayer.position - spawnPosition;
        targetOffset.y = 0f;
        if (targetOffset.sqrMagnitude <= MinimumAimDirectionSqrMagnitude)
        {
            return false;
        }

        shotDirection = targetOffset.normalized;
        return true;
    }

    /// <summary>
    /// summary: 计算当前远程攻击实际使用的世界发射点。
    /// param: 无
    /// returns: 子弹出生世界坐标
    /// </summary>
    private Vector3 GetBulletSpawnPosition()
    {
        Transform spawnRoot = bulletSpawnOrigin != null ? bulletSpawnOrigin : transform;
        return spawnRoot.TransformPoint(bulletSpawnLocalOffset);
    }

    /// <summary>
    /// summary: 判断当前玩家是否仍处于敌人的远程攻击距离内。
    /// param: attackRange 当前敌人声明的攻击距离
    /// returns: 平面距离不大于攻击距离时返回 true
    /// </summary>
    private bool IsTargetWithinRange(float attackRange)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= attackRange * attackRange;
    }

    private void InvalidateCompiledAttack()
    {
        compiledDefinition = null;
        compiledAttackCache = null;
        hasLoggedCompileFailure = false;
    }

    private bool TryResolveEnemyData()
    {
        if (enemyData != null && enemyData.transform == transform)
        {
            return true;
        }

        enemyData = null;
        return TryGetComponent(out enemyData);
    }

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

    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
