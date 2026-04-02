using Kernel.MapGrid;
using UnityEngine;

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
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField] private Transform spawnedEnemyParent;
    [SerializeField] private Vector2 spawnIntervalRange = new(1f, 2.5f);
    [SerializeField, Min(0f)] private float spawnDistance = 30f;
    [SerializeField, Min(MinimumGroundSpawnRolls)] private int maxGroundSpawnRolls = 16;

    private float nextSpawnTime;

    private void Awake()
    {
        TryResolveTargetPlayer();
        TryResolveMapGrid();
        SanitizeConfiguration();
    }

    private void OnEnable()
    {
        ScheduleNextSpawn(Time.time);
    }

    /// <summary>
    /// summary: 到达下一次刷新时间后生成敌人，并重新抽取下一轮随机间隔。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (Time.time < nextSpawnTime)
        {
            return;
        }

        TrySpawnEnemy();
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
    /// summary: 生成一名新的文字敌人，并把当前玩家目标注入到敌人行为脚本。
    /// param: 无
    /// returns: 成功实例化敌人时返回 true
    /// </summary>
    private bool TrySpawnEnemy()
    {
        if (charEnemyPrefab == null || !TryGetSpawnPosition(out Vector3 spawnPosition))
        {
            return false;
        }

        Quaternion spawnRotation = GetSpawnRotation(spawnPosition, targetPlayer.position);
        CharEnemyMovement enemyMovement = Instantiate(charEnemyPrefab, spawnPosition, spawnRotation, spawnedEnemyParent);
        enemyMovement.TrySetTarget(targetPlayer);
        return true;
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
        Vector2 randomOffset = Random.insideUnitCircle;
        if (randomOffset.sqrMagnitude <= MinimumSpawnOffsetSqrMagnitude)
        {
            randomOffset = Vector2.up;
        }

        randomOffset.Normalize();
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
    /// summary: 抽取下一次刷怪时间，保证间隔始终落在配置范围内。
    /// param: currentTime 当前时间戳
    /// returns: 无
    /// </summary>
    private void ScheduleNextSpawn(float currentTime)
    {
        nextSpawnTime = currentTime + Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
    }

    /// <summary>
    /// summary: 修正刷怪间隔和刷怪距离，避免 Inspector 输入无效值。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        spawnIntervalRange.x = Mathf.Max(MinimumSpawnInterval, spawnIntervalRange.x);
        spawnIntervalRange.y = Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y);
        spawnDistance = Mathf.Max(0f, spawnDistance);
        maxGroundSpawnRolls = Mathf.Max(MinimumGroundSpawnRolls, maxGroundSpawnRolls);
    }
}
