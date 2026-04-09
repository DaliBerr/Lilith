using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 让敌人在攻击距离内按冷却召唤配置好的敌人。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySummoner : MonoBehaviour
{
    [SerializeField] private Enemy enemyData;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;
    [SerializeField] private EnemyGenerator enemyGenerator;

    private readonly List<Enemy> aliveSummons = new();
    private float nextSummonTime;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
        TryResolveEnemyGenerator();
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        TryPerformSummon(Time.time);
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveTargetPlayer();
        TryResolveEnemyGenerator();
    }

    /// <summary>
    /// summary: 显式设置召唤逻辑使用的玩家目标，并同步解析生命组件。
    /// param: player 当前敌人应感知的玩家 Transform
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
    /// summary: 显式设置召唤使用的敌人生成器。
    /// param: generator 当前召唤逻辑应复用的 EnemyGenerator
    /// returns: 传入生成器有效时返回 true
    /// </summary>
    public bool TrySetEnemyGenerator(EnemyGenerator generator)
    {
        if (generator == null)
        {
            return false;
        }

        enemyGenerator = generator;
        return true;
    }

    /// <summary>
    /// summary: 在满足攻击距离、冷却与召唤上限时，于敌人身边生成配置好的召唤物。
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功生成至少一名召唤物时返回 true
    /// </summary>
    private bool TryPerformSummon(float currentTime)
    {
        if (currentTime < nextSummonTime || !TryResolveEnemyData() || !TryResolveTargetPlayer() || !TryResolveEnemyGenerator())
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
        EnemyDefinition.SummonAttackDefinition summonAttack = definition != null ? definition.SummonAttack : default;
        if (summonAttack.summonedEnemyDefinition == null)
        {
            return false;
        }

        PruneDeadSummons();
        int allowedCount = summonAttack.maxAliveSummons > 0
            ? Mathf.Max(0, summonAttack.maxAliveSummons - aliveSummons.Count)
            : 0;
        if (allowedCount <= 0)
        {
            return false;
        }

        int summonCount = Mathf.Min(summonAttack.summonCountPerCast, allowedCount);
        int spawnedCount = 0;
        EnemyWaveConfig summonConfig = summonAttack.summonedEnemyConfig.GetSanitized(clearTokenDrops: true);
        for (int i = 0; i < summonCount; i++)
        {
            if (!enemyGenerator.TryGetSpawnPositionAround(transform.position, summonAttack.summonRadius, out Vector3 spawnPosition))
            {
                continue;
            }

            if (!enemyGenerator.TrySpawnEnemyAt(summonAttack.summonedEnemyDefinition, summonConfig, spawnPosition, out Enemy summonedEnemy))
            {
                continue;
            }

            aliveSummons.Add(summonedEnemy);
            spawnedCount++;
        }

        if (spawnedCount <= 0)
        {
            return false;
        }

        nextSummonTime = currentTime + Mathf.Max(0f, enemyData.AttackCooldown);
        return true;
    }

    /// <summary>
    /// summary: 清理已经死亡或被销毁的召唤物引用，确保上限判断只统计有效单位。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void PruneDeadSummons()
    {
        for (int i = aliveSummons.Count - 1; i >= 0; i--)
        {
            Enemy summon = aliveSummons[i];
            if (summon == null || summon.IsDead)
            {
                aliveSummons.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// summary: 判断当前玩家是否处于敌人的召唤触发距离内。
    /// param: attackRange 当前敌人声明的攻击距离
    /// returns: 平面距离不大于攻击距离时返回 true
    /// </summary>
    private bool IsTargetWithinRange(float attackRange)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= attackRange * attackRange;
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

    private bool TryResolveEnemyGenerator()
    {
        if (enemyGenerator != null)
        {
            return true;
        }

        enemyGenerator = FindFirstObjectByType<EnemyGenerator>();
        return enemyGenerator != null;
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
