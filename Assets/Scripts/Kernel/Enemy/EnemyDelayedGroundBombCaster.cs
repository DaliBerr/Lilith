using System.Collections;
using System.Collections.Generic;
using Kernel;
using Kernel.MapGrid;
using UnityEngine;
using Vocalith.Logging;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 在玩家脚下按序列投放延时爆炸炸弹，并叠加显示逐步扩张的红色范围圈。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDelayedGroundBombCaster : MonoBehaviour, IEnemySkillCaster
{
    private const int MinimumIndicatorSegments = 16;
    private const float MinimumIndicatorRadius = 0.01f;
    private const float FillPlaneOffset = 0.002f;
    private const float OuterRingPlaneOffset = 0.004f;
    private const float ExpandingRingPlaneOffset = 0.006f;

    private static Material sharedIndicatorMaterial;

    private sealed class BombIndicatorVisual
    {
        public GameObject rootObject;
        public LineRenderer outerRing;
        public LineRenderer expandingRing;
        public MeshFilter fillMeshFilter;
        public MeshRenderer fillRenderer;
        public Mesh fillMesh;
        public Material fillMaterial;
        public Color baseColor;
    }

    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private PlayerHealth targetPlayerHealth;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField, Min(MinimumIndicatorSegments)] private int indicatorSegmentCount = 64;
    [SerializeField, Min(0f)] private float indicatorHeightOffset = 0.08f;
    [SerializeField] private Material indicatorMaterial;

    private readonly List<BombIndicatorVisual> activeIndicators = new();
    private VocalithRandom randomSource;
    private bool isSequenceRunning;
    private float nextAllowedCastTime;

    public EnemySkillKind SkillKind => EnemySkillKind.DelayedGroundBomb;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
        TryResolveTargetMapGrid();
        EnsureRandomSource();
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveStatusEffects();
        TryResolveTargetPlayer();
        TryResolveTargetMapGrid();
        EnsureRandomSource();
        indicatorSegmentCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);
        indicatorHeightOffset = Mathf.Max(0f, indicatorHeightOffset);
    }

    private void OnDisable()
    {
        isSequenceRunning = false;
        ClearActiveIndicators();
    }

    private void OnDestroy()
    {
        ClearActiveIndicators();
    }

    /// <summary>
    /// summary: 尝试按技能槽配置启动一轮延时炸弹序列。
    /// param: skillSlot 当前调度命中的技能槽配置
    /// returns: 成功启动一轮投放时返回 true
    /// </summary>
    public bool TryCastSkill(EnemyDefinition.EnemySkillSlotDefinition skillSlot)
    {
        if (skillSlot.skillKind != SkillKind || !TryResolveEnemyData() || !TryResolveTargetPlayer())
        {
            return false;
        }

        if (TryResolveStatusEffects() && !statusEffects.CanAct)
        {
            return false;
        }

        if (targetPlayerHealth == null || targetPlayerHealth.IsDead || isSequenceRunning || Time.time < nextAllowedCastTime)
        {
            return false;
        }

        float castRange = skillSlot.ResolveCastRange(enemyData.AttackRange);
        if (castRange > 0f && !IsTargetWithinRange(castRange))
        {
            return false;
        }

        EnemyDefinition.DelayedGroundBombSkillDefinition delayedBomb = skillSlot.delayedGroundBombSkill.GetSanitized();
        StartCoroutine(RunBombSequence(delayedBomb));
        return true;
    }

    /// <summary>
    /// summary: 按配置顺序投放一轮炸弹，并在轮次结束后计算下一次随机冷却。
    /// param: delayedBomb 当前技能槽配置的延时炸弹参数
    /// returns: 协程
    /// </summary>
    private IEnumerator RunBombSequence(EnemyDefinition.DelayedGroundBombSkillDefinition delayedBomb)
    {
        isSequenceRunning = true;
        for (int i = 0; i < delayedBomb.bombsPerSequence; i++)
        {
            if (!TryResolveTargetPlayer() || targetPlayerHealth == null || targetPlayerHealth.IsDead)
            {
                break;
            }

            Vector3 bombCenter = ResolveTargetGroundCenter();
            BombIndicatorVisual indicatorVisual = CreateIndicatorVisual(delayedBomb);
            if (indicatorVisual != null)
            {
                activeIndicators.Add(indicatorVisual);
            }

            StartCoroutine(RunSingleBomb(delayedBomb, bombCenter, indicatorVisual));
            if (i < delayedBomb.bombsPerSequence - 1)
            {
                yield return WaitForGameplaySeconds(delayedBomb.bombIntervalSeconds);
            }
        }

        isSequenceRunning = false;
        float nextCooldown = ResolveRandomCooldown(delayedBomb);
        nextAllowedCastTime = Time.time + Mathf.Max(0f, nextCooldown);
        GameDebug.Log($"EnemyDelayedGroundBombCaster on {gameObject.name} finished sequence, next cooldown {nextCooldown:F2}s.");
    }

    /// <summary>
    /// summary: 推进单颗炸弹的倒计时与范围圈扩张，并在结束时结算爆炸伤害。
    /// param: delayedBomb 当前技能参数
    /// param: center 当前炸弹中心点
    /// param: indicator 当前炸弹对应的红圈渲染器
    /// returns: 协程
    /// </summary>
    private IEnumerator RunSingleBomb(
        EnemyDefinition.DelayedGroundBombSkillDefinition delayedBomb,
        Vector3 center,
        BombIndicatorVisual indicator)
    {
        float elapsed = 0f;
        float delaySeconds = Mathf.Max(0f, delayedBomb.delaySeconds);
        UpdateIndicatorVisual(indicator, center, delayedBomb.explosionRadius, 0f, 0f);

        while (elapsed < delaySeconds)
        {
            if (!EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
            {
                elapsed += Time.deltaTime;
                float progress = delaySeconds > 0f ? Mathf.Clamp01(elapsed / delaySeconds) : 1f;
                float expandingRadius = Mathf.Lerp(0f, delayedBomb.explosionRadius, progress);
                UpdateIndicatorVisual(indicator, center, delayedBomb.explosionRadius, expandingRadius, progress);
            }

            yield return null;
        }

        UpdateIndicatorVisual(indicator, center, delayedBomb.explosionRadius, delayedBomb.explosionRadius, 1f);
        TryApplyExplosionToPlayer(center, delayedBomb.explosionRadius, delayedBomb.explosionDamage);
        DestroyIndicator(indicator);
    }

    /// <summary>
    /// summary: 在给定中心点按半径判定玩家是否被命中，并结算固定伤害。
    /// param: center 爆炸中心点
    /// param: explosionRadius 爆炸半径
    /// param: explosionDamage 对玩家造成的伤害
    /// returns: 玩家实际受伤时返回 true
    /// </summary>
    private bool TryApplyExplosionToPlayer(Vector3 center, float explosionRadius, float explosionDamage)
    {
        if (!TryResolveTargetPlayer() || targetPlayerHealth == null || targetPlayerHealth.IsDead)
        {
            return false;
        }

        Vector3 offset = targetPlayer.position - center;
        offset.y = 0f;
        if (offset.sqrMagnitude > explosionRadius * explosionRadius)
        {
            return false;
        }

        bool didDamage = targetPlayerHealth.TryApplyDamage(explosionDamage, out _, out _);
        if (didDamage)
        {
            GameDebug.Log($"EnemyDelayedGroundBombCaster on {gameObject.name} exploded at {center}, radius={explosionRadius:F2}, damage={explosionDamage:F2}.");
        }

        return didDamage;
    }

    /// <summary>
    /// summary: 创建一条用于显示爆炸范围的红色圆环渲染器。
    /// param: delayedBomb 当前技能配置
    /// returns: 可用的 LineRenderer 引用
    /// </summary>
    private BombIndicatorVisual CreateIndicatorVisual(EnemyDefinition.DelayedGroundBombSkillDefinition delayedBomb)
    {
        Material resolvedMaterial = indicatorMaterial != null ? indicatorMaterial : ResolveSharedIndicatorMaterial();
        if (resolvedMaterial == null)
        {
            return null;
        }

        GameObject indicatorObject = new($"{name}_DelayedBombIndicator");
        BombIndicatorVisual indicator = new()
        {
            rootObject = indicatorObject,
            baseColor = delayedBomb.indicatorColor,
        };

        Color outerRingColor = delayedBomb.indicatorColor;
        outerRingColor.a = Mathf.Clamp01(Mathf.Max(outerRingColor.a * 0.85f, 0.35f));
        indicator.outerRing = CreateRingRenderer(
            indicatorObject.transform,
            "OuterRing",
            delayedBomb.indicatorWidth,
            outerRingColor,
            resolvedMaterial,
            sortingOrder: 10);

        Color expandingRingColor = delayedBomb.indicatorColor;
        expandingRingColor.a = Mathf.Clamp01(Mathf.Max(expandingRingColor.a, 0.8f));
        indicator.expandingRing = CreateRingRenderer(
            indicatorObject.transform,
            "ExpandingRing",
            delayedBomb.indicatorWidth,
            expandingRingColor,
            resolvedMaterial,
            sortingOrder: 12);

        GameObject fillObject = new("Fill");
        fillObject.transform.SetParent(indicatorObject.transform, false);
        indicator.fillMeshFilter = fillObject.AddComponent<MeshFilter>();
        indicator.fillRenderer = fillObject.AddComponent<MeshRenderer>();
        indicator.fillMesh = new Mesh
        {
            name = $"{name}_DelayedBombFillMesh",
        };
        indicator.fillMesh.MarkDynamic();
        indicator.fillMeshFilter.sharedMesh = indicator.fillMesh;

        indicator.fillMaterial = new Material(resolvedMaterial)
        {
            name = $"{name}_DelayedBombFillMaterial"
        };
        ApplyMaterialColor(indicator.fillMaterial, ResolveFillColor(indicator.baseColor, 0f));
        indicator.fillRenderer.sharedMaterial = indicator.fillMaterial;
        indicator.fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        indicator.fillRenderer.receiveShadows = false;
        indicator.fillRenderer.allowOcclusionWhenDynamic = true;
        indicator.fillRenderer.sortingOrder = 8;

        return indicator;
    }

    /// <summary>
    /// summary: 创建单条红圈渲染器，用于静态外圈或扩张内圈。
    /// param: parent 当前技能指示器根节点
    /// param: objectName 圈渲染器对象名称
    /// param: width 圈线宽
    /// param: color 圈颜色
    /// param: material 圈材质
    /// param: sortingOrder 圈渲染顺序
    /// returns: 创建完成的圈渲染器
    /// </summary>
    private LineRenderer CreateRingRenderer(
        Transform parent,
        string objectName,
        float width,
        Color color,
        Material material,
        int sortingOrder)
    {
        GameObject ringObject = new(objectName);
        ringObject.transform.SetParent(parent, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.enabled = true;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);
        ring.widthMultiplier = width;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.textureMode = LineTextureMode.Stretch;
        ring.startColor = color;
        ring.endColor = color;
        ring.sharedMaterial = material;
        ring.sortingOrder = sortingOrder;
        return ring;
    }

    /// <summary>
    /// summary: 更新延时炸弹的外圈、扩张圈与内部填充表现。
    /// param: indicator 当前炸弹可视化对象
    /// param: center 红圈中心点
    /// param: outerRadius 外圈固定半径
    /// param: expandingRadius 扩张圈当前半径
    /// param: fillProgress 当前填充渐深进度
    /// returns: 无
    /// </summary>
    private void UpdateIndicatorVisual(
        BombIndicatorVisual indicator,
        Vector3 center,
        float outerRadius,
        float expandingRadius,
        float fillProgress)
    {
        if (indicator == null)
        {
            return;
        }

        UpdateRingGeometry(indicator.outerRing, center, outerRadius, OuterRingPlaneOffset);
        UpdateRingGeometry(indicator.expandingRing, center, expandingRadius, ExpandingRingPlaneOffset);
        UpdateFillMesh(indicator, center, expandingRadius, fillProgress);
    }

    /// <summary>
    /// summary: 用当前目标半径重建单条圆环顶点。
    /// param: ring 当前红圈渲染器
    /// param: center 圆心
    /// param: radius 圆环半径
    /// param: planeOffset 与地面的微小偏移，避免 z-fighting
    /// returns: 无
    /// </summary>
    private void UpdateRingGeometry(LineRenderer ring, Vector3 center, float radius, float planeOffset)
    {
        if (ring == null)
        {
            return;
        }

        int segmentCount = Mathf.Max(MinimumIndicatorSegments, ring.positionCount);
        float resolvedRadius = Mathf.Max(MinimumIndicatorRadius, radius);
        float worldY = center.y + planeOffset;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segmentCount;
            Vector3 position = new(
                center.x + Mathf.Cos(angle) * resolvedRadius,
                worldY,
                center.z + Mathf.Sin(angle) * resolvedRadius);
            ring.SetPosition(i, position);
        }
    }

    /// <summary>
    /// summary: 更新扩张圈内部填充扇形网格，并按进度加深红色。
    /// param: indicator 当前炸弹可视化对象
    /// param: center 填充中心点
    /// param: radius 填充半径
    /// param: progress 当前填充渐深进度
    /// returns: 无
    /// </summary>
    private void UpdateFillMesh(BombIndicatorVisual indicator, Vector3 center, float radius, float progress)
    {
        if (indicator.fillMesh == null || indicator.fillMaterial == null)
        {
            return;
        }

        int segmentCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);
        float resolvedRadius = Mathf.Max(MinimumIndicatorRadius, radius);
        Vector3[] vertices = new Vector3[segmentCount + 1];
        int[] triangles = new int[segmentCount * 3];
        float worldY = center.y + FillPlaneOffset;
        vertices[0] = new Vector3(center.x, worldY, center.z);

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segmentCount;
            vertices[i + 1] = new Vector3(
                center.x + Mathf.Cos(angle) * resolvedRadius,
                worldY,
                center.z + Mathf.Sin(angle) * resolvedRadius);

            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == segmentCount - 1 ? 1 : i + 2;
        }

        indicator.fillMesh.Clear();
        indicator.fillMesh.vertices = vertices;
        indicator.fillMesh.triangles = triangles;
        indicator.fillMesh.RecalculateNormals();
        indicator.fillMesh.RecalculateBounds();
        ApplyMaterialColor(indicator.fillMaterial, ResolveFillColor(indicator.baseColor, progress));
    }

    /// <summary>
    /// summary: 按进度生成从浅到深的红色填充。
    /// param: baseColor 以技能配置颜色为基准色
    /// param: progress 当前进度，0 为最浅，1 为最深
    /// returns: 当前填充颜色
    /// </summary>
    private static Color ResolveFillColor(Color baseColor, float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);

        Color startColor = baseColor;
        startColor.g *= 0.85f;
        startColor.b *= 0.85f;
        startColor.a = Mathf.Clamp01(Mathf.Max(baseColor.a * 0.08f, 0.06f));

        Color endColor = baseColor;
        endColor.g *= 0.35f;
        endColor.b *= 0.35f;
        endColor.a = Mathf.Clamp01(Mathf.Max(baseColor.a * 0.6f, 0.45f));

        return Color.Lerp(startColor, endColor, clampedProgress);
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    /// <summary>
    /// summary: 解析当前投放中心点在地图平面上的世界坐标。
    /// param: 无
    /// returns: 当前炸弹应使用的世界中心点
    /// </summary>
    private Vector3 ResolveTargetGroundCenter()
    {
        Vector3 center = targetPlayer != null ? targetPlayer.position : transform.position;
        float planeY = TryResolveTargetMapGrid() ? targetMapGrid.WorldPlaneY : center.y;
        return WorldHeightUtility.GetPositionAtPlaneHeight(center, planeY, indicatorHeightOffset);
    }

    /// <summary>
    /// summary: 在保持游戏暂停语义的前提下等待一段游戏逻辑时间。
    /// param: duration 需要等待的秒数
    /// returns: 协程
    /// </summary>
    private IEnumerator WaitForGameplaySeconds(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            if (!EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
            {
                remaining -= Time.deltaTime;
            }

            yield return null;
        }
    }

    /// <summary>
    /// summary: 从给定区间内解析一次随机冷却时长。
    /// param: delayedBomb 当前技能配置
    /// returns: 本轮结束后应使用的冷却秒数
    /// </summary>
    private float ResolveRandomCooldown(EnemyDefinition.DelayedGroundBombSkillDefinition delayedBomb)
    {
        EnsureRandomSource();
        float minCooldown = Mathf.Max(0f, delayedBomb.randomCooldownMinSeconds);
        float maxCooldown = Mathf.Max(minCooldown, delayedBomb.randomCooldownMaxSeconds);
        if (Mathf.Approximately(maxCooldown, minCooldown))
        {
            return minCooldown;
        }

        return Mathf.Lerp(minCooldown, maxCooldown, randomSource.NextFloat01());
    }

    /// <summary>
    /// summary: 清理当前执行器持有的所有红圈实例，避免残留对象泄漏。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ClearActiveIndicators()
    {
        for (int i = activeIndicators.Count - 1; i >= 0; i--)
        {
            DestroyIndicator(activeIndicators[i]);
        }

        activeIndicators.Clear();
    }

    private void DestroyIndicator(BombIndicatorVisual indicator)
    {
        if (indicator == null)
        {
            return;
        }

        activeIndicators.Remove(indicator);
        if (indicator.fillMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(indicator.fillMesh);
            }
            else
            {
                DestroyImmediate(indicator.fillMesh);
            }
        }

        if (indicator.fillMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(indicator.fillMaterial);
            }
            else
            {
                DestroyImmediate(indicator.fillMaterial);
            }
        }

        if (indicator.rootObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(indicator.rootObject);
            }
            else
            {
                DestroyImmediate(indicator.rootObject);
            }
        }
    }

    private bool IsTargetWithinRange(float range)
    {
        Vector3 offset = targetPlayer.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
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

    private bool TryResolveTargetMapGrid()
    {
        if (targetMapGrid != null)
        {
            return true;
        }

        targetMapGrid = FindFirstObjectByType<MapGridAuthoring>();
        return targetMapGrid != null;
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

    private static Material ResolveSharedIndicatorMaterial()
    {
        if (sharedIndicatorMaterial != null)
        {
            return sharedIndicatorMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        sharedIndicatorMaterial = new Material(shader)
        {
            name = "EnemyDelayedBombIndicatorMaterial"
        };
        return sharedIndicatorMaterial;
    }

    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
