using Kernel.MapGrid;
using UnityEngine;
using System;
using System.Collections.Generic;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 按随机时间间隔在玩家周围固定半径生成文字敌人。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyGenerator : MonoBehaviour
{
    private const float MinimumSpawnInterval = 0.05f;
    private const float MinimumSpawnOffsetSqrMagnitude = 0.0001f;
    private const int MinimumGroundSpawnRolls = 1;

    [SerializeField] private CharEnemyMovement charEnemyPrefab;
    [SerializeField] private List<CharEnemyMovement> additionalEnemyPrefabs = new();
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField] private Transform spawnedEnemyParent;
    [SerializeField] private Vector2 spawnIntervalRange = new(1f, 2.5f);
    [SerializeField, Min(0f)] private float spawnDistance = 30f;
    [SerializeField, Min(MinimumGroundSpawnRolls)] private int maxGroundSpawnRolls = 16;
    [SerializeField] private bool runAutonomousLoop = true;

    private float nextSpawnTime;
    private VocalithRandom randomSource;

    private void Awake()
    {
        TryResolveTargetPlayer();
        TryResolveMapGrid();
        SanitizeConfiguration();
        EnsureRandomSource();
    }

    private void OnEnable()
    {
        if (runAutonomousLoop)
        {
            ScheduleNextSpawn(Time.time);
        }
    }

    /// <summary>
    /// summary: 到达下一次刷新时间后生成敌人，并重新抽取下一轮随机间隔。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (!runAutonomousLoop || Time.time < nextSpawnTime)
        {
            return;
        }

        TrySpawnEnemyInternal(out _);
        ScheduleNextSpawn(Time.time);
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
    }

    /// <summary>
    /// summary: 显式设置当前生成器跟随的玩家目标。
    /// param: player 作为刷怪中心的玩家 Transform
    /// returns: 传入目标有效时返回 true
    /// </summary>
    public bool TrySetTarget(Transform player)
    {
        if (player == null)
        {
            return false;
        }

        targetPlayer = player;
        return true;
    }

    /// <summary>
    /// summary: 切换生成器是否运行自身的随机刷怪循环。
    /// param: shouldRun 为 true 时启用自治循环，为 false 时仅允许外部显式触发单次生成
    /// returns: 无
    /// </summary>
    public void SetAutonomousLoop(bool shouldRun)
    {
        runAutonomousLoop = shouldRun;
        if (runAutonomousLoop)
        {
            ScheduleNextSpawn(Time.time);
        }
    }

    /// <summary>
    /// summary: 按当前波次配置显式生成一名敌人，并把覆写数值应用到新实例。
    /// param: config 当前波次给出的敌人数值配置
    /// param: spawnedEnemy 输出的实际生成敌人数据组件
    /// param: prefabOverride 可选的敌人 prefab 覆写；为空时回退到生成器默认 prefab
    /// returns: 成功实例化敌人时返回 true
    /// </summary>
    public bool TrySpawnEnemy(EnemyWaveConfig config, out Enemy spawnedEnemy, CharEnemyMovement prefabOverride = null)
    {
        if (!TrySpawnEnemyInternal(out spawnedEnemy, prefabOverride))
        {
            return false;
        }

        if (spawnedEnemy is IEnemyWaveConfigReceiver receiver)
        {
            receiver.ApplyWaveConfig(config);
        }

        return true;
    }

    /// <summary>
    /// summary: 按敌人名称从生成器的 prefab 目录中解析目标敌人，并应用当前波次配置完成一次生成。
    /// param: enemyName 本次需要生成的敌人名称
    /// param: config 当前波次给出的敌人数值配置
    /// param: spawnedEnemy 输出的实际生成敌人数据组件
    /// returns: 成功找到同名 prefab 并实例化敌人时返回 true
    /// </summary>
    public bool TrySpawnEnemy(string enemyName, EnemyWaveConfig config, out Enemy spawnedEnemy)
    {
        spawnedEnemy = null;
        if (!TryResolveEnemyPrefab(enemyName, out CharEnemyMovement prefabToSpawn))
        {
            return false;
        }

        return TrySpawnEnemy(config, out spawnedEnemy, prefabToSpawn);
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定玩家时，尝试自动找到场景中的玩家平面移动组件。
    /// param: 无
    /// returns: 成功拿到刷怪中心时返回 true
    /// </summary>
    private bool TryResolveTargetPlayer()
    {
        if (targetPlayer != null)
        {
            return true;
        }

        PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
        if (playerMovement == null)
        {
            return false;
        }

        targetPlayer = playerMovement.transform;
        return true;
    }

    /// <summary>
    /// summary: 生成一名新的文字敌人，并把当前玩家目标注入到敌人移动行为脚本。
    /// param: spawnedEnemy 输出的实际生成敌人数据组件
    /// param: prefabOverride 可选的敌人 prefab 覆写；为空时回退到默认 prefab
    /// returns: 成功实例化敌人时返回 true
    /// </summary>
    private bool TrySpawnEnemyInternal(out Enemy spawnedEnemy, CharEnemyMovement prefabOverride = null)
    {
        spawnedEnemy = null;
        CharEnemyMovement prefabToSpawn = prefabOverride != null ? prefabOverride : charEnemyPrefab;
        if (prefabToSpawn == null || !TryResolveTargetPlayer() || !TryGetSpawnPosition(out Vector3 spawnPosition))
        {
            return false;
        }

        Quaternion spawnRotation = GetSpawnRotation(spawnPosition, targetPlayer.position);
        CharEnemyMovement enemyMovement = Instantiate(prefabToSpawn, spawnPosition, spawnRotation, spawnedEnemyParent);
        enemyMovement.TrySetTarget(targetPlayer);
        TryBindEnemyAttackTarget(enemyMovement);
        enemyMovement.TryGetComponent(out spawnedEnemy);
        return spawnedEnemy != null;
    }

    /// <summary>
    /// summary: 按敌人名称在默认 prefab 与附加 prefab 目录中解析目标敌人模板。
    /// param: enemyName 当前波次条目指定的敌人名称
    /// param: prefab 输出的匹配 prefab
    /// returns: 成功找到同名敌人 prefab 时返回 true
    /// </summary>
    private bool TryResolveEnemyPrefab(string enemyName, out CharEnemyMovement prefab)
    {
        prefab = null;
        if (string.IsNullOrWhiteSpace(enemyName))
        {
            return false;
        }

        if (TryMatchEnemyPrefab(charEnemyPrefab, enemyName, out prefab))
        {
            return true;
        }

        if (additionalEnemyPrefabs == null)
        {
            return false;
        }

        for (int i = 0; i < additionalEnemyPrefabs.Count; i++)
        {
            if (TryMatchEnemyPrefab(additionalEnemyPrefabs[i], enemyName, out prefab))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 在玩家周围多次抽样候选点，只返回落在 `Ground` 标记地块上的刷怪位置。
    /// </summary>
    /// <param name="spawnPosition">输出的敌人出生世界坐标。</param>
    /// <returns>找到有效地面刷怪点时返回 true。</returns>
    public bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = default;
        if (!TryResolveTargetPlayer() || !TryResolveMapGrid())
        {
            return false;
        }

        for (int attempt = 0; attempt < maxGroundSpawnRolls; attempt++)
        {
            Vector3 candidatePosition = GetRandomSpawnCandidatePosition();
            if (!IsGroundSpawnPosition(candidatePosition))
            {
                continue;
            }

            spawnPosition = candidatePosition;
            return true;
        }

        return false;
    }

    /// <summary>
    /// summary: 以玩家为圆心，在 XZ 平面上随机抽样一个固定半径的候选刷怪点。
    /// param: 无
    /// returns: 一个尚未经过地面校验的候选世界坐标
    /// </summary>
    private Vector3 GetRandomSpawnCandidatePosition()
    {
        EnsureRandomSource();
        float angleRadians = NextFloat(0f, Mathf.PI * 2f);
        Vector2 randomOffset = new(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        Vector3 playerPosition = targetPlayer.position;
        return playerPosition + new Vector3(randomOffset.x, 0f, randomOffset.y) * spawnDistance;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定地图时，尝试自动找到场景中的 MapGridAuthoring。
    /// param: 无
    /// returns: 成功拿到地图网格时返回 true
    /// </summary>
    private bool TryResolveMapGrid()
    {
        if (targetMapGrid != null)
        {
            return true;
        }

        targetMapGrid = FindFirstObjectByType<MapGridAuthoring>();
        return targetMapGrid != null;
    }

    /// <summary>
    /// summary: 检查候选刷怪点是否落在已索引且 tag 为 Ground 的地块上。
    /// param: candidatePosition 本次候选世界坐标
    /// returns: 命中有效 Ground 地块时返回 true
    /// </summary>
    private bool IsGroundSpawnPosition(Vector3 candidatePosition)
    {
        if (targetMapGrid == null ||
            !targetMapGrid.TryGetCellCoordinateFromWorldPoint(candidatePosition, out Vector2Int coordinates) ||
            !targetMapGrid.TryGetCell(coordinates, out GameObject cellObject) ||
            cellObject == null)
        {
            return false;
        }

        return cellObject.CompareTag(MapGridAuthoring.GroundTagName);
    }

    /// <summary>
    /// summary: 让新生成的敌人出生时先朝向玩家，避免第一帧朝向错误。
    /// param: spawnPosition 敌人出生点
    /// param: playerPosition 玩家当前位置
    /// returns: 敌人出生时应使用的旋转
    /// </summary>
    private static Quaternion GetSpawnRotation(Vector3 spawnPosition, Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= MinimumSpawnOffsetSqrMagnitude)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    /// <summary>
    /// summary: 检查某个敌人 prefab 是否声明了目标敌人名称。
    /// param: candidatePrefab 当前待比较的敌人 prefab
    /// param: enemyName 当前波次条目指定的敌人名称
    /// param: matchedPrefab 当名称匹配时输出该 prefab
    /// returns: prefab 上存在 Enemy 组件且名称匹配时返回 true
    /// </summary>
    private static bool TryMatchEnemyPrefab(CharEnemyMovement candidatePrefab, string enemyName, out CharEnemyMovement matchedPrefab)
    {
        matchedPrefab = null;
        if (candidatePrefab == null || !candidatePrefab.TryGetComponent(out Enemy enemy))
        {
            return false;
        }

        if (!string.Equals(enemy.EnemyName, enemyName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        matchedPrefab = candidatePrefab;
        return true;
    }

    /// <summary>
    /// summary: 如果敌人身上挂有近战攻击组件，则把当前玩家目标同步注入进去。
    /// param: enemyMovement 刚刚实例化完成的敌人移动组件
    /// returns: 无
    /// </summary>
    private void TryBindEnemyAttackTarget(CharEnemyMovement enemyMovement)
    {
        if (enemyMovement == null || targetPlayer == null || !enemyMovement.TryGetComponent(out EnemyMeleeAttacker meleeAttacker))
        {
            return;
        }

        meleeAttacker.TrySetTarget(targetPlayer);
    }

    /// <summary>
    /// summary: 抽取下一次刷怪时间，保证间隔始终落在配置范围内。
    /// param: currentTime 当前时间戳
    /// returns: 无
    /// </summary>
    private void ScheduleNextSpawn(float currentTime)
    {
        EnsureRandomSource();
        nextSpawnTime = currentTime + NextFloat(spawnIntervalRange.x, spawnIntervalRange.y);
    }

    /// <summary>
    /// summary: 修正刷怪间隔和刷怪距离，避免 Inspector 输入无效值。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        if (additionalEnemyPrefabs == null)
        {
            additionalEnemyPrefabs = new List<CharEnemyMovement>();
        }

        spawnIntervalRange.x = Mathf.Max(MinimumSpawnInterval, spawnIntervalRange.x);
        spawnIntervalRange.y = Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y);
        spawnDistance = Mathf.Max(0f, spawnDistance);
        maxGroundSpawnRolls = Mathf.Max(MinimumGroundSpawnRolls, maxGroundSpawnRolls);
    }

    /// <summary>
    /// summary: 确保当前生成器拥有可用的 Vocalith 随机数源。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureRandomSource()
    {
        if (randomSource == null)
        {
            randomSource = new VocalithRandom();
        }
    }

    /// <summary>
    /// summary: 使用 Vocalith 随机数源在指定浮点区间内采样一个值。
    /// param: minInclusive 采样下界
    /// param: maxInclusive 采样上界
    /// returns: 位于给定区间内的随机浮点值
    /// </summary>
    private float NextFloat(float minInclusive, float maxInclusive)
    {
        if (maxInclusive <= minInclusive)
        {
            return minInclusive;
        }

        return minInclusive + (maxInclusive - minInclusive) * randomSource.NextFloat01();
    }
}
