using System.Collections.Generic;
using Kernel;
using Kernel.MapGrid;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 负责在地图地面上实例化敌人定义对应的运行时 prefab。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyGenerator : MonoBehaviour
{
    private const float MinimumSpawnOffsetSqrMagnitude = 0.0001f;
    private const int MinimumGroundSpawnRolls = 1;

    [SerializeField] private Transform targetPlayer;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField] private Transform spawnedEnemyParent;
    [SerializeField] private Transform pickupParent;
    [SerializeField, Min(0f)] private float spawnDistance = 30f;
    [SerializeField, Min(MinimumGroundSpawnRolls)] private int maxGroundSpawnRolls = 16;
    [SerializeField, Min(0)] private int completedWaveCount;

    private VocalithRandom randomSource;

    public float SpawnDistance => spawnDistance;
    public int CompletedWaveCount => completedWaveCount;

    private void Awake()
    {
        TryResolveTargetPlayer();
        TryResolveMapGrid();
        SanitizeConfiguration();
        EnsureRandomSource();
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
    /// summary: 显式设置当前生成器所使用的地图网格，避免多地图场景下依赖模糊的自动查找。
    /// param: mapGrid 当前单局战斗应使用的地图网格
    /// returns: 传入网格有效时返回 true
    /// </summary>
    public bool TrySetTargetMapGrid(MapGridAuthoring mapGrid)
    {
        if (mapGrid == null)
        {
            return false;
        }

        targetMapGrid = mapGrid;
        return true;
    }

    /// <summary>
    /// summary: 按敌人定义和当前波次配置显式生成一名敌人，并把覆写数值应用到新实例。
    /// param: definition 当前波次指定的敌人定义
    /// param: config 当前波次给出的敌人数值配置
    /// param: spawnedEnemy 输出的实际生成敌人数据组件
    /// returns: 成功实例化敌人时返回 true
    /// </summary>
    public bool TrySpawnEnemy(EnemyDefinition definition, EnemyWaveConfig config, out Enemy spawnedEnemy)
    {
        spawnedEnemy = null;
        if (definition == null || definition.RuntimePrefabBinder == null)
        {
            return false;
        }

        if (!TryResolveTargetPlayer() || !TryGetSpawnPosition(out Vector3 spawnPosition))
        {
            return false;
        }

        return TrySpawnEnemyAt(definition, config, spawnPosition, out spawnedEnemy);
    }

    /// <summary>
    /// summary: 把当前战斗里已清空的波次数同步给生成器，供后续刷怪和召唤统一解算成长层级。
    /// param: waveCount 当前战斗里已经完成的波次数量
    /// returns: 无论传入值是否合法都会完成归一化写入，因此恒为 true
    /// </summary>
    public bool TrySetCompletedWaveCount(int waveCount)
    {
        completedWaveCount = Mathf.Max(0, waveCount);
        return true;
    }

    /// <summary>
    /// summary: 基于当前成长层级和定义资产，解算一份可直接应用到运行时实例的敌人数值。
    /// param: definition 当前要生成的敌人定义
    /// param: tokenDrops 当前刷怪条目额外声明的掉落表
    /// returns: 当前生成器层级下应使用的运行时敌人数值
    /// </summary>
    public EnemyWaveConfig ResolveRuntimeConfig(EnemyDefinition definition, IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops = null)
    {
        return definition != null
            ? definition.ResolveRuntimeConfig(completedWaveCount, tokenDrops)
            : new EnemyWaveConfig(1f, 0f, 0f, 0f, 0f);
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

        return TryGetSpawnPositionOnGround(targetPlayer.position, spawnDistance, useFullRadius: false, out spawnPosition);
    }

    /// <summary>
    /// summary: 在指定中心点附近多次抽样候选点，并只返回落在 Ground 地块上的刷怪位置。
    /// param: centerPosition 本次抽样使用的世界中心点
    /// param: radius 本次抽样允许使用的最大半径
    /// param: spawnPosition 输出的有效敌人出生世界坐标
    /// returns: 找到有效地面刷怪点时返回 true
    /// </summary>
    public bool TryGetSpawnPositionAround(Vector3 centerPosition, float radius, out Vector3 spawnPosition)
    {
        return TryGetSpawnPositionOnGround(centerPosition, radius, useFullRadius: true, out spawnPosition);
    }

    /// <summary>
    /// summary: 按显式世界坐标实例化一名敌人，并沿用与常规刷怪相同的定义绑定与目标注入流程。
    /// param: definition 当前要实例化的敌人定义
    /// param: config 当前实例应接收的敌人数值配置
    /// param: spawnPosition 当前实例的世界出生点
    /// param: spawnedEnemy 输出的 Enemy 运行时组件
    /// returns: 成功实例化敌人时返回 true
    /// </summary>
    public bool TrySpawnEnemyAt(EnemyDefinition definition, EnemyWaveConfig config, Vector3 spawnPosition, out Enemy spawnedEnemy)
    {
        spawnedEnemy = null;
        if (definition == null || definition.RuntimePrefabBinder == null || !TryResolveTargetPlayer() || !TryResolveMapGrid())
        {
            return false;
        }

        Vector3 resolvedSpawnPosition = spawnPosition;
        resolvedSpawnPosition.y = targetMapGrid.WorldPlaneY;
        Quaternion spawnRotation = GetSpawnRotation(resolvedSpawnPosition, targetPlayer.position);
        EnemyDefinitionBinder spawnedBinder = Instantiate(definition.RuntimePrefabBinder, resolvedSpawnPosition, spawnRotation, spawnedEnemyParent);
        if (!TryInitializeSpawnedEnemy(spawnedBinder, definition, config, out spawnedEnemy))
        {
            DestroySpawnedObject(spawnedBinder != null ? spawnedBinder.gameObject : null);
            spawnedEnemy = null;
            return false;
        }

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
    /// summary: 以玩家为圆心，在 XZ 平面上随机抽样一个固定半径的候选刷怪点。
    /// param: 无
    /// returns: 一个尚未经过地面校验的候选世界坐标
    /// </summary>
    private Vector3 GetRandomSpawnCandidatePosition(Vector3 centerPosition, float radius, bool useFullRadius)
    {
        EnsureRandomSource();
        float angleRadians = NextFloat(0f, Mathf.PI * 2f);
        Vector2 randomOffset = new(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        float resolvedRadius = Mathf.Max(0f, radius);
        float offsetScale = useFullRadius ? Mathf.Sqrt(NextFloat(0f, 1f)) * resolvedRadius : resolvedRadius;
        float planeY = targetMapGrid.WorldPlaneY;
        return new Vector3(
            centerPosition.x + (randomOffset.x * offsetScale),
            planeY,
            centerPosition.z + (randomOffset.y * offsetScale));
    }

    /// <summary>
    /// summary: 把实例化出的敌人根节点绑定到定义、波次配置和当前玩家目标上。
    /// param: spawnedRoot 刚刚实例化完成的敌人根对象
    /// param: definition 当前实例应使用的敌人定义
    /// param: config 当前波次给出的敌人数值配置
    /// param: spawnedEnemy 输出的 Enemy 运行时组件
    /// returns: 成功完成定义绑定与初始化时返回 true
    /// </summary>
    private bool TryInitializeSpawnedEnemy(EnemyDefinitionBinder spawnedBinder, EnemyDefinition definition, EnemyWaveConfig config, out Enemy spawnedEnemy)
    {
        spawnedEnemy = null;
        if (spawnedBinder == null || definition == null)
        {
            return false;
        }

        if (!spawnedBinder.ApplyDefinition(definition))
        {
            return false;
        }

        GameObject spawnedRoot = spawnedBinder.gameObject;
        spawnedEnemy = spawnedRoot.GetComponent<Enemy>();
        if (spawnedEnemy == null)
        {
            return false;
        }

        TrySnapSpawnedEnemyToGroundPlane(spawnedRoot.transform);
        TryBindEnemyTargets(spawnedRoot);
        ApplyWaveConfigToReceivers(spawnedEnemy, config);
        return true;
    }

    /// <summary>
    /// summary: 把同一份波次配置广播给生成对象根节点上的全部接收者，避免数值与掉落逻辑拆成多个组件后漏配。
    /// param: spawnedEnemy 本次已经成功实例化出的敌人
    /// param: config 当前波次指定的敌人配置
    /// returns: 无
    /// </summary>
    private static void ApplyWaveConfigToReceivers(Enemy spawnedEnemy, EnemyWaveConfig config)
    {
        if (spawnedEnemy == null)
        {
            return;
        }

        IEnemyWaveConfigReceiver[] receivers = spawnedEnemy.GetComponents<IEnemyWaveConfigReceiver>();
        for (int i = 0; i < receivers.Length; i++)
        {
            receivers[i].ApplyWaveConfig(config);
        }
    }

    /// <summary>
    /// summary: 如果敌人根节点上挂有追踪或近战组件，则把当前玩家目标同步注入进去。
    /// param: spawnedRoot 刚刚实例化完成的敌人根对象
    /// returns: 无
    /// </summary>
    private void TryBindEnemyTargets(GameObject spawnedRoot)
    {
        if (spawnedRoot == null || targetPlayer == null)
        {
            return;
        }

        CharEnemyMovement enemyMovement = spawnedRoot.GetComponent<CharEnemyMovement>();
        if (enemyMovement != null)
        {
            enemyMovement.TrySetTarget(targetPlayer);
            enemyMovement.TrySetTargetMapGrid(targetMapGrid);
        }

        EnemyMeleeAttacker meleeAttacker = spawnedRoot.GetComponent<EnemyMeleeAttacker>();
        if (meleeAttacker != null)
        {
            meleeAttacker.TrySetTarget(targetPlayer);
        }

        EnemyRangedTokenAttacker rangedTokenAttacker = spawnedRoot.GetComponent<EnemyRangedTokenAttacker>();
        if (rangedTokenAttacker != null)
        {
            rangedTokenAttacker.TrySetTarget(targetPlayer);
        }

        EnemyExplosiveAttacker explosiveAttacker = spawnedRoot.GetComponent<EnemyExplosiveAttacker>();
        if (explosiveAttacker != null)
        {
            explosiveAttacker.TrySetTarget(targetPlayer);
        }

        EnemySummoner summoner = spawnedRoot.GetComponent<EnemySummoner>();
        if (summoner != null)
        {
            summoner.TrySetTarget(targetPlayer);
            summoner.TrySetEnemyGenerator(this);
        }

        EnemyBulletTokenDropper tokenDropper = spawnedRoot.GetComponent<EnemyBulletTokenDropper>();
        if (tokenDropper != null)
        {
            tokenDropper.TrySetTargetMapGrid(targetMapGrid);
            tokenDropper.TrySetPickupParent(pickupParent != null ? pickupParent : spawnedEnemyParent);
        }
    }

    /// <summary>
    /// summary: 在实例化后立刻把 grounded 敌人根节点抬到地图平面上，避免旧 prefab 高度约定继续沿用到运行时。
    /// param: enemyRoot 刚刚实例化完成的敌人根节点
    /// returns: 无
    /// </summary>
    private void TrySnapSpawnedEnemyToGroundPlane(Transform enemyRoot)
    {
        if (enemyRoot == null || !TryResolveMapGrid())
        {
            return;
        }

        if (!WorldHeightUtility.TryFindGroundingReferenceCollider(enemyRoot, out Collider referenceCollider))
        {
            if (WorldHeightUtility.TrySnapTransformToPlaneHeight(enemyRoot, targetMapGrid.WorldPlaneY))
            {
                SyncSpawnedEnemyRigidbody(enemyRoot);
            }

            return;
        }

        if (WorldHeightUtility.TrySnapGroundedRoot(enemyRoot, referenceCollider, targetMapGrid.WorldPlaneY))
        {
            SyncSpawnedEnemyRigidbody(enemyRoot);
        }
    }

    private static void SyncSpawnedEnemyRigidbody(Transform enemyRoot)
    {
        if (enemyRoot == null || !enemyRoot.TryGetComponent(out Rigidbody targetRigidbody))
        {
            return;
        }

        targetRigidbody.position = enemyRoot.position;
        targetRigidbody.linearVelocity = Vector3.zero;
        targetRigidbody.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// summary: 修正刷怪距离和地面重试次数，避免 Inspector 输入无效值。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        spawnDistance = Mathf.Max(0f, spawnDistance);
        maxGroundSpawnRolls = Mathf.Max(MinimumGroundSpawnRolls, maxGroundSpawnRolls);
        completedWaveCount = Mathf.Max(0, completedWaveCount);
        if (spawnedEnemyParent == null)
        {
            spawnedEnemyParent = transform;
        }
    }

    /// <summary>
    /// summary: 确保当前生成器拥有可用的 Vocalith 随机数源。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureRandomSource()
    {
        randomSource ??= new VocalithRandom();
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

    private bool TryGetSpawnPositionOnGround(Vector3 centerPosition, float radius, bool useFullRadius, out Vector3 spawnPosition)
    {
        spawnPosition = default;
        if (!TryResolveMapGrid())
        {
            return false;
        }

        for (int attempt = 0; attempt < maxGroundSpawnRolls; attempt++)
        {
            Vector3 candidatePosition = GetRandomSpawnCandidatePosition(centerPosition, radius, useFullRadius);
            if (!IsGroundSpawnPosition(candidatePosition))
            {
                continue;
            }

            spawnPosition = candidatePosition;
            return true;
        }

        return false;
    }

    private static void DestroySpawnedObject(GameObject spawnedRoot)
    {
        if (spawnedRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(spawnedRoot);
            return;
        }

        DestroyImmediate(spawnedRoot);
    }
}
