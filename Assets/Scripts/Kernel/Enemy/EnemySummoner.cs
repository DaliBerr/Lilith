using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 按技能槽配置执行一次召唤技能；冷却与多技能调度由外层统一控制。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySummoner : MonoBehaviour, IEnemySkillCaster
{
    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;
    [SerializeField] private EnemyGenerator enemyGenerator;

    private readonly List<Enemy> aliveSummons = new();
    private VocalithRandom randomSource;

    public EnemySkillKind SkillKind => EnemySkillKind.SummonEnemy;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
        TryResolveEnemyGenerator();
        EnsureRandomSource();
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
        TryResolveEnemyGenerator();
        EnsureRandomSource();
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
    /// summary: 按给定技能槽配置执行一次召唤；具体冷却和多技能并发由调度器统一处理。
    /// param: skillSlot 当前命中的技能槽配置
    /// returns: 成功生成至少一名召唤物时返回 true
    /// </summary>
    public bool TryCastSkill(EnemyDefinition.EnemySkillSlotDefinition skillSlot)
    {
        if (skillSlot.skillKind != SkillKind || !TryResolveEnemyData() || !TryResolveTargetPlayer() || !TryResolveEnemyGenerator())
        {
            return false;
        }

        if (TryResolveStatusEffects() && !statusEffects.CanAct)
        {
            return false;
        }

        if (targetPlayerHealth == null || targetPlayerHealth.IsDead)
        {
            return false;
        }

        float castRange = skillSlot.ResolveCastRange(enemyData.AttackRange);
        if (castRange <= 0f || !IsTargetWithinRange(castRange))
        {
            return false;
        }

        EnemyDefinition.SummonSkillDefinition summonSkill = skillSlot.summonSkill;
        if (summonSkill.summonedEnemyDefinition == null)
        {
            return false;
        }

        PruneDeadSummons();
        int allowedCount = summonSkill.maxAliveSummons > 0
            ? Mathf.Max(0, summonSkill.maxAliveSummons - aliveSummons.Count)
            : 0;
        if (allowedCount <= 0)
        {
            return false;
        }

        EnsureRandomSource();
        int summonCount = Mathf.Min(ResolveSummonCount(summonSkill), allowedCount);
        int spawnedCount = 0;
        EnemyWaveConfig summonConfig = enemyGenerator.ResolveRuntimeConfig(summonSkill.summonedEnemyDefinition).GetSanitized(clearTokenDrops: true);
        for (int i = 0; i < summonCount; i++)
        {
            if (!enemyGenerator.TryGetSpawnPositionAround(transform.position, summonSkill.summonRadius, out Vector3 spawnPosition))
            {
                continue;
            }

            if (!enemyGenerator.TrySpawnEnemyAt(summonSkill.summonedEnemyDefinition, summonConfig, spawnPosition, out Enemy summonedEnemy))
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

        return true;
    }

    /// <summary>
    /// summary: 在技能槽声明的最小和最大召唤数量之间随机解析本次实际要刷出的单位数量。
    /// param: summonSkill 当前技能槽声明的召唤配置
    /// returns: 本次技能应尝试生成的召唤物数量
    /// </summary>
    private int ResolveSummonCount(EnemyDefinition.SummonSkillDefinition summonSkill)
    {
        int minCount = Mathf.Max(1, summonSkill.minSummonCountPerCast);
        int maxCount = Mathf.Max(minCount, summonSkill.maxSummonCountPerCast);
        if (minCount == maxCount)
        {
            return minCount;
        }

        return randomSource.Next(minCount, maxCount + 1);
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

    /// <summary>
    /// summary: 解析当前物体上的敌人数据组件，保证技能执行和基础数据读取使用同一实例。
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
    /// summary: 解析当前敌人根节点上的状态效果控制器，供眩晕阻断技能释放使用。
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

    private void EnsureRandomSource()
    {
        randomSource ??= new VocalithRandom();
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
