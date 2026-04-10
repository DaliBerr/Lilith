using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.MapGrid;
using UnityEngine;
using Vocalith.Logging;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 负责接收波次掉落配置，并在敌人死亡时生成对应的 Bullet Token 拾取物。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyBulletTokenDropper : MonoBehaviour, IEnemyWaveConfigReceiver
{
    [Header("Pickup")]
    [SerializeField] private BulletTokenPickup pickupPrefab;
    [SerializeField] private Transform pickupParent;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField, Min(0f)] private float pickupHeightOffset = 6f;
    [SerializeField, Min(0f)] private float pickupSpreadRadius = 6f;

    private readonly List<EnemyBulletTokenDropEntry> tokenDrops = new();
    private readonly List<PlaceableTokenData> rolledDrops = new();

    private Enemy ownerEnemy;
    private VocalithRandom randomSource;
    private bool isSubscribedToDeath;

    private void Awake()
    {
        TryResolveEnemy();
        TryResolveMapGrid();
        EnsureRandomSource();
        SanitizeConfiguration();
        EnsureDeathSubscription();
    }

    private void OnEnable()
    {
        EnsureDeathSubscription();
    }

    private void OnDisable()
    {
        RemoveDeathSubscription();
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
    }

    /// <summary>
    /// summary: 缓存当前波次下该敌人的掉落表，供死亡时独立概率判定使用。
    /// param: config 当前波次指定的敌人配置
    /// returns: 无
    /// </summary>
    public void ApplyWaveConfig(EnemyWaveConfig config)
    {
        EnsureDeathSubscription();
        EnsureRandomSource();
        tokenDrops.Clear();
        if (config.tokenDrops == null)
        {
            return;
        }

        for (int i = 0; i < config.tokenDrops.Count; i++)
        {
            EnemyBulletTokenDropEntry entry = config.tokenDrops[i];
            if (entry.token == null)
            {
                continue;
            }

            entry.dropChance = Mathf.Clamp01(entry.dropChance);
            entry.dropCount = Mathf.Max(1, entry.dropCount);
            tokenDrops.Add(entry);
        }
    }

    /// <summary>
    /// summary: 供测试与 prefab 初始化直接设置默认 pickup 模板。
    /// param: prefab 要实例化的拾取物 prefab
    /// returns: 传入 prefab 有效时返回 true
    /// </summary>
    public bool TrySetPickupPrefab(BulletTokenPickup prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        pickupPrefab = prefab;
        return true;
    }

    /// <summary>
    /// summary: 供测试读取当前缓存的掉落表，确认波次配置是否已经正确写入。
    /// param: 无
    /// returns: 当前缓存的掉落表只读视图
    /// </summary>
    public IReadOnlyList<EnemyBulletTokenDropEntry> TokenDrops => tokenDrops;

    /// <summary>
    /// summary: 在敌人确认死亡时独立判定所有掉落项，并把成功项实例化为场景 pickup。
    /// param: deadEnemy 触发死亡通知的敌人
    /// returns: 无
    /// </summary>
    private void HandleEnemyDied(Enemy deadEnemy)
    {
        if (deadEnemy == null || deadEnemy != ownerEnemy)
        {
            return;
        }

        if (pickupPrefab == null)
        {
            GameDebug.LogWarning("[EnemyBulletTokenDropper] Pickup prefab is missing, skip token drop.");
            return;
        }

        if (tokenDrops.Count <= 0)
        {
            return;
        }

        rolledDrops.Clear();
        for (int i = 0; i < tokenDrops.Count; i++)
        {
            EnemyBulletTokenDropEntry entry = tokenDrops[i];
            if (!ShouldDrop(entry))
            {
                continue;
            }

            int dropCount = Mathf.Max(1, entry.dropCount);
            for (int countIndex = 0; countIndex < dropCount; countIndex++)
            {
                rolledDrops.Add(entry.token);
            }
        }

        if (rolledDrops.Count <= 0)
        {
            return;
        }

        EnsureRandomSource();
        Transform resolvedParent = pickupParent != null ? pickupParent : ownerEnemy.transform.parent;
        float baseAngleDegrees = randomSource.NextFloat01() * 360f;
        Vector3 basePosition = Kernel.WorldHeightUtility.GetPositionAtPlaneHeight(
            ownerEnemy.transform.position,
            GetPickupPlaneY(),
            pickupHeightOffset);

        for (int i = 0; i < rolledDrops.Count; i++)
        {
            PlaceableTokenData token = rolledDrops[i];
            Vector3 spawnPosition = basePosition + GetSpreadOffset(i, rolledDrops.Count, baseAngleDegrees);
            BulletTokenPickup spawnedPickup = Instantiate(pickupPrefab, spawnPosition, Quaternion.identity, resolvedParent);
            if (!spawnedPickup.TrySetToken(token))
            {
                GameDebug.LogWarning("[EnemyBulletTokenDropper] Spawned pickup is missing a valid token binding.");
                Destroy(spawnedPickup.gameObject);
            }
        }
    }

    /// <summary>
    /// summary: 判断单个掉落项本次是否命中独立概率，0 和 1 的边界值不走随机数。
    /// param: entry 当前待判定的掉落项
    /// returns: 本次需要生成对应拾取物时返回 true
    /// </summary>
    private bool ShouldDrop(EnemyBulletTokenDropEntry entry)
    {
        float chance = Mathf.Clamp01(entry.dropChance);
        if (chance <= 0f || entry.token == null)
        {
            return false;
        }

        if (chance >= 1f)
        {
            return true;
        }

        EnsureRandomSource();
        return randomSource.NextFloat01() <= chance;
    }

    /// <summary>
    /// summary: 按成功掉落数量为 pickup 生成一个简单的平面散布偏移，避免多项重叠在同一点。
    /// param: index 当前 pickup 的序号
    /// param: totalCount 本次总共需要生成的 pickup 数量
    /// param: baseAngleDegrees 当前死亡事件的起始角度
    /// returns: 当前 pickup 的世界坐标偏移
    /// </summary>
    private Vector3 GetSpreadOffset(int index, int totalCount, float baseAngleDegrees)
    {
        if (totalCount <= 1 || pickupSpreadRadius <= 0f)
        {
            return Vector3.zero;
        }

        float angleDegrees = baseAngleDegrees + (360f / totalCount) * index;
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angleRadians), 0f, Mathf.Sin(angleRadians)) * pickupSpreadRadius;
    }

    /// <summary>
    /// summary: 解析当前同物体上的 Enemy 组件，供死亡通知订阅和出生位置读取使用。
    /// param: 无
    /// returns: 成功拿到同物体上的 Enemy 组件时返回 true
    /// </summary>
    private bool TryResolveEnemy()
    {
        if (ownerEnemy != null)
        {
            return true;
        }

        ownerEnemy = GetComponent<Enemy>();
        return ownerEnemy != null;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定地图时，尝试自动解析当前场景中的 MapGridAuthoring。
    /// param: 无
    /// returns: 成功拿到地图平面来源时返回 true
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
    /// summary: 统一确保当前组件已经订阅到敌人的死亡事件，避免 EditMode 或动态挂载时漏掉生命周期回调。
    /// param: 无
    /// returns: 成功建立订阅时返回 true
    /// </summary>
    private bool EnsureDeathSubscription()
    {
        if (isSubscribedToDeath)
        {
            return true;
        }

        if (!TryResolveEnemy())
        {
            return false;
        }

        ownerEnemy.Died += HandleEnemyDied;
        isSubscribedToDeath = true;
        return true;
    }

    /// <summary>
    /// summary: 取消当前组件对敌人死亡事件的订阅，避免组件停用或销毁后残留委托。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void RemoveDeathSubscription()
    {
        if (!isSubscribedToDeath || ownerEnemy == null)
        {
            isSubscribedToDeath = false;
            return;
        }

        ownerEnemy.Died -= HandleEnemyDied;
        isSubscribedToDeath = false;
    }

    /// <summary>
    /// summary: 确保掉落组件拥有可用的随机数源。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureRandomSource()
    {
        randomSource ??= new VocalithRandom();
    }

    /// <summary>
    /// summary: 修正 Inspector 中允许被误填成负值的基础散布参数。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        pickupHeightOffset = Mathf.Max(0f, pickupHeightOffset);
        pickupSpreadRadius = Mathf.Max(0f, pickupSpreadRadius);
    }

    /// <summary>
    /// summary: 获取当前 pickup 生成逻辑应该使用的共享地图平面高度。
    /// param: 无
    /// returns: 共享地图平面的世界 Y；找不到地图时回退到敌人当前根节点高度
    /// </summary>
    private float GetPickupPlaneY()
    {
        return TryResolveMapGrid() ? targetMapGrid.WorldPlaneY : ownerEnemy.transform.position.y;
    }
}
