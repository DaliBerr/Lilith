using System;
using System.Collections.Generic;
using Kernel.GameState;
using TMPro;
using UnityEngine;
using Vocalith.Logging;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Kernel.Bullet
{
/// <summary>
/// CharBullet 是一个高度模块化的子弹控制组件，设计用于承载各种基于字符的攻击行为。
/// 通过组合不同的攻击配置和运行时参数，CharBullet 可以实现丰富多样的子弹表现和交互效果。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharBullet : MonoBehaviour
{
    private const float MinimumVectorSqrMagnitude = 0.0001f;
    private const float DefaultHomingSearchRadius = 256f;
    private const float DefaultHomingTurnSpeedDegrees = 540f;
    private const float HomingTargetRefreshIntervalSeconds = 0.12f;
    private const float DefaultBehaviorChainRadius = 96f;
    private const float BehaviorRushAccelerationScalePerSecond = 0.25f;
    private const float BehaviorRushMaxSpeedMultiplier = 2.5f;
    private const float BehaviorSlowMinSpeedMultiplier = 0.15f;
    private const float BehaviorSnakeAmplitudeUnits = 6f;
    private const float BehaviorSnakeFrequencyHz = 2f;
    private const float BehaviorWanderTurnSpeedDegrees = 90f;
    private const float BehaviorSplitChildDamageMultiplier = 0.5f;
    private const float BehaviorSplitForkAngleDegrees = 35f;
    private const int BehaviorSplitChildrenPerTrigger = 2;
    private const int MaxBehaviorSplitDerivedProjectileCount = 64;
    private const float ResultDrainHealingMultiplier = 0.5f;
    private const float ResultShieldAmountMultiplier = 1f;
    private const float ResultDisplacementDistancePerStrength = 2f;
    private const float DefaultDelayedExplosionIndicatorWidth = 0.2f;
    private const float DefaultDelayedExplosionIndicatorHeightOffset = 0.08f;
    private const int MaxPayloadDepth = 4;
    private const int MaxPayloadDerivedProjectileCount = 64;
    private const string EnemyTagName = "Enemy_Object";
    private static readonly Color DefaultDelayedExplosionIndicatorColor = new(1f, 0.1f, 0.1f, 0.92f);
    internal static Func<int, int> ConfuseCandidateIndexResolver { get; set; } = count => UnityEngine.Random.Range(0, count);
    // private const string AlternateEnemyTagName = "Enemy";

    private static readonly string[] PreferredMovementChildNames =
    {
        "Movement",
        "MoveRoot",
        "Mover",
        "Visual",
        "Glyph",
    };

    [Header("Bindings")]
    [SerializeField] private TMP_Text glyphText;
    [SerializeField] private Transform movementTarget;
    [SerializeField] private Transform sizeTarget;
    [SerializeField] private Rigidbody movementRigidbody;
    [SerializeField] private SphereCollider impactCollider;
    [SerializeField] private CharBulletVisualPresenter visualPresenter;
#if UNITY_EDITOR
    private bool editorPreviewRefreshQueued;
#endif

    [Header("Combat")]
    [SerializeField] private AttackSpec attackSpec = AttackSpec.CreateDefault();

    [Header("Movement")]
    [SerializeField] private bool autoMove = true;
    [SerializeField] private Space movementSpace = Space.World;
    [SerializeField] private Vector3 direction = Vector3.forward;
    [SerializeField, Min(0f)] private float speed;
    [SerializeField, Min(0f)] private float homingTurnSpeedDegrees = DefaultHomingTurnSpeedDegrees;

    [Header("Scale")]
    [SerializeField, Min(0f)] private float scaleMultiplier = 1f;
    [SerializeField, Min(0f)] private float impactRadiusMultiplier = 1f;

    private readonly HashSet<int> impactedTargetRoots = new();
    private readonly HashSet<int> penetratedEnvironmentColliders = new();
    private readonly List<SpellPayloadBlock> activePayloads = new();
    private readonly HashSet<SpellPayloadBlock> executedOneShotPayloads = new();
    private Vector3 baseLocalScale = Vector3.one;
    private float baseImpactRadius = 0.5f;
    private float fontSizeDrivenImpactRadius;
    private bool hasBaseScaleSnapshot;
    private bool hasFontSizeDrivenImpactRadius;
    private Transform ownerRoot;
    private Vector3 spawnWorldPosition;
    private Vector3 previousImpactCheckCenter;
    private float elapsedLifetime;
    private int remainingLife;
    private bool hasPreviousImpactCheckCenter;
    private bool isActiveShot;
    private bool ignoreGameplayPauseStatus;
    private Transform movementAnchor;
    private bool expireWhenMovementAnchorInvalid;
    private SpellProjectileNode currentProjectileNode;
    private CharBullet spawnTemplate;
    private BulletTargetPolicy targetPolicy = BulletTargetPolicy.EnemiesOnly;
    private int remainingBounceCount;
    private Transform homingTarget;
    private float nextHomingTargetRefreshTime;
    private Vector3 behaviorBaseDirection = Vector3.forward;
    private Vector3 behaviorRightDirection = Vector3.right;
    private float behaviorInitialSpeed;
    private float behaviorWanderSeed;
    private int behaviorSplitTotalCount;
    private int behaviorSplitTriggeredCount;
    private int behaviorSplitEmittedCount;
    private Vector3 behaviorSpinLastAnchorPosition;
    private Vector3 behaviorSpinRadialDirection = Vector3.forward;
    private float behaviorSpinRadius;
    private float behaviorSpinAngleDegrees;
    private float behaviorSpinDirectionSign = 1f;
    private bool stasisDirectImpactConsumed;
    private bool stasisDirectImpactInProgress;

    public TMP_Text GlyphText
    {
        get
        {
            TryCacheBindings();
            return glyphText;
        }
    }

    public Transform MovementTarget
    {
        get
        {
            TryCacheBindings();
            return movementTarget != null ? movementTarget : transform;
        }
    }

    public Transform SizeTarget
    {
        get
        {
            TryCacheBindings();
            return sizeTarget != null ? sizeTarget : MovementTarget;
        }
    }

    public Rigidbody MovementRigidbody
    {
        get
        {
            TryCacheBindings();
            return movementRigidbody;
        }
    }

    public SphereCollider ImpactCollider
    {
        get
        {
            TryCacheBindings();
            return impactCollider;
        }
    }

    public bool AutoMove
    {
        get => autoMove;
        set => autoMove = value;
    }

    public Space MovementSpace
    {
        get => movementSpace;
        set => movementSpace = value;
    }

    public Transform OwnerRoot => ownerRoot;
    public Transform MovementAnchor => movementAnchor;
    public Vector3 SpawnWorldPosition => spawnWorldPosition;
    public float ElapsedLifetime => elapsedLifetime;
    public int RemainingLife => remainingLife;
    public bool IsActiveShot => isActiveShot;
    public Vector3 Direction => direction;
    public float Speed => speed;
    public float ScaleMultiplier => scaleMultiplier;
    public float ImpactRadiusMultiplier => impactRadiusMultiplier;
    public float Damage => attackSpec.damage;
    public AttackSpec CurrentAttackSpec => attackSpec;
    public SpellProjectileNode CurrentProjectileNode => currentProjectileNode;
    public IReadOnlyList<SpellPayloadBlock> CurrentPayloads => activePayloads;
    public BulletTargetPolicy TargetPolicy => targetPolicy;

    private void Awake()
    {
        TryCacheBindings();
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        attackSpec = attackSpec.GetSanitized();
        spawnTemplate ??= this;
        remainingBounceCount = attackSpec.bounceCount;
        CaptureCurrentScaleAsBase();
        CaptureImpactColliderBaseRadius();
        ApplyScaleMultiplier();
    }

    public void InitializeShot(
        Transform owner,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        AttackSpec shotAttackSpec,
        SpellProjectileNode shotProjectileNode,
        BulletTargetPolicy shotTargetPolicy = BulletTargetPolicy.EnemiesOnly)
    {
        InitializeShotCore(owner, spawnPosition, shotDirection, shotAttackSpec, shotProjectileNode, shotTargetPolicy);
    }

    private void InitializeShotCore(
        Transform owner,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        AttackSpec shotAttackSpec,
        SpellProjectileNode shotProjectileNode,
        BulletTargetPolicy shotTargetPolicy)
    {
        TryCacheBindings(overwriteExisting: true);
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        AttackSpec resolvedAttackSpec = shotProjectileNode != null ? shotProjectileNode.AttackSpec : shotAttackSpec;
        attackSpec = resolvedAttackSpec.GetSanitized();
        currentProjectileNode = shotProjectileNode;
        activePayloads.Clear();
        executedOneShotPayloads.Clear();
        if (shotProjectileNode != null)
        {
            for (int i = 0; i < shotProjectileNode.Payloads.Count; i++)
            {
                if (shotProjectileNode.Payloads[i] != null)
                {
                    activePayloads.Add(shotProjectileNode.Payloads[i]);
                }
            }
        }

        spawnTemplate ??= this;
        targetPolicy = shotTargetPolicy;
        ownerRoot = owner;
        movementAnchor = null;
        expireWhenMovementAnchorInvalid = false;
        spawnWorldPosition = spawnPosition;
        elapsedLifetime = 0f;
        impactedTargetRoots.Clear();
        penetratedEnvironmentColliders.Clear();
        hasPreviousImpactCheckCenter = false;
        remainingLife = attackSpec.projectileLife;
        remainingBounceCount = attackSpec.bounceCount;
        isActiveShot = true;
        autoMove = true;
        movementSpace = Space.World;
        homingTarget = null;
        nextHomingTargetRefreshTime = 0f;
        behaviorBaseDirection = ResolvePlanarDirection(shotDirection, Vector3.forward);
        behaviorRightDirection = ResolvePlanarRightDirection(behaviorBaseDirection);
        behaviorInitialSpeed = Mathf.Max(0f, attackSpec.projectileSpeed);
        behaviorWanderSeed = ResolveBehaviorSeed(spawnPosition, behaviorBaseDirection);
        behaviorSplitTotalCount = Mathf.Max(0, Mathf.RoundToInt(attackSpec.behaviorParameter));
        behaviorSplitTriggeredCount = 0;
        behaviorSplitEmittedCount = 0;
        behaviorSpinLastAnchorPosition = ownerRoot != null ? ownerRoot.position : spawnPosition;
        behaviorSpinRadialDirection = behaviorBaseDirection;
        behaviorSpinRadius = 0f;
        behaviorSpinAngleDegrees = 0f;
        behaviorSpinDirectionSign = 1f;
        stasisDirectImpactConsumed = false;
        stasisDirectImpactInProgress = false;
        hasFontSizeDrivenImpactRadius = false;
        fontSizeDrivenImpactRadius = 0f;
        ignoreGameplayPauseStatus = false;

        EnableImpactCollider(true);
        TryStopMovement();
        TrySetWorldPosition(spawnPosition);
        TrySetDirectionAndSpeed(shotDirection, attackSpec.projectileSpeed, Space.World);
        ConfigureInitialMovementBehavior();
        ApplyFacingDirection(shotDirection);
        IgnoreOwnerCollisions();
        ResetImpactCheckState();
        ApplyProjectilePresentation();

        // LogShotInitialized(spawnPosition, shotDirection, attackSpec.projectileSpeed);
        // LogSpawnOverlapIfNeeded();
    }

    /// <summary>
    /// summary: 记录当前子弹后续二次发射应复用的模板实例，避免从运行态子弹复制污染过的运行时状态。
    /// param: template 发射子弹时使用的模板对象
    /// returns: 无
    /// </summary>
    public void SetSpawnTemplate(CharBullet template)
    {
        spawnTemplate = template != null ? template : this;
    }

    /// <summary>
    /// summary: 为 Orbit/Spin 类运动指定额外移动锚点；施法归属仍由 ownerRoot 表示。
    /// param: anchor 运动锚点
    /// param: expireWhenInvalid 锚点丢失或主弹失活时是否立刻过期
    /// returns: 无
    /// </summary>
    public void SetMovementAnchor(Transform anchor, bool expireWhenInvalid)
    {
        movementAnchor = anchor;
        expireWhenMovementAnchorInvalid = expireWhenInvalid;
        if (TryResolveSpinAnchorPosition(expireIfInvalid: false, out Vector3 anchorPosition))
        {
            behaviorSpinLastAnchorPosition = anchorPosition;
        }

        if (isActiveShot && ShouldUseSpinBehavior())
        {
            ConfigureSpinBehavior();
        }
    }

    /// <summary>
    /// summary: 设置当前子弹是否忽略战斗暂停门控，供背包内预览子弹保持演示动画。
    /// param: ignoreGameplayPause true 表示忽略暂停门控，false 表示按战斗暂停状态冻结
    /// returns: 无
    /// </summary>
    public void SetIgnoreGameplayPauseStatus(bool ignoreGameplayPause)
    {
        ignoreGameplayPauseStatus = ignoreGameplayPause;
    }

    /// <summary>
    /// summary: 把一个目标根节点加入本发子弹的忽略命中集合，避免分裂子弹出生瞬间再次命中原目标。
    /// param: targetRoot 需要忽略的目标根节点
    /// returns: 传入目标有效并成功加入集合时返回 true
    /// </summary>
    public bool RegisterIgnoredTargetRoot(Transform targetRoot)
    {
        if (targetRoot == null)
        {
            return false;
        }

        impactedTargetRoots.Add(targetRoot.GetInstanceID());
        return true;
    }

    /// <summary>
    /// summary: 尝试缓存当前 prefab 层级中的文字、移动和命中引用。
    /// param: overwriteExisting 为 true 时强制刷新已有引用
    /// returns: 成功解析到有效移动目标时返回 true
    /// </summary>
    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || !IsGlyphTextReferenceValid())
        {
            glyphText = FindPreferredGlyphText();
        }

        if (overwriteExisting || !IsMovementTargetReferenceValid())
        {
            movementTarget = FindPreferredMovementTarget();
        }

        if (overwriteExisting || !IsSizeTargetReferenceValid())
        {
            sizeTarget = FindPreferredSizeTarget();
        }

        if (overwriteExisting || !IsMovementRigidbodyReferenceValid())
        {
            movementRigidbody = FindPreferredMovementRigidbody(movementTarget);
        }

        if (overwriteExisting || !IsImpactColliderReferenceValid())
        {
            impactCollider = FindPreferredImpactCollider();
            CaptureImpactColliderBaseRadius();
        }

        if (overwriteExisting || visualPresenter == null)
        {
            visualPresenter = GetComponent<CharBulletVisualPresenter>();
        }

        return IsMovementTargetReferenceValid();
    }

    /// <summary>
    /// summary: 绑定用于显示字符的 TMP 文本组件。
    /// param: text 需要绑定的文字组件
    /// returns: 绑定成功时返回 true
    /// </summary>
    public bool TryBindGlyph(TMP_Text text)
    {
        if (text == null || !IsTransformInsideBullet(text.transform))
        {
            return false;
        }

        glyphText = text;
        if (!IsSizeTargetReferenceValid())
        {
            sizeTarget = ResolvePreferredSizeTargetForGlyph(text);
        }

        CaptureCurrentScaleAsBase();
        ApplyScaleMultiplier();
        return true;
    }

    /// <summary>
    /// summary: 绑定用于移动子弹的目标节点和可选刚体。
    /// param: target 需要被推进的目标节点
    /// param: rigidbody 需要同步速度的刚体
    /// returns: 绑定成功时返回 true
    /// </summary>
    public bool TryBindMovementTarget(Transform target, Rigidbody rigidbody = null)
    {
        Transform resolvedTarget = target != null ? target : transform;
        if (!IsTransformInsideBullet(resolvedTarget))
        {
            return false;
        }

        if (rigidbody != null &&
            (!IsTransformInsideBullet(rigidbody.transform) ||
             (rigidbody.transform != resolvedTarget && !rigidbody.transform.IsChildOf(resolvedTarget))))
        {
            return false;
        }

        movementTarget = resolvedTarget;
        movementRigidbody = rigidbody != null ? rigidbody : FindPreferredMovementRigidbody(resolvedTarget);
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        if (!IsSizeTargetReferenceValid())
        {
            sizeTarget = FindPreferredSizeTarget();
        }

        CaptureCurrentScaleAsBase();
        ApplyScaleMultiplier();
        return true;
    }

    /// <summary>
    /// summary: 绑定用于控制视觉缩放的目标节点。
    /// param: target 需要被缩放的节点
    /// returns: 绑定成功时返回 true
    /// </summary>
    public bool TryBindSizeTarget(Transform target)
    {
        if (target == null || !IsTransformInsideBullet(target))
        {
            return false;
        }

        sizeTarget = target;
        CaptureCurrentScaleAsBase();
        ApplyScaleMultiplier();
        return true;
    }

    /// <summary>
    /// summary: 绑定用于命中检测的球形触发体。
    /// param: collider 需要绑定的球形碰撞体
    /// returns: 绑定成功时返回 true
    /// </summary>
    public bool TryBindImpactCollider(SphereCollider collider)
    {
        if (collider == null || !IsTransformInsideBullet(collider.transform))
        {
            return false;
        }

        impactCollider = collider;
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        CaptureImpactColliderBaseRadius();
        ApplyImpactColliderScale();
        return true;
    }

    /// <summary>
    /// summary: 用新的攻击配置替换当前子弹持有的 AttackSpec，并按需同步当前运行时状态。
    /// param: newAttackSpec 需要应用的新攻击配置
    /// param: syncCurrentSpeed 是否同时把当前速度同步到攻击配置里的 projectileSpeed
    /// param: syncRemainingLife 是否同时把当前剩余生命同步到攻击配置里的 projectileLife
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetAttackSpec(AttackSpec newAttackSpec, bool syncCurrentSpeed = true, bool syncRemainingLife = true)
    {
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed, syncRemainingLife);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的语义词条，不修改数值参数。
    /// param: coreType 攻击核心类型
    /// param: behaviorType 攻击行为类型
    /// param: valueType 攻击数值类型
    /// param: resultType 攻击结果类型
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetAttackTypes(
        AttackCoreType coreType,
        AttackBehaviorType behaviorType,
        AttackValueType valueType,
        AttackResultType resultType)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.coreType = coreType;
        newAttackSpec.behaviorType = behaviorType;
        newAttackSpec.valueType = valueType;
        newAttackSpec.resultType = resultType;
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: false, syncRemainingLife: false);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的伤害值。
    /// param: newDamage 需要应用的新伤害
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetDamage(float newDamage)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.damage = Mathf.Max(0f, newDamage);
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: false, syncRemainingLife: false);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的投射物数量、反弹、链式和穿透词条。
    /// param: projectileCount 需要应用的投射物数量
    /// param: bounceCount 需要应用的反弹次数
    /// param: chainCount 需要应用的链式次数
    /// param: pierceCount 需要应用的穿透次数
    /// param: syncRemainingLife 是否把当前剩余生命同步到新的 projectileLife
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetProjectileCounts(int projectileCount, int bounceCount, int chainCount, int pierceCount, bool syncRemainingLife = false)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.projectileCount = Mathf.Max(1, projectileCount);
        newAttackSpec.bounceCount = Mathf.Max(0, bounceCount);
        newAttackSpec.chainCount = Mathf.Max(0, chainCount);
        newAttackSpec.pierceCount = Mathf.Max(0, pierceCount);
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: false, syncRemainingLife: syncRemainingLife);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的子弹生命与命中生命消耗。
    /// param: projectileLife 需要应用的子弹生命
    /// param: impactLifeCost 需要应用的单次命中生命消耗
    /// param: syncRemainingLife 是否同时覆盖当前剩余生命
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetLifeSettings(int projectileLife, int impactLifeCost, bool syncRemainingLife = true)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.projectileLife = Mathf.Max(1, projectileLife);
        newAttackSpec.impactLifeCost = Mathf.Max(1, impactLifeCost);
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: false, syncRemainingLife: syncRemainingLife);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的弹速，并按需同步到当前飞行速度。
    /// param: projectileSpeed 需要应用的新弹速
    /// param: syncCurrentSpeed 是否同时覆盖当前 speed
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetProjectileSpeed(float projectileSpeed, bool syncCurrentSpeed = true)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.projectileSpeed = Mathf.Max(0f, projectileSpeed);
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: syncCurrentSpeed, syncRemainingLife: false);
        return true;
    }

    /// <summary>
    /// summary: 更新当前攻击的存活时间、飞行距离和命中层级。
    /// param: maxLifetime 需要应用的最大存活时间
    /// param: maxTravelDistance 需要应用的最大飞行距离
    /// param: impactMask 需要应用的命中层级掩码
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetAttackLifetime(float maxLifetime, float maxTravelDistance, LayerMask impactMask)
    {
        AttackSpec newAttackSpec = attackSpec;
        newAttackSpec.maxLifetime = Mathf.Max(0f, maxLifetime);
        newAttackSpec.maxTravelDistance = Mathf.Max(0f, maxTravelDistance);
        newAttackSpec.impactMask = impactMask;
        ApplyAttackSpec(newAttackSpec, syncCurrentSpeed: false, syncRemainingLife: false);
        return true;
    }

    /// <summary>
    /// summary: 设置子弹显示的字符内容。
    /// param: content 需要显示的文本内容
    /// returns: 成功拿到文字组件并完成赋值时返回 true
    /// </summary>
    public bool TrySetText(string content)
    {
        if (GlyphText == null)
        {
            return false;
        }

        glyphText.text = content;
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 直接设置子弹文字的颜色。
    /// param: color 需要应用的文字颜色
    /// returns: 成功拿到文字组件并完成赋值时返回 true
    /// </summary>
    public bool TrySetTextColor(Color color)
    {
        if (GlyphText == null)
        {
            return false;
        }

        glyphText.color = color;
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 设置文字节点的宽高尺寸，并默认保持宽高一致；当前 FontSize 语义映射到文字容器尺寸而非 TMP 字号。
    /// param: fontSize 需要应用的文字容器边长
    /// returns: 成功拿到文字节点的 RectTransform 并完成赋值时返回 true
    /// </summary>
    public bool TrySetFontSize(float fontSize)
    {
        if (!TryGetGlyphRectTransform(out RectTransform glyphRectTransform))
        {
            return false;
        }

        float sanitizedSize = Mathf.Max(0f, fontSize);
        glyphRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sanitizedSize);
        glyphRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sanitizedSize);
        ApplyImpactColliderRadiusFromFontSize(sanitizedSize);
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 把当前缩放目标的 localScale 记录为倍率缩放的基准值。
    /// param: 无
    /// returns: 成功拿到缩放目标时返回 true
    /// </summary>
    public bool CaptureCurrentScaleAsBase()
    {
        Transform target = SizeTarget;
        if (target == null)
        {
            return false;
        }

        baseLocalScale = target.localScale;
        hasBaseScaleSnapshot = true;
        return true;
    }

    /// <summary>
    /// summary: 记录当前球形触发体的基础半径，用于后续按倍率同步命中体积。
    /// param: 无
    /// returns: 成功拿到命中触发体时返回 true
    /// </summary>
    public bool CaptureImpactColliderBaseRadius()
    {
        if (impactCollider == null)
        {
            return false;
        }

        baseImpactRadius = impactCollider.radius;
        return true;
    }

    /// <summary>
    /// summary: 设置倍率缩放所依赖的基础 localScale。
    /// param: localScale 需要记录的基础 localScale
    /// returns: 成功拿到缩放目标时返回 true
    /// </summary>
    public bool TrySetBaseLocalScale(Vector3 localScale)
    {
        if (SizeTarget == null)
        {
            return false;
        }

        baseLocalScale = localScale;
        hasBaseScaleSnapshot = true;
        ApplyScaleMultiplier();
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 设置统一的基础缩放值。
    /// param: uniformScale 需要应用的统一基础缩放
    /// returns: 成功拿到缩放目标时返回 true
    /// </summary>
    public bool TrySetBaseUniformScale(float uniformScale)
    {
        return TrySetBaseLocalScale(Vector3.one * Mathf.Max(0f, uniformScale));
    }

    /// <summary>
    /// summary: 应用视觉缩放倍率，并同步刷新命中球半径。
    /// param: multiplier 需要应用的缩放倍率
    /// returns: 成功拿到缩放目标时返回 true
    /// </summary>
    public bool TrySetScaleMultiplier(float multiplier)
    {
        if (SizeTarget == null)
        {
            return false;
        }

        EnsureBaseScaleSnapshot();
        scaleMultiplier = Mathf.Max(0f, multiplier);
        ApplyScaleMultiplier();
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 设置命中球半径的独立倍率，并在视觉缩放之外继续叠加碰撞半径修饰。
    /// param: multiplier 需要应用的命中半径倍率
    /// returns: 成功拿到命中体并完成刷新时返回 true
    /// </summary>
    public bool TrySetImpactRadiusMultiplier(float multiplier)
    {
        if (impactCollider == null)
        {
            return false;
        }

        impactRadiusMultiplier = Mathf.Max(0f, multiplier);
        ApplyImpactColliderScale();
        NotifyVisualPresenterPreview();
        return true;
    }

    /// <summary>
    /// summary: 设置子弹飞行方向并保留当前速度。
    /// param: newDirection 需要应用的新方向
    /// param: directionSpace 输入方向所处的空间
    /// returns: 输入方向非零时返回 true
    /// </summary>
    public bool TrySetDirection(Vector3 newDirection, Space directionSpace = Space.World)
    {
        if (!TryConvertDirectionToStorageSpace(newDirection, directionSpace, out Vector3 storedDirection))
        {
            return false;
        }

        direction = storedDirection;
        return true;
    }

    /// <summary>
    /// summary: 设置子弹速度并保留当前方向。
    /// param: newSpeed 需要应用的新速度
    /// returns: 无条件返回 true
    /// </summary>
    public bool TrySetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);
        return true;
    }

    /// <summary>
    /// summary: 一次性设置子弹方向和速度。
    /// param: newDirection 需要应用的新方向
    /// param: newSpeed 需要应用的新速度
    /// param: directionSpace 输入方向所处的空间
    /// returns: 输入方向非零时返回 true
    /// </summary>
    public bool TrySetDirectionAndSpeed(Vector3 newDirection, float newSpeed, Space directionSpace = Space.World)
    {
        if (!TrySetDirection(newDirection, directionSpace))
        {
            return false;
        }

        speed = Mathf.Max(0f, newSpeed);
        return true;
    }

    /// <summary>
    /// summary: 按输入速度向量推导方向和速度标量。
    /// param: velocity 需要应用的速度向量
    /// param: velocitySpace 输入速度所处的空间
    /// returns: 输入速度非零时返回 true
    /// </summary>
    public bool TrySetVelocity(Vector3 velocity, Space velocitySpace = Space.World)
    {
        float magnitude = velocity.magnitude;
        speed = magnitude;
        if (magnitude * magnitude <= MinimumVectorSqrMagnitude)
        {
            return false;
        }

        return TrySetDirection(velocity / magnitude, velocitySpace);
    }

    /// <summary>
    /// summary: 读取当前子弹在指定空间下的速度向量。
    /// param: outputSpace 需要输出的空间
    /// returns: 当前速度向量
    /// </summary>
    public Vector3 GetVelocity(Space outputSpace = Space.World)
    {
        Vector3 storedVelocity = GetStoredVelocity();
        if (movementSpace == outputSpace)
        {
            return storedVelocity;
        }

        Transform target = MovementTarget;
        if (movementSpace == Space.Self && outputSpace == Space.World)
        {
            return target.TransformDirection(storedVelocity);
        }

        if (movementSpace == Space.World && outputSpace == Space.Self)
        {
            return target.InverseTransformDirection(storedVelocity);
        }

        return storedVelocity;
    }

    /// <summary>
    /// summary: 推进一次子弹位移；有刚体时走物理接口，无刚体时直接改 Transform。
    /// param: deltaTime 本次推进使用的时间步长
    /// returns: 成功推进时返回 true
    /// </summary>
    public bool MoveStep(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return false;
        }

        Vector3 worldVelocity = GetVelocity(Space.World);
        if (MovementRigidbody != null)
        {
            if (movementRigidbody.isKinematic)
            {
                movementRigidbody.MovePosition(movementRigidbody.position + worldVelocity * deltaTime);
            }
            else
            {
                movementRigidbody.linearVelocity = worldVelocity;
            }

            return true;
        }

        if (movementSpace == Space.Self)
        {
            MovementTarget.Translate(GetStoredVelocity() * deltaTime, Space.Self);
        }
        else
        {
            MovementTarget.position += worldVelocity * deltaTime;
        }

        return true;
    }

    /// <summary>
    /// summary: 直接设置移动目标的世界坐标。
    /// param: worldPosition 需要应用的世界坐标
    /// returns: 成功完成设置时返回 true
    /// </summary>
    public bool TrySetWorldPosition(Vector3 worldPosition)
    {
        if (MovementRigidbody != null)
        {
            movementRigidbody.position = worldPosition;
            movementRigidbody.transform.position = worldPosition;
            return true;
        }

        MovementTarget.position = worldPosition;
        return true;
    }

    /// <summary>
    /// summary: 直接设置移动目标的局部坐标。
    /// param: localPosition 需要应用的局部坐标
    /// returns: 成功完成设置时返回 true
    /// </summary>
    public bool TrySetLocalPosition(Vector3 localPosition)
    {
        MovementTarget.localPosition = localPosition;
        return true;
    }

    /// <summary>
    /// summary: 按指定空间平移移动目标。
    /// param: translation 需要应用的平移向量
    /// param: relativeTo 平移使用的参考空间
    /// returns: 成功完成平移时返回 true
    /// </summary>
    public bool TryTranslate(Vector3 translation, Space relativeTo = Space.World)
    {
        if (MovementRigidbody != null)
        {
            Vector3 worldTranslation = relativeTo == Space.World
                ? translation
                : MovementTarget.TransformDirection(translation);

            Vector3 nextPosition = movementRigidbody.position + worldTranslation;
            if (movementRigidbody.isKinematic)
            {
                movementRigidbody.MovePosition(nextPosition);
            }
            else
            {
                movementRigidbody.position = nextPosition;
            }

            return true;
        }

        MovementTarget.Translate(translation, relativeTo);
        return true;
    }

    /// <summary>
    /// summary: 扣减当前子弹生命值；生命归零时立即回收。
    /// param: amount 本次需要扣减的生命值
    /// returns: 无
    /// </summary>
    public void ApplyLifeCost(int amount)
    {
        if (!isActiveShot)
        {
            return;
        }

        remainingLife = Mathf.Max(0, remainingLife - Mathf.Max(1, amount));
        if (remainingLife <= 0)
        {
            Expire();
        }
    }

    /// <summary>
    /// summary: 停止当前子弹的运动，并销毁自身。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Expire()
    {
        if (!isActiveShot)
        {
            return;
        }

        Vector3 expirePoint = MovementTarget != null ? MovementTarget.position : transform.position;
        TryExecutePayloadsByTrigger(SpellTriggerType.OnExpire, expirePoint, Damage, null, null, null);
        isActiveShot = false;
        EnableImpactCollider(false);
        TryStopMovement();
        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }

    /// <summary>
    /// summary: 停止当前子弹的线速度和角速度。
    /// param: includeAngularVelocity 是否同时清零角速度
    /// returns: 无条件返回 true
    /// </summary>
    public bool TryStopMovement(bool includeAngularVelocity = true)
    {
        speed = 0f;
        if (MovementRigidbody == null)
        {
            return true;
        }

        if (!movementRigidbody.isKinematic)
        {
            movementRigidbody.linearVelocity = Vector3.zero;
            if (includeAngularVelocity)
            {
                movementRigidbody.angularVelocity = Vector3.zero;
            }
        }

        return true;
    }

    /// <summary>
    /// summary: 判断当前是否处于会冻结战斗子弹的暂停状态。
    /// param: 无
    /// returns: 当存在背包或暂停状态，且当前子弹未声明忽略暂停门控时返回 true
    /// </summary>
    private bool ShouldSuspendForGameplayPause()
    {
        if (ignoreGameplayPauseStatus)
        {
            return false;
        }

        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }

    /// <summary>
    /// summary: 在暂停期间把动态刚体速度清零，避免物理世界继续推进已发射子弹。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void FreezeDynamicRigidbodyVelocity()
    {
        if (MovementRigidbody == null || movementRigidbody.isKinematic)
        {
            return;
        }

        movementRigidbody.linearVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (ShouldSuspendForGameplayPause())
        {
            FreezeDynamicRigidbodyVelocity();
            return;
        }

        TryUpdateHomingDirection(Time.deltaTime);
        if (MovementRigidbody != null)
        {
            RunImpactContactsIfAllowed();
            CheckNonImpactPayloadTriggers();
            if (!autoMove)
            {
                UpdateLifetime(Time.deltaTime);
            }

            return;
        }

        TryUpdateMovementBehavior(Time.deltaTime);
        if (autoMove)
        {
            MoveStep(Time.deltaTime);
        }

        RunImpactContactsIfAllowed();
        CheckNonImpactPayloadTriggers();
        UpdateLifetime(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (ShouldSuspendForGameplayPause())
        {
            FreezeDynamicRigidbodyVelocity();
            return;
        }

        if (MovementRigidbody == null)
        {
            return;
        }

        TryUpdateHomingDirection(Time.fixedDeltaTime);
        TryUpdateMovementBehavior(Time.fixedDeltaTime);

        if (autoMove)
        {
            MoveStep(Time.fixedDeltaTime);
        }

        RunImpactContactsIfAllowed();
        CheckNonImpactPayloadTriggers();
        UpdateLifetime(Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterImpact(other);
    }

    private void Reset()
    {
        TryCacheBindings(overwriteExisting: true);
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        CaptureCurrentScaleAsBase();
        CaptureImpactColliderBaseRadius();
        ApplyScaleMultiplier();
        NotifyVisualPresenterPreview();
    }

    private void OnValidate()
    {
        TryCacheBindings();
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        attackSpec = attackSpec.GetSanitized();
        scaleMultiplier = Mathf.Max(0f, scaleMultiplier);
        speed = Mathf.Max(0f, speed);
        homingTurnSpeedDegrees = Mathf.Max(0f, homingTurnSpeedDegrees);
        if (direction.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            direction.Normalize();
        }

        CaptureImpactColliderBaseRadius();
        ApplyImpactColliderScale();
        QueueEditorVisualPresenterPreview();
    }

    /// <summary>
    /// summary: 按当前生命周期配置检查超时、超距和生命是否耗尽。
    /// param: deltaTime 本次更新使用的时间步长
    /// returns: 无
    /// </summary>
    private void UpdateLifetime(float deltaTime)
    {
        if (!isActiveShot)
        {
            return;
        }

        elapsedLifetime += Mathf.Max(0f, deltaTime);
        if (attackSpec.maxLifetime > 0f && elapsedLifetime >= attackSpec.maxLifetime)
        {
            Expire();
            return;
        }

        if (attackSpec.maxTravelDistance <= 0f)
        {
            return;
        }

        Vector3 distanceVector = MovementTarget.position - spawnWorldPosition;
        if (distanceVector.sqrMagnitude >= attackSpec.maxTravelDistance * attackSpec.maxTravelDistance)
        {
            Expire();
        }
    }

    /// <summary>
    /// summary: 当当前行为为 Homing 时，持续把飞行方向转向最近可命中的目标。
    /// param: deltaTime 本次转向使用的时间步长
    /// returns: 无
    /// </summary>
    private void TryUpdateHomingDirection(float deltaTime)
    {
        if (!isActiveShot || deltaTime <= 0f || !ShouldUseHomingBehavior())
        {
            return;
        }

        float currentTime = Time.time;
        if ((homingTarget == null || !IsHomingTargetValid(homingTarget)) && currentTime < nextHomingTargetRefreshTime)
        {
            nextHomingTargetRefreshTime = currentTime;
        }

        if (currentTime >= nextHomingTargetRefreshTime)
        {
            homingTarget = ResolveHomingTarget();
            nextHomingTargetRefreshTime = currentTime + HomingTargetRefreshIntervalSeconds;
        }

        if (homingTarget == null || !IsHomingTargetValid(homingTarget))
        {
            return;
        }

        Vector3 desiredDirection = homingTarget.position - MovementTarget.position;
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            return;
        }

        Vector3 currentDirection = GetVelocity(Space.World);
        if (currentDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            currentDirection = direction;
        }

        if (currentDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            currentDirection = desiredDirection;
        }

        Vector3 nextDirection = Vector3.RotateTowards(
            currentDirection.normalized,
            desiredDirection.normalized,
            homingTurnSpeedDegrees * Mathf.Deg2Rad * deltaTime,
            0f);
        if (nextDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            return;
        }

        TrySetDirection(nextDirection, Space.World);
        ApplyFacingDirection(nextDirection);
    }

    /// <summary>
    /// summary: 判断当前子弹行为是否声明为 Homing。
    /// param: 无
    /// returns: 当前行为词为 Homing 时返回 true
    /// </summary>
    private bool ShouldUseHomingBehavior()
    {
        return attackSpec.behaviorType == AttackBehaviorType.Homing ||
               currentProjectileNode?.BehaviorType == AttackBehaviorType.Homing;
    }

    private bool ShouldUseStasisBehavior()
    {
        return attackSpec.behaviorType == AttackBehaviorType.Stasis ||
               currentProjectileNode?.BehaviorType == AttackBehaviorType.Stasis;
    }

    private bool ShouldUseSpinBehavior()
    {
        return attackSpec.behaviorType == AttackBehaviorType.Spin ||
               currentProjectileNode?.BehaviorType == AttackBehaviorType.Spin;
    }

    private void ConfigureInitialMovementBehavior()
    {
        if (!isActiveShot)
        {
            return;
        }

        if (ShouldUseSpinBehavior())
        {
            ConfigureSpinBehavior();
            return;
        }

        if (!ShouldUseStasisBehavior())
        {
            return;
        }

        AttackSpec stasisSpec = attackSpec;
        stasisSpec.maxLifetime = Mathf.Max(0f, stasisSpec.behaviorParameter);
        stasisSpec.maxTravelDistance = 0f;
        stasisSpec.projectileSpeed = 0f;
        attackSpec = stasisSpec.GetSanitized();
        autoMove = false;
        TryStopMovement();
        TrySetWorldPosition(spawnWorldPosition);
    }

    private void ConfigureSpinBehavior()
    {
        behaviorSpinRadius = ResolveSpinRadius();
        if (!TryResolveSpinAnchorPosition(expireIfInvalid: false, out behaviorSpinLastAnchorPosition))
        {
            behaviorSpinLastAnchorPosition = spawnWorldPosition;
        }

        behaviorSpinRadialDirection = ResolveSpinRadialDirection(behaviorSpinLastAnchorPosition);
        behaviorSpinAngleDegrees = Vector3.SignedAngle(Vector3.forward, behaviorSpinRadialDirection, Vector3.up);

        Vector3 positiveTangent = Quaternion.AngleAxis(90f, Vector3.up) * behaviorSpinRadialDirection;
        behaviorSpinDirectionSign = Vector3.Dot(positiveTangent, behaviorBaseDirection) >=
                                    Vector3.Dot(-positiveTangent, behaviorBaseDirection)
            ? 1f
            : -1f;

        autoMove = false;
        TryStopMovement();
        TrySetWorldPosition(behaviorSpinLastAnchorPosition + (behaviorSpinRadialDirection * behaviorSpinRadius));
        ApplyFacingDirection(behaviorBaseDirection);
    }

    private void TryUpdateMovementBehavior(float deltaTime)
    {
        if (!isActiveShot || deltaTime <= 0f)
        {
            return;
        }

        switch (attackSpec.behaviorType)
        {
            case AttackBehaviorType.Rush:
                UpdateRushBehavior(deltaTime);
                break;

            case AttackBehaviorType.Slow:
                UpdateSlowBehavior(deltaTime);
                break;

            case AttackBehaviorType.Snake:
                UpdateSnakeBehavior(deltaTime);
                break;

            case AttackBehaviorType.Wander:
                UpdateWanderBehavior(deltaTime);
                break;

            case AttackBehaviorType.Split:
                UpdateSplitBehavior(deltaTime);
                break;

            case AttackBehaviorType.Spin:
                UpdateSpinBehavior(deltaTime);
                break;
        }
    }

    private void UpdateRushBehavior(float deltaTime)
    {
        float strength = Mathf.Max(0f, attackSpec.behaviorParameter);
        float acceleration = behaviorInitialSpeed * BehaviorRushAccelerationScalePerSecond * strength;
        float maxSpeed = behaviorInitialSpeed * BehaviorRushMaxSpeedMultiplier;
        TrySetSpeed(maxSpeed > 0f ? Mathf.Min(maxSpeed, speed + acceleration * deltaTime) : speed);
    }

    private void UpdateSlowBehavior(float deltaTime)
    {
        float strength = Mathf.Max(0f, attackSpec.behaviorParameter);
        float deceleration = behaviorInitialSpeed * BehaviorRushAccelerationScalePerSecond * strength;
        float minSpeed = behaviorInitialSpeed * BehaviorSlowMinSpeedMultiplier;
        TrySetSpeed(Mathf.Max(minSpeed, speed - deceleration * deltaTime));
    }

    private void UpdateSnakeBehavior(float deltaTime)
    {
        float strength = Mathf.Max(0f, attackSpec.behaviorParameter);
        float amplitude = BehaviorSnakeAmplitudeUnits * strength;
        float nextTime = elapsedLifetime + Mathf.Max(0f, deltaTime);
        float lateralVelocity = Mathf.Cos(nextTime * BehaviorSnakeFrequencyHz * Mathf.PI * 2f) *
                                amplitude *
                                BehaviorSnakeFrequencyHz *
                                Mathf.PI * 2f;
        Vector3 nextDirection = (behaviorBaseDirection * Mathf.Max(1f, behaviorInitialSpeed)) +
                                (behaviorRightDirection * lateralVelocity);
        if (nextDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            return;
        }

        TrySetDirection(nextDirection.normalized, Space.World);
        ApplyFacingDirection(nextDirection);
    }

    private void UpdateWanderBehavior(float deltaTime)
    {
        float strength = Mathf.Max(0f, attackSpec.behaviorParameter);
        if (strength <= 0f)
        {
            return;
        }

        float noise = Mathf.PerlinNoise(behaviorWanderSeed, (elapsedLifetime + deltaTime) * 4f);
        float signedNoise = (noise * 2f) - 1f;
        float turnDegrees = signedNoise * BehaviorWanderTurnSpeedDegrees * strength * deltaTime;
        Vector3 currentDirection = GetVelocity(Space.World);
        if (currentDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            currentDirection = direction;
        }

        Vector3 nextDirection = Quaternion.AngleAxis(turnDegrees, Vector3.up) * ResolvePlanarDirection(currentDirection, behaviorBaseDirection);
        TrySetDirection(nextDirection, Space.World);
        ApplyFacingDirection(nextDirection);
    }

    private void UpdateSplitBehavior(float deltaTime)
    {
        int totalCount = behaviorSplitTotalCount > 0
            ? behaviorSplitTotalCount
            : Mathf.Max(1, Mathf.RoundToInt(attackSpec.behaviorParameter));
        if (totalCount <= 0)
        {
            return;
        }

        float splitLifetime = ResolveBehaviorSplitLifetime();
        if (splitLifetime <= 0f)
        {
            return;
        }

        float nextElapsedLifetime = elapsedLifetime + Mathf.Max(0f, deltaTime);
        while (behaviorSplitTriggeredCount < totalCount)
        {
            float triggerTime = splitLifetime * (behaviorSplitTriggeredCount + 1) / (totalCount + 1);
            if (nextElapsedLifetime + 0.0001f < triggerTime)
            {
                break;
            }

            behaviorSplitTriggeredCount++;
            behaviorSplitEmittedCount += TryEmitBehaviorSplitProjectiles();
        }
    }

    private int TryEmitBehaviorSplitProjectiles()
    {
        if (currentProjectileNode == null || behaviorSplitEmittedCount >= MaxBehaviorSplitDerivedProjectileCount)
        {
            return 0;
        }

        CharBullet template = spawnTemplate != null ? spawnTemplate : this;
        SpellProjectileNode childProjectile = SpellProjectileNode.CreateDerivedCoreStatusChild(
            currentProjectileNode,
            attackSpec.damage * BehaviorSplitChildDamageMultiplier);
        if (childProjectile == null || !childProjectile.CanFire)
        {
            return 0;
        }

        Vector3 baseDirection = ResolvePlanarDirection(GetVelocity(Space.World), direction);
        Vector3 spawnPoint = MovementTarget.position;
        int emittedCount = 0;
        int remainingBudget = Mathf.Max(0, MaxBehaviorSplitDerivedProjectileCount - behaviorSplitEmittedCount);
        for (int i = 0; i < BehaviorSplitChildrenPerTrigger && emittedCount < remainingBudget; i++)
        {
            float angle = i == 0 ? -BehaviorSplitForkAngleDegrees : BehaviorSplitForkAngleDegrees;
            Vector3 splitDirection = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
            Vector3 spawnOffset = splitDirection * Mathf.Max(ResolveImpactRadius() + 0.05f, 0.1f);
            List<CharBullet> spawnedBullets = new();
            emittedCount += AttackProjectileEmitter.Emit(
                template,
                ownerRoot != null ? ownerRoot : transform,
                spawnPoint + spawnOffset,
                splitDirection,
                childProjectile,
                targetPolicy,
                null,
                spawnedBullets);

            for (int bulletIndex = 0; bulletIndex < spawnedBullets.Count; bulletIndex++)
            {
                CharBullet spawnedBullet = spawnedBullets[bulletIndex];
                if (spawnedBullet == null)
                {
                    continue;
                }

                spawnedBullet.SetIgnoreGameplayPauseStatus(ignoreGameplayPauseStatus);
            }
        }

        return emittedCount;
    }

    private float ResolveBehaviorSplitLifetime()
    {
        if (attackSpec.maxLifetime > 0f)
        {
            return attackSpec.maxLifetime;
        }

        if (attackSpec.maxTravelDistance > 0f && behaviorInitialSpeed > 0f)
        {
            return attackSpec.maxTravelDistance / behaviorInitialSpeed;
        }

        return 0f;
    }

    private void UpdateSpinBehavior(float deltaTime)
    {
        if (!TryResolveSpinAnchorPosition(expireIfInvalid: true, out Vector3 anchor))
        {
            return;
        }

        Vector3 currentPosition = MovementTarget.position;
        if (behaviorSpinRadius <= 0f)
        {
            Vector3 anchorVelocity = (anchor - currentPosition) / deltaTime;
            TrySetWorldPosition(anchor);
            TrySetVelocity(anchorVelocity, Space.World);
            ApplyFacingDirection(anchorVelocity.sqrMagnitude > MinimumVectorSqrMagnitude ? anchorVelocity : behaviorBaseDirection);
            return;
        }

        float angularDegrees = behaviorInitialSpeed > 0f
            ? (behaviorInitialSpeed / behaviorSpinRadius) * Mathf.Rad2Deg
            : 0f;
        behaviorSpinAngleDegrees += angularDegrees * behaviorSpinDirectionSign * deltaTime;
        behaviorSpinRadialDirection = Quaternion.AngleAxis(behaviorSpinAngleDegrees, Vector3.up) * Vector3.forward;

        Vector3 nextPosition = anchor + (behaviorSpinRadialDirection * behaviorSpinRadius);
        Vector3 velocity = (nextPosition - currentPosition) / deltaTime;
        TrySetWorldPosition(nextPosition);
        if (velocity.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            TrySetVelocity(velocity, Space.World);
            ApplyFacingDirection(velocity);
            return;
        }

        Vector3 tangent = Quaternion.AngleAxis(90f * behaviorSpinDirectionSign, Vector3.up) * behaviorSpinRadialDirection;
        TrySetDirection(tangent, Space.World);
        ApplyFacingDirection(tangent);
    }

    private float ResolveSpinRadius()
    {
        return Mathf.Max(0f, attackSpec.behaviorParameter);
    }

    private Vector3 ResolveSpinAnchorPosition()
    {
        TryResolveSpinAnchorPosition(expireIfInvalid: true, out Vector3 anchorPosition);
        return anchorPosition;
    }

    private bool TryResolveSpinAnchorPosition(bool expireIfInvalid, out Vector3 anchorPosition)
    {
        if (movementAnchor != null)
        {
            if (IsMovementAnchorActive())
            {
                behaviorSpinLastAnchorPosition = movementAnchor.position;
                anchorPosition = behaviorSpinLastAnchorPosition;
                return true;
            }

            if (expireWhenMovementAnchorInvalid)
            {
                anchorPosition = behaviorSpinLastAnchorPosition;
                if (expireIfInvalid && isActiveShot)
                {
                    Expire();
                    return false;
                }

                return true;
            }
        }

        if (expireWhenMovementAnchorInvalid && movementAnchor == null)
        {
            anchorPosition = behaviorSpinLastAnchorPosition;
            if (expireIfInvalid && isActiveShot)
            {
                Expire();
                return false;
            }

            return true;
        }

        if (ownerRoot != null)
        {
            behaviorSpinLastAnchorPosition = ownerRoot.position;
            anchorPosition = behaviorSpinLastAnchorPosition;
            return true;
        }

        anchorPosition = behaviorSpinLastAnchorPosition;
        return true;
    }

    private bool IsMovementAnchorActive()
    {
        if (movementAnchor == null)
        {
            return false;
        }

        if (movementAnchor.TryGetComponent(out CharBullet anchorBullet))
        {
            return anchorBullet.IsActiveShot && anchorBullet.gameObject.activeInHierarchy;
        }

        return movementAnchor.gameObject.activeInHierarchy;
    }

    private Vector3 ResolveSpinRadialDirection(Vector3 anchorPosition)
    {
        Vector3 radial = spawnWorldPosition - anchorPosition;
        radial.y = 0f;
        return ResolvePlanarDirection(radial, behaviorBaseDirection);
    }

    private void RunImpactContactsIfAllowed()
    {
        if (!ShouldBeginDirectImpactCheck())
        {
            return;
        }

        try
        {
            CheckImpactContacts();
        }
        finally
        {
            stasisDirectImpactInProgress = false;
        }
    }

    private bool ShouldBeginDirectImpactCheck()
    {
        if (!ShouldUseStasisBehavior())
        {
            return true;
        }

        if (stasisDirectImpactConsumed)
        {
            return false;
        }

        stasisDirectImpactConsumed = true;
        stasisDirectImpactInProgress = true;
        return true;
    }

    /// <summary>
    /// summary: 解析当前 Homing 应追踪的最近合法目标。
    /// param: 无
    /// returns: 找到目标时返回目标 Transform，否则返回 null
    /// </summary>
    private Transform ResolveHomingTarget()
    {
        Transform nearestEnemy = null;
        Transform nearestPlayer = null;
        bool hasEnemy = ShouldDamageEnemies() && TryFindNearestEnemyTarget(out nearestEnemy);
        bool hasPlayer = ShouldDamagePlayer() && TryFindNearestPlayerTarget(out nearestPlayer);
        if (!hasEnemy)
        {
            return hasPlayer ? nearestPlayer : null;
        }

        if (!hasPlayer)
        {
            return nearestEnemy;
        }

        Vector3 currentPosition = MovementTarget.position;
        float enemyDistanceSqr = GetPlanarDistanceSqr(currentPosition, nearestEnemy.position);
        float playerDistanceSqr = GetPlanarDistanceSqr(currentPosition, nearestPlayer.position);
        return playerDistanceSqr < enemyDistanceSqr ? nearestPlayer : nearestEnemy;
    }

    /// <summary>
    /// summary: 按当前目标策略在半径内寻找最近的存活敌人目标。
    /// param: target 输出的最近敌人 Transform
    /// returns: 找到至少一个合法敌人目标时返回 true
    /// </summary>
    private bool TryFindNearestEnemyTarget(out Transform target)
    {
        target = null;
        float searchRadius = ResolveHomingSearchRadius();
        float bestDistanceSqr = float.MaxValue;
        Vector3 currentPosition = MovementTarget.position;
        Collider[] overlaps = Physics.OverlapSphere(currentPosition, searchRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> visitedRoots = new();
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null || overlap.isTrigger || IsOwnedTransform(overlap.transform) || overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            Transform targetRoot = enemy.transform;
            if (targetRoot == null || IsOwnedTransform(targetRoot) || !visitedRoots.Add(targetRoot.GetInstanceID()) || !IsEnemyImpactTarget(overlap, targetRoot, enemy))
            {
                continue;
            }

            float distanceSqr = GetPlanarDistanceSqr(currentPosition, targetRoot.position);
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            target = targetRoot;
        }

        return target != null;
    }

    /// <summary>
    /// summary: 按当前目标策略在半径内寻找最近的存活玩家目标。
    /// param: target 输出的最近玩家 Transform
    /// returns: 找到至少一个合法玩家目标时返回 true
    /// </summary>
    private bool TryFindNearestPlayerTarget(out Transform target)
    {
        target = null;
        float searchRadiusSqr = ResolveHomingSearchRadius() * ResolveHomingSearchRadius();
        float bestDistanceSqr = float.MaxValue;
        Vector3 currentPosition = MovementTarget.position;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.IsDead || player.transform == null || IsOwnedTransform(player.transform))
            {
                continue;
            }

            float distanceSqr = GetPlanarDistanceSqr(currentPosition, player.transform.position);
            if (distanceSqr > searchRadiusSqr || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            target = player.transform;
        }

        return target != null;
    }

    /// <summary>
    /// summary: 判断当前缓存的 Homing 目标在本帧是否仍可追踪。
    /// param: target 当前缓存目标
    /// returns: 目标有效且与当前策略一致时返回 true
    /// </summary>
    private bool IsHomingTargetValid(Transform target)
    {
        if (target == null || IsOwnedTransform(target))
        {
            return false;
        }

        Enemy enemy = target.GetComponent<Enemy>() ?? target.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            return ShouldDamageEnemies() && !enemy.IsDead;
        }

        PlayerHealth player = target.GetComponent<PlayerHealth>() ?? target.GetComponentInParent<PlayerHealth>();
        return player != null && ShouldDamagePlayer() && !player.IsDead;
    }

    /// <summary>
    /// summary: 读取当前 Homing 搜敌半径；优先复用子弹最大射程。
    /// param: 无
    /// returns: 当前 Homing 搜索半径
    /// </summary>
    private float ResolveHomingSearchRadius()
    {
        return attackSpec.maxTravelDistance > 0f ? attackSpec.maxTravelDistance : DefaultHomingSearchRadius;
    }

    /// <summary>
    /// summary: 计算两个世界点的平面距离平方。
    /// param: from 起点
    /// param: to 终点
    /// returns: XZ 平面距离平方值
    /// </summary>
    private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
    {
        float deltaX = to.x - from.x;
        float deltaZ = to.z - from.z;
        return (deltaX * deltaX) + (deltaZ * deltaZ);
    }

    private static Vector3 ResolvePlanarDirection(Vector3 candidate, Vector3 fallback)
    {
        candidate.y = 0f;
        if (candidate.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            return candidate.normalized;
        }

        fallback.y = 0f;
        if (fallback.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private static Vector3 ResolvePlanarRightDirection(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, ResolvePlanarDirection(forward, Vector3.forward));
        return right.sqrMagnitude > MinimumVectorSqrMagnitude ? right.normalized : Vector3.right;
    }

    private static float ResolveBehaviorSeed(Vector3 spawnPosition, Vector3 baseDirection)
    {
        float seed = (spawnPosition.x * 12.9898f) +
                     (spawnPosition.z * 78.233f) +
                     (baseDirection.x * 37.719f) +
                     (baseDirection.z * 11.137f);
        return Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1000f);
    }

    /// <summary>
    /// summary: 在初始化发射时让子弹的朝向与飞行方向保持一致。
    /// param: worldDirection 当前发射使用的世界方向
    /// returns: 无
    /// </summary>
    private void ApplyFacingDirection(Vector3 worldDirection)
    {
        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        if (MovementRigidbody != null)
        {
            movementRigidbody.rotation = targetRotation;
            return;
        }

        MovementTarget.rotation = targetRotation;
    }

    /// <summary>
    /// summary: 忽略当前子弹与发射者根节点下所有碰撞体之间的碰撞。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void IgnoreOwnerCollisions()
    {
        if (ownerRoot == null)
        {
            return;
        }

        Collider[] bulletColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(includeInactive: true);
        for (int bulletIndex = 0; bulletIndex < bulletColliders.Length; bulletIndex++)
        {
            Collider bulletCollider = bulletColliders[bulletIndex];
            if (bulletCollider == null || !bulletCollider.enabled)
            {
                continue;
            }

            for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
            {
                Collider ownerCollider = ownerColliders[ownerIndex];
                if (ownerCollider == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(bulletCollider, ownerCollider, true);
            }
        }
    }

    /// <summary>
    /// summary: 开关当前子弹的命中触发体，避免回收后继续收到命中事件。
    /// param: enabled 目标启用状态
    /// returns: 无
    /// </summary>
    private void EnableImpactCollider(bool enabled)
    {
        if (impactCollider == null)
        {
            return;
        }

        impactCollider.enabled = enabled;
    }

    /// <summary>
    /// summary: 按当前 prefab 结构修正移动目标与刚体绑定；优先适配共享根节点，不默认创建额外刚体。
    /// param: allowFallbackCreation 是否允许在极端缺失绑定时进入最后兜底
    /// returns: 无
    /// </summary>
    private void EnsureCompatiblePhysicsBindings(bool allowFallbackCreation)
    {
        movementTarget = ResolveCompatibleMovementTarget();
        if (IsMovementRigidbodyCompatible(movementRigidbody))
        {
            DisableUnsupportedNestedRigidbodies();
            return;
        }

        Rigidbody preferredRigidbody = FindPreferredMovementRigidbody(MovementTarget);
        if (IsMovementRigidbodyCompatible(preferredRigidbody))
        {
            movementRigidbody = preferredRigidbody;
            DisableUnsupportedNestedRigidbodies();
            return;
        }

        movementRigidbody = null;
        DisableUnsupportedNestedRigidbodies();
        if (allowFallbackCreation && !CanDriveBulletByTransform())
        {
            TryCreateFallbackRootRigidbody();
        }
    }

    /// <summary>
    /// summary: 将当前实例里未被采用的嵌套刚体切到静止状态，避免它们干扰根节点位移。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void DisableUnsupportedNestedRigidbodies()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody candidate = rigidbodies[i];
            if (candidate == null || candidate == movementRigidbody)
            {
                continue;
            }

            if (!candidate.isKinematic)
            {
                candidate.linearVelocity = Vector3.zero;
                candidate.angularVelocity = Vector3.zero;
            }

            candidate.isKinematic = true;
        }
    }

    /// <summary>
    /// summary: 确保命中球使用 Trigger 语义，避免文字子弹被物理碰撞阻挡在原地。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureImpactColliderConfiguration()
    {
        if (impactCollider == null)
        {
            return;
        }

        if (!impactCollider.isTrigger)
        {
            impactCollider.isTrigger = true;
            GameDebug.LogWarningFormat(
                "[CharBullet] impact collider on '{0}' was not Trigger. Forced to Trigger at runtime/editor validation.",
                name);
        }
    }

    /// <summary>
    /// summary: 记录当前命中球中心，供后续 sweep 检测使用。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResetImpactCheckState()
    {
        if (!TryGetImpactSphere(out Vector3 currentCenter, out _))
        {
            hasPreviousImpactCheckCenter = false;
            return;
        }

        previousImpactCheckCenter = currentCenter;
        hasPreviousImpactCheckCenter = true;
    }

    /// <summary>
    /// summary: 不依赖 sibling Rigidbody 的手动命中检测，覆盖当前 prefab 结构下的触发缺失问题。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void CheckImpactContacts()
    {
        if (!isActiveShot || !TryGetImpactSphere(out Vector3 currentCenter, out float radius))
        {
            return;
        }

        if (!hasPreviousImpactCheckCenter)
        {
            previousImpactCheckCenter = currentCenter;
            hasPreviousImpactCheckCenter = true;
            CheckImpactOverlapAt(currentCenter, radius);
            return;
        }

        Vector3 sweepVector = currentCenter - previousImpactCheckCenter;
        float sweepDistance = sweepVector.magnitude;
        if (sweepDistance > 0f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                previousImpactCheckCenter,
                radius,
                sweepVector / sweepDistance,
                sweepDistance,
                attackSpec.impactMask,
                QueryTriggerInteraction.Ignore);

            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, CompareRaycastDistance);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (!TryRegisterImpactInternal(hits[i].collider, hits[i].normal, out bool stopFurtherProcessing))
                    {
                        continue;
                    }

                    if (!isActiveShot || stopFurtherProcessing)
                    {
                        return;
                    }
                }
            }
        }

        if (isActiveShot)
        {
            CheckImpactOverlapAt(currentCenter, radius);
        }

        previousImpactCheckCenter = currentCenter;
        hasPreviousImpactCheckCenter = true;
    }

    /// <summary>
    /// summary: 对当前命中球所在位置做一次重叠检测，补足 sweep 末端和静止状态下的命中。
    /// param: center 当前命中球中心
    /// param: radius 当前命中球半径
    /// returns: 无
    /// </summary>
    private void CheckImpactOverlapAt(Vector3 center, float radius)
    {
        Collider[] overlaps = Physics.OverlapSphere(center, radius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (!TryRegisterImpactInternal(overlaps[i], null, out bool stopFurtherProcessing))
            {
                continue;
            }

            if (!isActiveShot || stopFurtherProcessing)
            {
                return;
            }
        }
    }

    /// <summary>
    /// summary: 统一处理一次有效命中，负责过滤目标、去重、打日志和扣除生命。
    /// param: other 本次检测到的命中碰撞体
    /// returns: 命中被接受并完成处理时返回 true
    /// </summary>
    private bool TryRegisterImpact(Collider other)
    {
        bool isStasisImpact = ShouldUseStasisBehavior();
        if (isStasisImpact && stasisDirectImpactConsumed && !stasisDirectImpactInProgress)
        {
            return false;
        }

        if (isStasisImpact && !stasisDirectImpactInProgress)
        {
            stasisDirectImpactConsumed = true;
            stasisDirectImpactInProgress = true;
        }

        try
        {
            return TryRegisterImpactInternal(other, null, out _);
        }
        finally
        {
            if (isStasisImpact)
            {
                stasisDirectImpactInProgress = false;
            }
        }
    }

    /// <summary>
    /// summary: 统一处理一次有效命中，按环境反弹、敌人/玩家直伤和命中后效果进行分发。
    /// param: other 本次检测到的命中碰撞体
    /// param: impactNormal sweep 命中时可用的法线；重叠检测时可为空
    /// param: stopFurtherProcessing 本次命中是否应立即停止当前帧的剩余命中检测
    /// returns: 命中被接受并完成处理时返回 true
    /// </summary>
    private bool TryRegisterImpactInternal(Collider other, Vector3? impactNormal, out bool stopFurtherProcessing)
    {
        stopFurtherProcessing = false;
        if (!isActiveShot || other == null || other.isTrigger)
        {
            return false;
        }

        if (IsGroundSurfaceImpact(other) ||
            !IsImpactLayerIncluded(other.gameObject.layer) ||
            IsOwnedTransform(other.transform) ||
            other.GetComponentInParent<CharBullet>() != null)
        {
            return false;
        }

        Transform targetRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
        bool isEnvironmentImpact = !HasImpactActor(other);
        bool isValidActorImpact = IsConfiguredActorImpactTarget(other, targetRoot);
        if (!isEnvironmentImpact && !isValidActorImpact)
        {
            return false;
        }

        if (isEnvironmentImpact)
        {
            if (IsPiercingLightCore())
            {
                return TryHandleLightEnvironmentPenetration(other, out stopFurtherProcessing);
            }

            return TryHandleEnvironmentImpact(other, impactNormal, out stopFurtherProcessing);
        }

        int targetRootId = targetRoot.GetInstanceID();
        if (!impactedTargetRoots.Add(targetRootId))
        {
            return false;
        }

        Vector3 impactPoint = GetImpactPoint(other);
        Enemy primaryEnemy = other.GetComponentInParent<Enemy>();
        float directDamage = ResolveDirectDamage(primaryEnemy);
        if (IsPiercingLightCore())
        {
            bool appliedLightDamage = TryApplyLightDirectDamage(other, targetRoot, directDamage);
            DecayLightDamageAfterPenetration();
            stopFurtherProcessing = false;
            return appliedLightDamage;
        }

        bool shouldApplyHealing = ShouldApplyHealingOnImpact();
        bool appliedDirectEffect = TryApplyConfiguredDirectDamage(other, targetRoot, "Direct", directDamage, out Enemy damagedEnemy, out _);
        if (appliedDirectEffect)
        {
            if (shouldApplyHealing)
            {
                TryApplyHealingAreaAt(impactPoint, GetResultEffects().effectRadius, directDamage, targetRoot);
            }
            else
            {
                TryApplyPostHitEffects(other, targetRoot, impactPoint, directDamage, primaryEnemy, damagedEnemy);
            }

            TryExecuteOnHitPayloads(impactPoint, directDamage, other, damagedEnemy != null ? damagedEnemy : primaryEnemy, targetRoot);
        }

        ApplyLifeCost(attackSpec.impactLifeCost);
        stopFurtherProcessing = !isActiveShot;
        return true;
    }

    /// <summary>
    /// summary: 处理环境碰撞；Bounce 行为会反射方向并保留生命，其他行为维持旧的墙体扣命语义。
    /// param: other 本次命中的环境碰撞体
    /// param: impactNormal 命中法线；为空时会自行估算法线
    /// param: stopFurtherProcessing 本次命中是否应立即停止当前帧的剩余命中检测
    /// returns: 环境碰撞被消费时返回 true
    /// </summary>
    private bool TryHandleEnvironmentImpact(Collider other, Vector3? impactNormal, out bool stopFurtherProcessing)
    {
        stopFurtherProcessing = false;
        if (!ShouldBounceOnEnvironment())
        {
            TryApplyCoreWindPressureAt(GetImpactPoint(other));
            TryExecuteOnHitPayloads(GetImpactPoint(other), Damage, null, null, null);
            ApplyLifeCost(attackSpec.impactLifeCost);
            stopFurtherProcessing = true;
            return true;
        }

        if (remainingBounceCount <= 0)
        {
            Expire();
            stopFurtherProcessing = true;
            return true;
        }

        Vector3 bounceNormal = ResolveImpactNormal(other, impactNormal);
        if (bounceNormal.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            Expire();
            stopFurtherProcessing = true;
            return true;
        }

        Vector3 incomingVelocity = GetVelocity(Space.World);
        Vector3 incomingDirection = incomingVelocity.sqrMagnitude > MinimumVectorSqrMagnitude
            ? incomingVelocity.normalized
            : MovementTarget.forward;
        Vector3 reflectedDirection = Vector3.Reflect(incomingDirection, bounceNormal.normalized);
        if (reflectedDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            Expire();
            stopFurtherProcessing = true;
            return true;
        }

        remainingBounceCount = Mathf.Max(0, remainingBounceCount - 1);
        float reboundOffset = Mathf.Max(ResolveImpactRadius() + 0.02f, 0.1f);
        TrySetWorldPosition(GetImpactPoint(other) + (bounceNormal.normalized * reboundOffset));
        TrySetDirectionAndSpeed(reflectedDirection, Mathf.Max(0f, speed), Space.World);
        ApplyFacingDirection(reflectedDirection);
        ResetImpactCheckState();
        stopFurtherProcessing = true;
        return true;
    }

    /// <summary>
    /// summary: 当命中的对象符合当前目标策略时，尝试把给定伤害应用到该 actor 上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中去重使用的根节点
    /// param: damageSource 本次伤害来源，便于区分直击、爆炸和链雷日志
    /// param: damageAmount 本次应结算的实际伤害值
    /// param: damagedEnemy 若成功伤害到敌人，则输出该敌人引用
    /// param: damagedPlayer 若成功伤害到玩家，则输出该生命组件引用
    /// returns: 成功对任一有效 actor 结算伤害时返回 true
    /// </summary>
    private bool TryApplyConfiguredDirectDamage(
        Collider other,
        Transform targetRoot,
        string damageSource,
        float damageAmount,
        out Enemy damagedEnemy,
        out PlayerHealth damagedPlayer)
    {
        damagedEnemy = null;
        damagedPlayer = null;
        bool damagedAnyTarget = false;
        bool shouldApplyHealing = ShouldApplyHealingOnImpact();
        if (ShouldDamageEnemies())
        {
            damagedAnyTarget |= shouldApplyHealing
                ? TryApplyHealingToEnemy(other, targetRoot, damageSource, damageAmount, out damagedEnemy)
                : TryApplyDamageToEnemy(other, targetRoot, damageSource, damageAmount, out damagedEnemy);
        }

        if (ShouldDamagePlayer())
        {
            damagedAnyTarget |= shouldApplyHealing
                ? TryApplyHealingToPlayer(other, targetRoot, damageSource, damageAmount, out damagedPlayer)
                : TryApplyDamageToPlayer(other, targetRoot, damageSource, damageAmount, out damagedPlayer);
        }

        return damagedAnyTarget;
    }

    /// <summary>
    /// summary: 当命中的对象被标记为敌人且当前策略允许治疗敌人时，尝试把当前子弹治疗量应用到其 Enemy 组件上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// param: healSource 本次治疗来源，便于区分直击与其他效果日志
    /// param: healingAmount 本次应结算的治疗值
    /// param: healedEnemy 若成功造成治疗则输出被命中的敌人组件
    /// returns: 成功对敌人结算治疗时返回 true
    /// </summary>
    private bool TryApplyHealingToEnemy(Collider other, Transform targetRoot, string healSource, float healingAmount, out Enemy healedEnemy)
    {
        healedEnemy = null;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (healingAmount <= 0f || !ShouldDamageEnemies() || !IsEnemyImpactTarget(other, targetRoot, enemy))
        {
            return false;
        }

        if (enemy == null)
        {
            return false;
        }

        string targetName = targetRoot != null ? targetRoot.name : "<destroyed>";
        float previousHealth = enemy.CurrentHealth;
        if (!enemy.TryApplyHealing(healingAmount, out float resultingHealth, out _))
        {
            GameDebug.LogFormat(
                "[CharBullet] Enemy target='{0}' ignored {1} healing={2} health={3}/{4}",
                targetName,
                healSource,
                healingAmount,
                previousHealth,
                enemy.MaxHealth);
            return false;
        }

        healedEnemy = enemy;
        TryNotifyEnemyHealingHit(enemy);
        return true;
    }

    /// <summary>
    /// summary: 当命中的对象被标记为敌人且当前策略允许伤害敌人时，尝试把当前子弹伤害应用到其 Enemy 组件上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// param: damageSource 本次伤害来源，便于区分直击和爆炸日志
    /// param: damageAmount 本次应结算的伤害值
    /// param: damagedEnemy 若成功造成伤害则输出被命中的敌人组件
    /// returns: 成功对敌人结算伤害时返回 true
    /// </summary>
    private bool TryApplyDamageToEnemy(
        Collider other,
        Transform targetRoot,
        string damageSource,
        float damageAmount,
        out Enemy damagedEnemy,
        bool triggerOnKillPayload = true)
    {
        damagedEnemy = null;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (damageAmount <= 0f || !ShouldDamageEnemies() || !IsEnemyImpactTarget(other, targetRoot, enemy))
        {
            return false;
        }

        if (enemy == null)
        {
            GameDebug.LogFormat(
                "[CharBullet] Target '{0}' is tagged as enemy but has no Enemy component.",
                targetRoot.name);
            return false;
        }

        string targetName = targetRoot != null ? targetRoot.name : "<destroyed>";
        Vector3 killPoint = targetRoot != null ? targetRoot.position : (MovementTarget != null ? MovementTarget.position : transform.position);
        float previousHealth = enemy.CurrentHealth;
        float dropChanceMultiplier = ResolveDropChanceMultiplierOnKill();
        if (dropChanceMultiplier > 1f)
        {
            EnemyDropBonusController.RegisterDropChanceMultiplier(enemy, dropChanceMultiplier);
        }

        if (!enemy.TryApplyDamage(damageAmount, out float remainingHealth, out bool isDead))
        {
            EnemyDropBonusController.ClearDropChanceMultiplier(enemy);
            GameDebug.LogFormat(
                "[CharBullet] Enemy target='{0}' ignored {1} damage={2} health={3}/{4}",
                targetName,
                damageSource,
                damageAmount,
                previousHealth,
                enemy.MaxHealth);
            return false;
        }

        // GameDebug.LogFormat(
        //     "[CharBullet] Damaged enemy target='{0}' via {1} collider='{2}' damage={3} health {4}->{5}",
        //     targetRoot.name,
        //     damageSource,
        //     other.name,
        //     Damage,
        //     previousHealth,
        //     remainingHealth);

        if (isDead)
        {
            GameDebug.LogFormat("[CharBullet] Enemy target='{0}' died from {1}.", targetName, damageSource);
            if (triggerOnKillPayload)
            {
                TryExecuteOnKillPayloads(killPoint, damageAmount, other, enemy, targetRoot);
            }
        }
        else if (dropChanceMultiplier > 1f)
        {
            EnemyDropBonusController.ClearDropChanceMultiplier(enemy);
        }

        damagedEnemy = enemy;
        return true;
    }

    private bool TryHandleLightEnvironmentPenetration(Collider other, out bool stopFurtherProcessing)
    {
        stopFurtherProcessing = false;
        if (other == null || !penetratedEnvironmentColliders.Add(other.GetInstanceID()))
        {
            return false;
        }

        DecayLightDamageAfterPenetration();
        return true;
    }

    private bool TryApplyLightDirectDamage(Collider other, Transform targetRoot, float directDamage)
    {
        bool damagedAnyTarget = false;
        if (ShouldDamageEnemies())
        {
            damagedAnyTarget |= TryApplyDamageToEnemy(other, targetRoot, "LightDirect", directDamage, out _, triggerOnKillPayload: false);
        }

        if (ShouldDamagePlayer())
        {
            damagedAnyTarget |= TryApplyDamageToPlayer(other, targetRoot, "LightDirect", directDamage, out _);
        }

        return damagedAnyTarget;
    }

    private void DecayLightDamageAfterPenetration()
    {
        CoreEffectPayload coreEffects = GetCoreEffects();
        float multiplier = coreEffects.penetrationDamageMultiplier > 0f ? coreEffects.penetrationDamageMultiplier : 1f;
        AttackSpec decayedSpec = attackSpec;
        decayedSpec.damage = Mathf.Max(0f, decayedSpec.damage * multiplier);
        attackSpec = decayedSpec.GetSanitized();
    }

    private float ResolveDropChanceMultiplierOnKill()
    {
        if (currentProjectileNode == null)
        {
            return 1f;
        }

        return currentProjectileNode.RuntimeModifiers.GetSanitized().dropChanceMultiplierOnKill;
    }

    /// <summary>
    /// summary: 当命中的对象拥有 PlayerHealth 且当前策略允许伤害玩家时，尝试把当前子弹伤害应用到其生命组件上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// param: damageSource 本次伤害来源，便于区分直击和爆炸日志
    /// param: damageAmount 本次应结算的伤害值
    /// param: damagedPlayer 若成功造成伤害则输出被命中的玩家生命组件
    /// returns: 成功对玩家结算伤害时返回 true
    /// </summary>
    private bool TryApplyDamageToPlayer(Collider other, Transform targetRoot, string damageSource, float damageAmount, out PlayerHealth damagedPlayer)
    {
        damagedPlayer = null;
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (damageAmount <= 0f || !ShouldDamagePlayer() || playerHealth == null)
        {
            return false;
        }

        string targetName = targetRoot != null ? targetRoot.name : "<destroyed>";
        float previousHealth = playerHealth.CurrentHealth;
        if (!playerHealth.TryApplyDamage(damageAmount, out float remainingHealth, out bool isDead))
        {
            GameDebug.LogFormat(
                "[CharBullet] Player target='{0}' ignored {1} damage={2} health={3}/{4}",
                targetName,
                damageSource,
                damageAmount,
                previousHealth,
                playerHealth.MaxHealth);
            return false;
        }

        if (isDead)
        {
            GameDebug.LogFormat("[CharBullet] Player target='{0}' died from {1}.", targetName, damageSource);
        }

        damagedPlayer = playerHealth;
        return true;
    }

    /// <summary>
    /// summary: 当命中的对象拥有 PlayerHealth 且当前策略允许治疗玩家时，尝试把当前子弹治疗值应用到其生命组件上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// param: healSource 本次治疗来源，便于区分直击与其他效果日志
    /// param: healingAmount 本次应结算的治疗值
    /// param: healedPlayer 若成功造成治疗则输出被命中的玩家生命组件
    /// returns: 成功对玩家结算治疗时返回 true
    /// </summary>
    private bool TryApplyHealingToPlayer(Collider other, Transform targetRoot, string healSource, float healingAmount, out PlayerHealth healedPlayer)
    {
        healedPlayer = null;
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (healingAmount <= 0f || !ShouldDamagePlayer() || playerHealth == null)
        {
            return false;
        }

        string targetName = targetRoot != null ? targetRoot.name : "<destroyed>";
        float previousHealth = playerHealth.CurrentHealth;
        if (!playerHealth.TryApplyHealing(healingAmount, out float resultingHealth, out _))
        {
            GameDebug.LogFormat(
                "[CharBullet] Player target='{0}' ignored {1} healing={2} health={3}/{4}",
                targetName,
                healSource,
                healingAmount,
                previousHealth,
                playerHealth.MaxHealth);
            return false;
        }

        healedPlayer = playerHealth;
        return true;
    }

    /// <summary>
    /// summary: 在一次有效 actor 命中后按结果词和核心词顺序结算爆炸、分裂、状态与链雷等二级效果。
    /// param: targetRoot 当前主命中的目标根节点
    /// param: impactPoint 当前命中的世界位置
    /// param: directDamage 当前直击实际造成的伤害值
    /// param: primaryEnemy 当前主命中在受伤前解析到的 Enemy
    /// param: damagedEnemy 当前主命中若为敌人且仍存活则输出对应 Enemy
    /// returns: 无
    /// </summary>
    private void TryApplyPostHitEffects(Collider impactCollider, Transform targetRoot, Vector3 impactPoint, float directDamage, Enemy primaryEnemy, Enemy damagedEnemy)
    {
        ResultEffectPayload resultEffects = GetResultEffects();
        if (IsCurrentResultType(AttackResultType.Confuse))
        {
            TryApplyRandomResultEffect(impactCollider, targetRoot, impactPoint, directDamage, primaryEnemy, damagedEnemy, resultEffects);
        }

        TryApplyExplosionDamageAt(impactPoint, directDamage);
        TryEmitSplitProjectiles(impactPoint, targetRoot, directDamage);
        TryApplyDrainToOwner(directDamage, resultEffects);
        TryApplyShieldToOwner(directDamage, resultEffects);
        TrySpawnLingeringAreaAt(impactPoint, directDamage, resultEffects);
        TryApplyDisplacementAt(impactPoint, resultEffects);
        TryApplyCoreWindPressureAt(impactPoint);
        if (primaryEnemy != null)
        {
            TryApplyThunderChain(targetRoot);
            TryApplyBehaviorChain(targetRoot, directDamage);
        }

        if (damagedEnemy == null || damagedEnemy.Equals(null))
        {
            return;
        }

        TryApplyCoreEnemyEffects(damagedEnemy);
        TryApplyResultEnemyEffects(damagedEnemy, impactPoint);
    }

    private int TryApplyRandomResultEffect(
        Collider impactCollider,
        Transform targetRoot,
        Vector3 impactPoint,
        float directDamage,
        Enemy primaryEnemy,
        Enemy damagedEnemy,
        ResultEffectPayload confuseEffects,
        int remainingBudget = MaxPayloadDerivedProjectileCount)
    {
        if (!confuseEffects.HasRandomResultCandidates ||
            !TryResolveRandomResultCandidate(confuseEffects, out RandomResultCandidatePayload candidate))
        {
            return 0;
        }

        return TryApplyExplicitResultEffect(
            candidate.resultType,
            candidate.resultEffects,
            impactCollider,
            targetRoot,
            impactPoint,
            directDamage,
            primaryEnemy,
            damagedEnemy,
            remainingBudget);
    }

    private bool TryResolveRandomResultCandidate(
        ResultEffectPayload confuseEffects,
        out RandomResultCandidatePayload candidate)
    {
        candidate = default;
        RandomResultCandidatePayload[] candidates = confuseEffects.randomResultCandidates;
        if (candidates == null || candidates.Length <= 0)
        {
            return false;
        }

        int rawIndex = ConfuseCandidateIndexResolver != null
            ? ConfuseCandidateIndexResolver(candidates.Length)
            : UnityEngine.Random.Range(0, candidates.Length);
        int resolvedIndex = Mathf.Clamp(rawIndex, 0, candidates.Length - 1);
        candidate = candidates[resolvedIndex].GetSanitized();
        return candidate.IsValid;
    }

    private int TryApplyExplicitResultEffect(
        AttackResultType resultType,
        ResultEffectPayload resultEffects,
        Collider impactCollider,
        Transform targetRoot,
        Vector3 impactPoint,
        float directDamage,
        Enemy primaryEnemy,
        Enemy damagedEnemy,
        int remainingBudget)
    {
        resultEffects = resultEffects.GetSanitized();
        if (resultEffects.HasStatusApplications)
        {
            if (resultEffects.effectRadius > 0f)
            {
                TryApplyStatusApplicationsAreaAt(impactPoint, resultEffects.effectRadius, resultEffects.statusApplications, null);
            }
            else
            {
                TryApplyStatusApplicationsToEnemy(damagedEnemy, resultEffects.statusApplications);
            }
        }

        switch (resultType)
        {
            case AttackResultType.Explosion:
                TryApplyExplosionDamageAt(impactPoint, directDamage, resultEffects);
                break;
            case AttackResultType.Split:
                return TryEmitSplitProjectiles(
                    impactPoint,
                    targetRoot,
                    directDamage,
                    resultEffects,
                    currentProjectileNode,
                    remainingBudget);
            case AttackResultType.Healing:
                TryApplyPayloadHealingEffect(
                    impactCollider,
                    damagedEnemy != null && !damagedEnemy.Equals(null) ? damagedEnemy : primaryEnemy,
                    targetRoot,
                    impactPoint,
                    directDamage * resultEffects.healingMultiplier,
                    resultEffects.effectRadius);
                break;
            case AttackResultType.StatusEffect:
                if (resultEffects.HasControl)
                {
                    if (resultEffects.effectRadius > 0f)
                    {
                        TryApplyControlAreaAt(impactPoint, resultEffects.effectRadius, resultEffects, null);
                    }
                    else
                    {
                        TryApplyControlToEnemy(damagedEnemy, resultEffects);
                    }
                }

                break;
            case AttackResultType.Drain:
                TryApplyDrainToOwner(directDamage, resultEffects, resultType);
                break;
            case AttackResultType.Shield:
                TryApplyShieldToOwner(directDamage, resultEffects, resultType);
                break;
            case AttackResultType.Leave:
                TrySpawnLingeringAreaAt(impactPoint, directDamage, resultEffects, resultType);
                break;
            case AttackResultType.Push:
            case AttackResultType.Pull:
                TryApplyDisplacementAt(impactPoint, resultEffects, resultType);
                break;
        }

        return 0;
    }

    /// <summary>
    /// summary: 若当前核心词声明了 burn 或 slow，则把这些效果结算到当前命中的敌人控制器上。
    /// param: enemy 当前主命中的敌人
    /// returns: 无
    /// </summary>
    private void TryApplyCoreEnemyEffects(Enemy enemy)
    {
        if (enemy == null || enemy.Equals(null))
        {
            return;
        }

        if (!enemy.TryGetComponent(out EnemyStatusEffectController statusController))
        {
            return;
        }

        CoreEffectPayload coreEffects = GetCoreEffects();
        if (coreEffects.HasBurn)
        {
            statusController.RegisterFireHit(coreEffects.burnTriggerCount, coreEffects.burnDamagePerSecond, coreEffects.burnDuration);
        }

        if (coreEffects.HasSlow)
        {
            statusController.ApplySlow(coreEffects.slowPercent, coreEffects.slowDuration);
        }

        if (coreEffects.HasStatusApplications)
        {
            statusController.TryApplyStatusApplications(coreEffects.statusApplications);
        }
    }

    /// <summary>
    /// summary: 若当前结果词声明了控制阈值，则把本次命中计入目标敌人的控制控制器。
    /// param: enemy 当前主命中的敌人
    /// returns: 无
    /// </summary>
    private void TryApplyResultEnemyEffects(Enemy enemy, Vector3 impactPoint)
    {
        if (enemy == null || enemy.Equals(null))
        {
            return;
        }

        ResultEffectPayload resultEffects = GetResultEffects();
        if (resultEffects.HasStatusApplications)
        {
            TryApplyStatusApplicationsToEnemy(enemy, resultEffects.statusApplications);
        }

        if (!ShouldTriggerControl())
        {
            return;
        }

        if (resultEffects.effectRadius > 0f)
        {
            TryApplyControlAreaAt(impactPoint, resultEffects.effectRadius, resultEffects, null);
            return;
        }

        TryApplyControlToEnemy(enemy, resultEffects);
    }

    private void TryApplyDrainToOwner(float directDamage, ResultEffectPayload resultEffects, AttackResultType resultType = AttackResultType.None)
    {
        if (!IsResultType(AttackResultType.Drain, resultType) || directDamage <= 0f)
        {
            return;
        }

        float healingAmount = directDamage * ResultDrainHealingMultiplier * Mathf.Max(0f, resultEffects.effectStrength);
        if (healingAmount <= 0f || ownerRoot == null)
        {
            return;
        }

        PlayerHealth ownerPlayer = ownerRoot.GetComponentInParent<PlayerHealth>();
        ownerPlayer ??= ownerRoot.GetComponentInChildren<PlayerHealth>();
        if (ownerPlayer != null)
        {
            ownerPlayer.TryApplyHealing(healingAmount, out _, out _);
            return;
        }

        Enemy ownerEnemy = ownerRoot.GetComponentInParent<Enemy>();
        ownerEnemy ??= ownerRoot.GetComponentInChildren<Enemy>();
        if (ownerEnemy != null && !ownerEnemy.Equals(null))
        {
            ownerEnemy.TryApplyHealing(healingAmount, out _, out _);
        }
    }

    private void TryApplyShieldToOwner(float directDamage, ResultEffectPayload resultEffects, AttackResultType resultType = AttackResultType.None)
    {
        if (!IsResultType(AttackResultType.Shield, resultType) || directDamage <= 0f || ownerRoot == null)
        {
            return;
        }

        float shieldAmount = directDamage * ResultShieldAmountMultiplier * Mathf.Max(0f, resultEffects.effectStrength);
        float shieldDuration = Mathf.Max(0f, resultEffects.shieldDuration);
        if (shieldAmount <= 0f || shieldDuration <= 0f)
        {
            return;
        }

        Transform shieldRoot = ResolveOwnerHealthRoot();
        DamageShieldController shield = DamageShieldController.GetOrAdd(shieldRoot);
        shield?.AddShield(shieldAmount, shieldDuration);
    }

    private void TrySpawnLingeringAreaAt(Vector3 impactPoint, float directDamage, ResultEffectPayload resultEffects, AttackResultType resultType = AttackResultType.None)
    {
        if (!IsResultType(AttackResultType.Leave, resultType) || !resultEffects.HasLingeringArea)
        {
            return;
        }

        float tickDamage = directDamage * resultEffects.areaDamageMultiplier;
        LingeringResultAreaRuntime.Spawn(
            impactPoint,
            resultEffects.effectDuration,
            resultEffects.areaTickSeconds,
            resultEffects.effectRadius,
            tickDamage,
            attackSpec.impactMask,
            targetPolicy,
            ownerRoot,
            GetCoreEffects());
    }

    private int TryApplyDisplacementAt(Vector3 impactPoint, ResultEffectPayload resultEffects, AttackResultType resultType = AttackResultType.None)
    {
        bool isPush = IsResultType(AttackResultType.Push, resultType);
        bool isPull = IsResultType(AttackResultType.Pull, resultType);
        if ((!isPush && !isPull) || !resultEffects.HasDisplacement || !ShouldDamageEnemies())
        {
            return 0;
        }

        return TryApplyEnemyDisplacementArea(
            impactPoint,
            resultEffects.effectRadius,
            ResultDisplacementDistancePerStrength * Mathf.Max(0f, resultEffects.effectStrength),
            Mathf.Max(0f, resultEffects.effectStrength),
            isPull);
    }

    private int TryApplyCoreWindPressureAt(Vector3 impactPoint)
    {
        CoreEffectPayload coreEffects = GetCoreEffects();
        if (!coreEffects.HasWindPressure || !ShouldDamageEnemies())
        {
            return 0;
        }

        return TryApplyEnemyDisplacementArea(
            impactPoint,
            coreEffects.windPressureRadius,
            coreEffects.windPressureDistance,
            coreEffects.windDisplacementWeightLimit,
            pullTowardImpact: false);
    }

    private int TryApplyEnemyDisplacementArea(
        Vector3 impactPoint,
        float effectRadius,
        float displacementDistance,
        float maxDisplacementWeight,
        bool pullTowardImpact)
    {
        if (effectRadius <= 0f || displacementDistance <= 0f || !ShouldDamageEnemies())
        {
            return 0;
        }

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, effectRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> displacedRoots = new();
        int displacedCount = 0;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (overlapRoot == null ||
                enemy == null ||
                enemy.Equals(null) ||
                enemy.DisplacementWeight > maxDisplacementWeight ||
                !displacedRoots.Add(overlapRoot.GetInstanceID()) ||
                !IsEnemyImpactTarget(overlap, overlapRoot, enemy))
            {
                continue;
            }

            Vector3 planarOffset = overlapRoot.position - impactPoint;
            planarOffset.y = 0f;
            Vector3 directionFromImpact = planarOffset.sqrMagnitude > MinimumVectorSqrMagnitude
                ? planarOffset.normalized
                : Vector3.forward;
            Vector3 displacementDirection = pullTowardImpact ? -directionFromImpact : directionFromImpact;
            float appliedDistance = displacementDistance;
            if (pullTowardImpact)
            {
                appliedDistance = Mathf.Min(displacementDistance, Mathf.Max(0f, planarOffset.magnitude - 0.1f));
            }

            if (appliedDistance <= 0f)
            {
                continue;
            }

            Vector3 targetPosition = overlapRoot.position + (displacementDirection * appliedDistance);
            targetPosition.y = overlapRoot.position.y;
            ApplyEnemyDisplacement(overlapRoot, targetPosition);
            displacedCount++;
        }

        return displacedCount;
    }

    private Transform ResolveOwnerHealthRoot()
    {
        if (ownerRoot == null)
        {
            return null;
        }

        PlayerHealth ownerPlayer = ownerRoot.GetComponentInParent<PlayerHealth>();
        if (ownerPlayer != null)
        {
            return ownerPlayer.transform;
        }

        ownerPlayer = ownerRoot.GetComponentInChildren<PlayerHealth>();
        if (ownerPlayer != null)
        {
            return ownerPlayer.transform;
        }

        Enemy ownerEnemy = ownerRoot.GetComponentInParent<Enemy>();
        if (ownerEnemy != null && !ownerEnemy.Equals(null))
        {
            return ownerEnemy.transform;
        }

        ownerEnemy = ownerRoot.GetComponentInChildren<Enemy>();
        return ownerEnemy != null && !ownerEnemy.Equals(null) ? ownerEnemy.transform : null;
    }

    private static void ApplyEnemyDisplacement(Transform enemyRoot, Vector3 worldPosition)
    {
        if (enemyRoot == null)
        {
            return;
        }

        Rigidbody rigidbody = enemyRoot.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.position = worldPosition;
            rigidbody.transform.position = worldPosition;
            rigidbody.linearVelocity = Vector3.zero;
            return;
        }

        enemyRoot.position = worldPosition;
    }

    private bool TryApplyStatusApplicationsToEnemy(Enemy enemy, SpellStatusApplication[] applications)
    {
        if (enemy == null || enemy.Equals(null) || applications == null || applications.Length <= 0)
        {
            return false;
        }

        return enemy.TryGetComponent(out EnemyStatusEffectController statusController) &&
               statusController.TryApplyStatusApplications(applications) > 0;
    }

    /// <summary>
    /// summary: Thunder 核心命中主目标后，对附近一个额外敌人补一段固定伤害且不递归触发其他效果。
    /// param: primaryRoot 当前主命中的敌人根节点
    /// returns: 无
    /// </summary>
    private void TryApplyThunderChain(Transform primaryRoot)
    {
        CoreEffectPayload coreEffects = GetCoreEffects();
        if (primaryRoot == null || !coreEffects.HasThunderChain)
        {
            return;
        }

        Collider[] overlaps = Physics.OverlapSphere(primaryRoot.position, coreEffects.thunderChainRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> visitedRoots = new();
        int primaryRootId = primaryRoot.GetInstanceID();
        Enemy bestEnemy = null;
        Collider bestCollider = null;
        Transform bestRoot = null;
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Enemy chainedEnemy = overlap.GetComponentInParent<Enemy>();
            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            int overlapRootId = overlapRoot.GetInstanceID();
            if (chainedEnemy == null ||
                overlapRootId == primaryRootId ||
                !visitedRoots.Add(overlapRootId) ||
                !IsEnemyImpactTarget(overlap, overlapRoot, chainedEnemy))
            {
                continue;
            }

            float distanceSqr = (overlapRoot.position - primaryRoot.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestEnemy = chainedEnemy;
            bestCollider = overlap;
            bestRoot = overlapRoot;
        }

        if (bestEnemy == null || bestCollider == null)
        {
            return;
        }

        TryApplyDamageToEnemy(bestCollider, bestRoot, "ThunderChain", coreEffects.thunderChainDamage, out _);
    }

    /// <summary>
    /// summary: Chain 行为命中后向附近未命中过的敌人传导 50% 直伤；不触发 payload，也不递归派生。
    /// param: primaryRoot 当前主命中的敌人根节点
    /// param: directDamage 当前主命中实际造成的直伤
    /// returns: 无
    /// </summary>
    private void TryApplyBehaviorChain(Transform primaryRoot, float directDamage)
    {
        if (primaryRoot == null ||
            attackSpec.behaviorType != AttackBehaviorType.Chain ||
            attackSpec.chainCount <= 0 ||
            directDamage <= 0f)
        {
            return;
        }

        HashSet<int> visitedRoots = new();
        visitedRoots.Add(primaryRoot.GetInstanceID());

        Transform currentRoot = primaryRoot;
        float chainDamage = directDamage * 0.5f;
        int remainingChains = Mathf.Max(0, attackSpec.chainCount);
        while (remainingChains > 0 &&
               TryResolveNearestBehaviorChainTarget(
                   currentRoot.position,
                   visitedRoots,
                   out Collider chainCollider,
                   out Transform chainRoot))
        {
            visitedRoots.Add(chainRoot.GetInstanceID());
            TryApplyDamageToEnemy(chainCollider, chainRoot, "BehaviorChain", chainDamage, out _, triggerOnKillPayload: false);
            currentRoot = chainRoot;
            remainingChains--;
        }
    }

    private bool TryResolveNearestBehaviorChainTarget(
        Vector3 searchCenter,
        HashSet<int> visitedRoots,
        out Collider bestCollider,
        out Transform bestRoot)
    {
        bestCollider = null;
        bestRoot = null;
        if (visitedRoots == null || !ShouldDamageEnemies())
        {
            return false;
        }

        Collider[] overlaps = Physics.OverlapSphere(searchCenter, DefaultBehaviorChainRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Enemy chainedEnemy = overlap.GetComponentInParent<Enemy>();
            Transform overlapRoot = chainedEnemy != null
                ? chainedEnemy.transform
                : (overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root);
            if (chainedEnemy == null ||
                chainedEnemy.IsDead ||
                overlapRoot == null ||
                visitedRoots.Contains(overlapRoot.GetInstanceID()) ||
                !IsEnemyImpactTarget(overlap, overlapRoot, chainedEnemy))
            {
                continue;
            }

            float distanceSqr = GetPlanarDistanceSqr(searchCenter, overlapRoot.position);
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestCollider = overlap;
            bestRoot = overlapRoot;
        }

        return bestCollider != null && bestRoot != null;
    }

    /// <summary>
    /// summary: 若当前编译结果带有爆炸语义，则在命中点附近再做一次 AoE 伤害结算。
    /// param: impactPoint 当前命中的世界位置
    /// param: directDamage 当前直击实际造成的伤害值
    /// returns: 无
    /// </summary>
    private void TryApplyExplosionDamageAt(Vector3 impactPoint, float directDamage)
    {
        if (!ShouldTriggerExplosion())
        {
            return;
        }

        ResultEffectPayload resultEffects = GetResultEffects();
        TryApplyExplosionDamageAt(impactPoint, directDamage, resultEffects, GetExplosionRadius());
    }

    private void TryApplyExplosionDamageAt(Vector3 impactPoint, float directDamage, ResultEffectPayload resultEffects)
    {
        TryApplyExplosionDamageAt(impactPoint, directDamage, resultEffects, resultEffects.explosionRadius);
    }

    private void TryApplyExplosionDamageAt(Vector3 impactPoint, float directDamage, ResultEffectPayload resultEffects, float explosionRadius)
    {
        resultEffects = resultEffects.GetSanitized();
        float explosionDamage = directDamage * resultEffects.explosionDamageMultiplier;
        if (explosionRadius <= 0f || explosionDamage <= 0f)
        {
            return;
        }

        float explosionDelaySeconds = resultEffects.explosionDelaySeconds;
        if (explosionDelaySeconds > 0f)
        {
            if (TrySpawnDelayedExplosionAt(impactPoint, explosionRadius, explosionDamage, explosionDelaySeconds))
            {
                return;
            }
        }

        TryApplyExplosionDamageImmediately(impactPoint, explosionRadius, explosionDamage);
    }

    private bool TrySpawnDelayedExplosionAt(Vector3 impactPoint, float explosionRadius, float explosionDamage, float explosionDelaySeconds)
    {
        GameObject delayedExplosionObject = new("CharBulletDelayedExplosion");
        DelayedExplosionEffectRuntime delayedExplosionRuntime = delayedExplosionObject.AddComponent<DelayedExplosionEffectRuntime>();
        bool didInitialize = delayedExplosionRuntime.Initialize(
            impactPoint,
            explosionDelaySeconds,
            explosionRadius,
            explosionDamage,
            attackSpec.impactMask,
            targetPolicy,
            ownerRoot,
            DefaultDelayedExplosionIndicatorWidth,
            DefaultDelayedExplosionIndicatorColor,
            DefaultDelayedExplosionIndicatorHeightOffset);
        if (didInitialize)
        {
            return true;
        }

        if (Application.isPlaying)
        {
            Destroy(delayedExplosionObject);
        }
        else
        {
            DestroyImmediate(delayedExplosionObject);
        }

        return false;
    }

    private void TryApplyExplosionDamageImmediately(Vector3 impactPoint, float explosionRadius, float explosionDamage)
    {

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, explosionRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> damagedRoots = new();
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            if (!damagedRoots.Add(overlapRoot.GetInstanceID()))
            {
                continue;
            }

            TryApplyConfiguredDirectDamage(overlap, overlapRoot, "Explosion", explosionDamage, out _, out _);
        }
    }

    private void TryNotifyEnemyControlHit(Enemy enemy)
    {
        if (enemy == null || enemy.Equals(null))
        {
            return;
        }

        if (enemy.TryGetComponent(out EnemyResultVisualFeedback resultVisualFeedback))
        {
            resultVisualFeedback.NotifyControlHitPulse();
        }
    }

    private void TryNotifyEnemyHealingHit(Enemy enemy)
    {
        if (enemy == null || enemy.Equals(null))
        {
            return;
        }

        if (enemy.TryGetComponent(out EnemyResultVisualFeedback resultVisualFeedback))
        {
            resultVisualFeedback.NotifyHealingHitPulse();
        }
    }

    /// <summary>
    /// summary: 若当前结果词带有分裂语义，则在命中点附近按随机方向再次发射子弹，并忽略原命中目标。
    /// param: impactPoint 当前命中的世界位置
    /// param: targetRoot 当前主命中的目标根节点
    /// param: directDamage 当前直击实际造成的伤害值
    /// returns: 无
    /// </summary>
    private void TryEmitSplitProjectiles(Vector3 impactPoint, Transform targetRoot, float directDamage)
    {
        if (!ShouldTriggerSplit())
        {
            return;
        }

        ResultEffectPayload resultEffects = GetResultEffects();
        TryEmitSplitProjectiles(
            impactPoint,
            targetRoot,
            directDamage,
            resultEffects,
            currentProjectileNode,
            int.MaxValue);
    }

    private int TryEmitSplitProjectiles(
        Vector3 impactPoint,
        Transform targetRoot,
        float directDamage,
        ResultEffectPayload resultEffects,
        SpellProjectileNode splitProjectile,
        int remainingBudget)
    {
        if (!resultEffects.HasSplit || remainingBudget <= 0)
        {
            return 0;
        }

        CharBullet template = spawnTemplate != null ? spawnTemplate : this;
        SpellProjectileNode childProjectile = CreateSplitChildProjectile(
            splitProjectile,
            directDamage,
            resultEffects);
        if (childProjectile == null || !childProjectile.CanFire)
        {
            return 0;
        }

        int emittedCount = 0;
        int childProjectileCount = Mathf.Max(1, childProjectile.ProjectileCount);
        int requestedSplitCount = Mathf.Min(resultEffects.splitProjectileCount, remainingBudget / childProjectileCount);
        for (int i = 0; i < requestedSplitCount && emittedCount < remainingBudget; i++)
        {
            Vector3 splitDirection = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
            Vector3 spawnOffset = splitDirection * Mathf.Max(ResolveImpactRadius() + 0.05f, 0.1f);
            List<CharBullet> spawnedBullets = new();
            emittedCount += AttackProjectileEmitter.Emit(
                template,
                ownerRoot != null ? ownerRoot : transform,
                impactPoint + spawnOffset,
                splitDirection,
                childProjectile,
                targetPolicy,
                null,
                spawnedBullets);

            for (int bulletIndex = 0; bulletIndex < spawnedBullets.Count; bulletIndex++)
            {
                CharBullet spawnedBullet = spawnedBullets[bulletIndex];
                if (spawnedBullet == null)
                {
                    continue;
                }

                spawnedBullet.RegisterIgnoredTargetRoot(targetRoot);
                spawnedBullet.SetIgnoreGameplayPauseStatus(ignoreGameplayPauseStatus);
            }
        }

        return emittedCount;
    }

    private static SpellProjectileNode CreateSplitChildProjectile(
        SpellProjectileNode splitProjectile,
        float directDamage,
        ResultEffectPayload resultEffects)
    {
        if (splitProjectile == null)
        {
            return null;
        }

        float childDamage = Mathf.Max(0f, directDamage * resultEffects.splitDamageMultiplier);
        return SpellProjectileNode.CreateDerivedCoreStatusChild(splitProjectile, childDamage);
    }

    private void TryExecuteOnHitPayloads(
        Vector3 impactPoint,
        float baseDamage,
        Collider payloadCollider,
        Enemy payloadEnemy,
        Transform targetRoot)
    {
        TryExecutePayloadsByTrigger(SpellTriggerType.OnHit, impactPoint, baseDamage, payloadCollider, payloadEnemy, targetRoot);
    }

    private void TryExecuteOnKillPayloads(
        Vector3 killPoint,
        float baseDamage,
        Collider payloadCollider,
        Enemy payloadEnemy,
        Transform targetRoot)
    {
        TryExecutePayloadsByTrigger(SpellTriggerType.OnKill, killPoint, baseDamage, payloadCollider, payloadEnemy, targetRoot);
    }

    private void CheckNonImpactPayloadTriggers()
    {
        if (activePayloads.Count <= 0)
        {
            return;
        }

        Vector3 currentPoint = MovementTarget != null ? MovementTarget.position : transform.position;
        for (int i = 0; i < activePayloads.Count; i++)
        {
            SpellPayloadBlock payload = activePayloads[i];
            if (payload == null || payload.InnerBlock == null || executedOneShotPayloads.Contains(payload))
            {
                continue;
            }

            switch (payload.TriggerType)
            {
                case SpellTriggerType.OnTimer:
                    if (elapsedLifetime >= payload.ParameterValue)
                    {
                        TryExecutePayload(payload, currentPoint, Damage, null, null, null);
                    }
                    break;
                case SpellTriggerType.OnDistance:
                    if ((currentPoint - spawnWorldPosition).magnitude >= payload.ParameterValue)
                    {
                        TryExecutePayload(payload, currentPoint, Damage, null, null, null);
                    }
                    break;
                case SpellTriggerType.OnProximity:
                    if (TryResolveNearestPayloadTarget(
                            payload.ParameterValue,
                            out Collider proximityCollider,
                            out Enemy proximityEnemy,
                            out Transform proximityRoot,
                            out Vector3 proximityPoint))
                    {
                        TryExecutePayload(payload, proximityPoint, Damage, proximityCollider, proximityEnemy, proximityRoot);
                    }
                    break;
            }
        }
    }

    private void TryExecutePayloadsByTrigger(
        SpellTriggerType triggerType,
        Vector3 triggerPoint,
        float baseDamage,
        Collider payloadCollider,
        Enemy payloadEnemy,
        Transform targetRoot)
    {
        if (activePayloads.Count <= 0)
        {
            return;
        }

        int remainingDerivedProjectiles = MaxPayloadDerivedProjectileCount;
        for (int i = 0; i < activePayloads.Count; i++)
        {
            SpellPayloadBlock payload = activePayloads[i];
            if (payload == null || payload.TriggerType != triggerType || payload.InnerBlock == null)
            {
                continue;
            }

            if (triggerType != SpellTriggerType.OnHit && executedOneShotPayloads.Contains(payload))
            {
                continue;
            }

            remainingDerivedProjectiles -= TryExecutePayload(
                payload,
                triggerPoint,
                baseDamage,
                payloadCollider,
                payloadEnemy,
                targetRoot,
                remainingDerivedProjectiles);
            remainingDerivedProjectiles = Mathf.Max(0, remainingDerivedProjectiles);
        }
    }

    private int TryExecutePayload(
        SpellPayloadBlock payload,
        Vector3 triggerPoint,
        float baseDamage,
        Collider payloadCollider,
        Enemy payloadEnemy,
        Transform targetRoot,
        int remainingDerivedProjectiles = MaxPayloadDerivedProjectileCount)
    {
        if (payload == null || payload.InnerBlock == null)
        {
            return 0;
        }

        if (payload.TriggerType != SpellTriggerType.OnHit)
        {
            executedOneShotPayloads.Add(payload);
        }

        SpellCastBlock innerBlock = payload.InnerBlock;
        if (innerBlock.Depth > MaxPayloadDepth)
        {
            GameDebug.LogWarning("[CharBullet] Ignored trigger payload because it exceeds the maximum payload depth.");
            return 0;
        }

        float payloadBaseDamage = baseDamage > 0f ? baseDamage : Damage;
        int emittedProjectiles = TryApplyPayloadEffects(
            innerBlock,
            triggerPoint,
            payloadBaseDamage,
            payloadCollider,
            payloadEnemy,
            targetRoot,
            remainingDerivedProjectiles);
        int remainingBudget = Mathf.Max(0, remainingDerivedProjectiles - emittedProjectiles);
        if (remainingBudget > 0)
        {
            emittedProjectiles += TryEmitPayloadProjectiles(innerBlock, triggerPoint, targetRoot, remainingBudget);
        }

        return emittedProjectiles;
    }

    private bool TryResolveNearestPayloadTarget(
        float radius,
        out Collider payloadCollider,
        out Enemy payloadEnemy,
        out Transform targetRoot,
        out Vector3 triggerPoint)
    {
        payloadCollider = null;
        payloadEnemy = null;
        targetRoot = null;
        triggerPoint = MovementTarget != null ? MovementTarget.position : transform.position;
        if (radius <= 0f)
        {
            return false;
        }

        Collider[] overlaps = Physics.OverlapSphere(triggerPoint, radius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            if (overlapRoot == null ||
                impactedTargetRoots.Contains(overlapRoot.GetInstanceID()) ||
                !IsConfiguredActorImpactTarget(overlap, overlapRoot))
            {
                continue;
            }

            float distanceSqr = (overlapRoot.position - triggerPoint).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            payloadCollider = overlap;
            payloadEnemy = overlap.GetComponentInParent<Enemy>();
            targetRoot = overlapRoot;
        }

        if (payloadCollider == null)
        {
            return false;
        }

        return true;
    }

    private int TryApplyPayloadEffects(
        SpellCastBlock innerBlock,
        Vector3 impactPoint,
        float baseDamage,
        Collider payloadCollider,
        Enemy payloadEnemy,
        Transform targetRoot,
        int remainingProjectileBudget)
    {
        IReadOnlyList<SpellPayloadEffectNode> effects = innerBlock.PayloadEffects;
        int emittedProjectiles = 0;
        for (int i = 0; i < effects.Count; i++)
        {
            SpellPayloadEffectNode effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            ResultEffectPayload resultEffects = effect.ResultEffects;
            if (resultEffects.HasStatusApplications)
            {
                TryApplyPayloadStatusApplications(payloadEnemy, impactPoint, resultEffects);
            }

            if (effect.ResultType == AttackResultType.Confuse)
            {
                int budgetForEffect = Mathf.Max(0, remainingProjectileBudget - emittedProjectiles);
                emittedProjectiles += TryApplyRandomResultEffect(
                    payloadCollider,
                    targetRoot,
                    impactPoint,
                    baseDamage,
                    payloadEnemy,
                    payloadEnemy,
                    resultEffects,
                    budgetForEffect);
                continue;
            }

            if (effect.ResultType == AttackResultType.Explosion)
            {
                float explosionRadius = Mathf.Max(0f, resultEffects.explosionRadius);
                float explosionDamage = Mathf.Max(0f, baseDamage * resultEffects.explosionDamageMultiplier);
                if (explosionRadius > 0f && explosionDamage > 0f)
                {
                    TryApplyExplosionDamageImmediately(impactPoint, explosionRadius, explosionDamage);
                }

                continue;
            }

            if (effect.ResultType == AttackResultType.StatusEffect)
            {
                TryApplyPayloadControlEffect(payloadEnemy, impactPoint, resultEffects);
                continue;
            }

            if (effect.ResultType == AttackResultType.Healing)
            {
                float healingAmount = Mathf.Max(0f, baseDamage * resultEffects.healingMultiplier);
                TryApplyPayloadHealingEffect(payloadCollider, payloadEnemy, targetRoot, impactPoint, healingAmount, resultEffects.effectRadius);
                continue;
            }

            if (effect.ResultType == AttackResultType.Split)
            {
                int budgetForEffect = Mathf.Max(0, remainingProjectileBudget - emittedProjectiles);
                emittedProjectiles += TryEmitSplitProjectiles(
                    impactPoint,
                    targetRoot,
                    baseDamage,
                    resultEffects,
                    currentProjectileNode,
                    budgetForEffect);
                continue;
            }

            if (effect.ResultType == AttackResultType.Drain)
            {
                TryApplyDrainToOwner(baseDamage, resultEffects, effect.ResultType);
                continue;
            }

            if (effect.ResultType == AttackResultType.Shield)
            {
                TryApplyShieldToOwner(baseDamage, resultEffects, effect.ResultType);
                continue;
            }

            if (effect.ResultType == AttackResultType.Leave)
            {
                TrySpawnLingeringAreaAt(impactPoint, baseDamage, resultEffects, effect.ResultType);
                continue;
            }

            if (effect.ResultType == AttackResultType.Push ||
                effect.ResultType == AttackResultType.Pull)
            {
                TryApplyDisplacementAt(impactPoint, resultEffects, effect.ResultType);
            }
        }

        return emittedProjectiles;
    }

    private void TryApplyPayloadStatusApplications(Enemy enemy, Vector3 impactPoint, ResultEffectPayload resultEffects)
    {
        if (!resultEffects.HasStatusApplications)
        {
            return;
        }

        if (resultEffects.effectRadius > 0f)
        {
            TryApplyStatusApplicationsAreaAt(impactPoint, resultEffects.effectRadius, resultEffects.statusApplications, null);
            return;
        }

        TryApplyStatusApplicationsToEnemy(enemy, resultEffects.statusApplications);
    }

    private void TryApplyPayloadControlEffect(Enemy enemy, Vector3 impactPoint, ResultEffectPayload resultEffects)
    {
        if (!resultEffects.HasControl)
        {
            return;
        }

        if (resultEffects.effectRadius > 0f)
        {
            TryApplyControlAreaAt(impactPoint, resultEffects.effectRadius, resultEffects, null);
            return;
        }

        TryApplyControlToEnemy(enemy, resultEffects);
    }

    private bool TryApplyControlToEnemy(Enemy enemy, ResultEffectPayload resultEffects)
    {
        if (enemy == null || enemy.Equals(null) || !resultEffects.HasControl)
        {
            return false;
        }

        TryNotifyEnemyControlHit(enemy);
        if (enemy.TryGetComponent(out EnemyStatusEffectController statusController))
        {
            return statusController.RegisterControlHit(resultEffects.controlTriggerCount, resultEffects.controlDuration);
        }

        return false;
    }

    private int TryApplyControlAreaAt(Vector3 impactPoint, float controlRadius, ResultEffectPayload resultEffects, Transform excludedRoot)
    {
        if (controlRadius <= 0f || !resultEffects.HasControl || !ShouldDamageEnemies())
        {
            return 0;
        }

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, controlRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> controlledRoots = new();
        int controlledCount = 0;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (overlapRoot == null ||
                (excludedRoot != null && overlapRoot == excludedRoot) ||
                !controlledRoots.Add(overlapRoot.GetInstanceID()) ||
                !IsEnemyImpactTarget(overlap, overlapRoot, enemy))
            {
                continue;
            }

            if (TryApplyControlToEnemy(enemy, resultEffects))
            {
                controlledCount++;
            }
        }

        return controlledCount;
    }

    private int TryApplyStatusApplicationsAreaAt(
        Vector3 impactPoint,
        float radius,
        SpellStatusApplication[] applications,
        Transform excludedRoot)
    {
        if (radius <= 0f || applications == null || applications.Length <= 0 || !ShouldDamageEnemies())
        {
            return 0;
        }

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, radius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> affectedRoots = new();
        int affectedCount = 0;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (overlapRoot == null ||
                (excludedRoot != null && overlapRoot == excludedRoot) ||
                !affectedRoots.Add(overlapRoot.GetInstanceID()) ||
                !IsEnemyImpactTarget(overlap, overlapRoot, enemy))
            {
                continue;
            }

            if (TryApplyStatusApplicationsToEnemy(enemy, applications))
            {
                affectedCount++;
            }
        }

        return affectedCount;
    }

    private void TryApplyPayloadHealingEffect(
        Collider payloadCollider,
        Enemy enemy,
        Transform targetRoot,
        Vector3 impactPoint,
        float healingAmount,
        float healingRadius)
    {
        if (healingAmount <= 0f)
        {
            return;
        }

        if (healingRadius > 0f)
        {
            TryApplyHealingAreaAt(impactPoint, healingRadius, healingAmount, null);
            return;
        }

        if (payloadCollider == null || targetRoot == null)
        {
            return;
        }

        if (enemy != null && !enemy.Equals(null))
        {
            TryApplyHealingToEnemy(payloadCollider, targetRoot, "PayloadHealing", healingAmount, out _);
        }

        TryApplyHealingToPlayer(payloadCollider, targetRoot, "PayloadHealing", healingAmount, out _);
    }

    private int TryApplyHealingAreaAt(Vector3 impactPoint, float healingRadius, float healingAmount, Transform excludedRoot)
    {
        if (healingRadius <= 0f || healingAmount <= 0f)
        {
            return 0;
        }

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, healingRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        HashSet<int> healedRoots = new();
        int healedCount = 0;
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap.isTrigger ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
            if (overlapRoot == null ||
                (excludedRoot != null && overlapRoot == excludedRoot) ||
                !healedRoots.Add(overlapRoot.GetInstanceID()))
            {
                continue;
            }

            bool healed = false;
            healed |= TryApplyHealingToEnemy(overlap, overlapRoot, "HealingArea", healingAmount, out _);
            healed |= TryApplyHealingToPlayer(overlap, overlapRoot, "HealingArea", healingAmount, out _);
            if (healed)
            {
                healedCount++;
            }
        }

        return healedCount;
    }

    private int TryEmitPayloadProjectiles(SpellCastBlock innerBlock, Vector3 impactPoint, Transform targetRoot, int remainingBudget)
    {
        if (remainingBudget <= 0 || innerBlock.Projectiles.Count <= 0)
        {
            return 0;
        }

        CharBullet template = spawnTemplate != null ? spawnTemplate : this;
        Transform payloadOwner = ownerRoot != null ? ownerRoot : transform;
        Vector3 baseDirection = direction.sqrMagnitude > MinimumVectorSqrMagnitude ? direction.normalized : Vector3.forward;
        int emittedCount = 0;
        for (int i = 0; i < innerBlock.Projectiles.Count && emittedCount < remainingBudget; i++)
        {
            SpellProjectileNode projectile = innerBlock.Projectiles[i];
            if (projectile == null || !projectile.CanFire)
            {
                continue;
            }

            Vector3 spawnOffset = baseDirection * Mathf.Max(ResolveImpactRadius() + 0.05f, 0.1f);
            List<CharBullet> spawnedBullets = new();
            emittedCount += AttackProjectileEmitter.Emit(
                template,
                payloadOwner,
                impactPoint + spawnOffset,
                baseDirection,
                projectile,
                targetPolicy,
                null,
                spawnedBullets);

            for (int bulletIndex = 0; bulletIndex < spawnedBullets.Count; bulletIndex++)
            {
                CharBullet spawnedBullet = spawnedBullets[bulletIndex];
                if (spawnedBullet == null)
                {
                    continue;
                }

                spawnedBullet.RegisterIgnoredTargetRoot(targetRoot);
                spawnedBullet.SetIgnoreGameplayPauseStatus(ignoreGameplayPauseStatus);
            }
        }

        return emittedCount;
    }

    /// <summary>
    /// summary: 根据当前核心词和目标敌人决定本次直击应使用的实际伤害值。
    /// param: enemy 当前直击命中的敌人；若命中的是玩家或环境则可为空
    /// returns: 已应用 Edge 护甲倍率后的直击伤害
    /// </summary>
    private float ResolveDirectDamage(Enemy enemy)
    {
        float resolvedDamage = Damage;
        CoreEffectPayload coreEffects = GetCoreEffects();
        if (enemy == null || enemy.Equals(null) || !coreEffects.HasArmoredBonus || enemy.Definition == null)
        {
            return resolvedDamage;
        }

        if (string.Equals(enemy.Definition.EnemyId, coreEffects.armoredEnemyId, StringComparison.Ordinal))
        {
            resolvedDamage *= coreEffects.armoredDamageMultiplier;
        }

        return resolvedDamage;
    }

    /// <summary>
    /// summary: 读取当前命中的法线；优先使用 sweep 法线，重叠检测时回退为由命中点估算的离面方向。
    /// param: other 当前命中的碰撞体
    /// param: impactNormal sweep 阶段得到的法线
    /// returns: 可用于反射的归一化法线
    /// </summary>
    private Vector3 ResolveImpactNormal(Collider other, Vector3? impactNormal)
    {
        if (impactNormal.HasValue && impactNormal.Value.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            return impactNormal.Value.normalized;
        }

        Vector3 impactPoint = GetImpactPoint(other);
        Vector3 referencePoint = MovementTarget != null ? MovementTarget.position : transform.position;
        Vector3 estimatedNormal = referencePoint - impactPoint;
        if (estimatedNormal.sqrMagnitude > MinimumVectorSqrMagnitude)
        {
            return estimatedNormal.normalized;
        }

        Vector3 fallbackNormal = -GetVelocity(Space.World);
        return fallbackNormal.sqrMagnitude > MinimumVectorSqrMagnitude ? fallbackNormal.normalized : Vector3.back;
    }

    /// <summary>
    /// summary: 读取当前命中球的世界半径，供反弹回退和分裂出生偏移使用。
    /// param: 无
    /// returns: 当前命中球的世界半径；缺失时返回 0
    /// </summary>
    private float ResolveImpactRadius()
    {
        return TryGetImpactSphere(out _, out float radius) ? radius : 0f;
    }

    /// <summary>
    /// summary: 判断当前行为是否允许在命中环境时执行反弹。
    /// param: 无
    /// returns: 当前行为词为 Bounce 且仍有剩余反弹次数时返回 true
    /// </summary>
    private bool ShouldBounceOnEnvironment()
    {
        return attackSpec.behaviorType == AttackBehaviorType.Bounce ||
               currentProjectileNode?.BehaviorType == AttackBehaviorType.Bounce;
    }

    private bool IsPiercingLightCore()
    {
        CoreEffectPayload coreEffects = GetCoreEffects();
        return attackSpec.coreType == AttackCoreType.Light &&
               coreEffects.HasPiercingSuppression;
    }

    /// <summary>
    /// summary: 读取当前核心词对应的二级效果载荷。
    /// param: 无
    /// returns: 当前编译结果携带的 burn/slow/thunder/armor bonus 配置
    /// </summary>
    private CoreEffectPayload GetCoreEffects()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.CoreEffects.GetSanitized();
        }

        return default;
    }

    /// <summary>
    /// summary: 读取当前结果词对应的二级效果载荷。
    /// param: 无
    /// returns: 当前编译结果携带的爆炸、分裂与控制配置
    /// </summary>
    private ResultEffectPayload GetResultEffects()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.ResultEffects.GetSanitized();
        }

        return default;
    }

    /// <summary>
    /// summary: 判断当前这发子弹是否需要在命中时触发分裂散射。
    /// param: 无
    /// returns: 结果词为 Split 且已配置有效子弹数量时返回 true
    /// </summary>
    private bool ShouldTriggerSplit()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.ResultType == AttackResultType.Split && currentProjectileNode.ResultEffects.HasSplit;
        }

        return attackSpec.resultType == AttackResultType.Split;
    }

    /// <summary>
    /// summary: 判断当前这发子弹是否需要在命中敌人时累计控制计数。
    /// param: 无
    /// returns: 结果词为 StatusEffect 且配置了有效控制阈值时返回 true
    /// </summary>
    private bool ShouldTriggerControl()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.ResultType == AttackResultType.StatusEffect && currentProjectileNode.ResultEffects.HasControl;
        }

        return attackSpec.resultType == AttackResultType.StatusEffect;
    }

    /// <summary>
    /// summary: 判断当前命中结果是否应按治疗结算，而非伤害结算。
    /// param: 无
    /// returns: 当前结果词为 Healing 时返回 true
    /// </summary>
    private bool ShouldApplyHealingOnImpact()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.ResultType == AttackResultType.Healing;
        }

        return attackSpec.resultType == AttackResultType.Healing;
    }

    private bool IsCurrentResultType(AttackResultType resultType)
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.ResultType == resultType;
        }

        return attackSpec.resultType == resultType;
    }

    private bool IsResultType(AttackResultType expectedType, AttackResultType explicitType)
    {
        return explicitType != AttackResultType.None
            ? explicitType == expectedType
            : IsCurrentResultType(expectedType);
    }

    /// <summary>
    /// summary: 判断本次命中的对象是否应被视为敌人目标；兼容历史误拼的 Enemey 标签。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// param: enemy 从父级解析到的 Enemy 组件
    /// returns: 任一相关节点带有敌人标签时返回 true
    /// </summary>
    private static bool IsEnemyImpactTarget(Collider other, Transform targetRoot, Enemy enemy)
    {
        return HasEnemyTag(other.transform) ||
               HasEnemyTag(targetRoot) ||
               (enemy != null && HasEnemyTag(enemy.transform));
    }

    /// <summary>
    /// summary: 判断当前命中的对象是否属于可识别的 actor；仅玩家与敌人会进入策略判定。
    /// param: other 当前命中的碰撞体
    /// returns: 命中对象携带 Enemy 或 PlayerHealth 组件时返回 true
    /// </summary>
    private static bool HasImpactActor(Collider other)
    {
        return other != null &&
               (other.GetComponentInParent<Enemy>() != null || other.GetComponentInParent<PlayerHealth>() != null);
    }

    /// <summary>
    /// summary: 根据当前子弹的目标策略判断本次命中的 actor 是否为合法目标。
    /// param: other 当前命中的碰撞体
    /// param: targetRoot 当前命中的根节点
    /// returns: 命中对象属于当前策略允许的玩家或敌人时返回 true
    /// </summary>
    private bool IsConfiguredActorImpactTarget(Collider other, Transform targetRoot)
    {
        if (other == null)
        {
            return false;
        }

        if (ShouldDamageEnemies())
        {
            Enemy enemy = other.GetComponentInParent<Enemy>();
            if (IsEnemyImpactTarget(other, targetRoot, enemy))
            {
                return true;
            }
        }

        if (ShouldDamagePlayer() && other.GetComponentInParent<PlayerHealth>() != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// summary: 判断单个节点是否带有项目里用于敌人的标签。
    /// param: target 需要检查的节点
    /// returns: 标签匹配 Enemy_Object 时返回 true
    /// </summary>
    private static bool HasEnemyTag(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string targetTag = target.tag;
        return string.Equals(targetTag, EnemyTagName, StringComparison.Ordinal);
    }

    /// <summary>
    /// summary: 判断当前子弹策略是否允许对敌人造成伤害。
    /// param: 无
    /// returns: 目标策略包含敌人时返回 true
    /// </summary>
    private bool ShouldDamageEnemies()
    {
        return targetPolicy == BulletTargetPolicy.EnemiesOnly || targetPolicy == BulletTargetPolicy.Both;
    }

    /// <summary>
    /// summary: 判断当前子弹策略是否允许对玩家造成伤害。
    /// param: 无
    /// returns: 目标策略包含玩家时返回 true
    /// </summary>
    private bool ShouldDamagePlayer()
    {
        return targetPolicy == BulletTargetPolicy.PlayerOnly || targetPolicy == BulletTargetPolicy.Both;
    }

    /// <summary>
    /// summary: 判断当前这发子弹是否需要在命中时触发爆炸结算。
    /// param: 无
    /// returns: 结果词为 Explosion 时返回 true
    /// </summary>
    private bool ShouldTriggerExplosion()
    {
        if (currentProjectileNode != null)
        {
            return currentProjectileNode.HasExplosion;
        }

        return attackSpec.resultType == AttackResultType.Explosion;
    }

    /// <summary>
    /// summary: 获取当前子弹命中后要使用的爆炸半径。
    /// param: 无
    /// returns: 已编译的爆炸半径；未配置时返回 0
    /// </summary>
    private float GetExplosionRadius()
    {
        if (currentProjectileNode != null)
        {
            return Mathf.Max(0f, currentProjectileNode.ExplosionRadius);
        }

        return 0f;
    }

    /// <summary>
    /// summary: 获取当前子弹命中后要使用的爆炸延时。
    /// param: 无
    /// returns: 已编译结果中的爆炸延时；未配置时返回 0
    /// </summary>
    private float GetExplosionDelaySeconds()
    {
        if (currentProjectileNode != null)
        {
            return Mathf.Max(0f, currentProjectileNode.ResultEffects.explosionDelaySeconds);
        }

        return 0f;
    }

    /// <summary>
    /// summary: 基于当前命中体估算一次爆炸或直击的世界位置。
    /// param: other 当前命中的碰撞体
    /// returns: 命中的世界位置；不可用时回退到子弹当前位置
    /// </summary>
    private Vector3 GetImpactPoint(Collider other)
    {
        Vector3 referencePoint = MovementTarget != null ? MovementTarget.position : transform.position;
        if (other == null)
        {
            return referencePoint;
        }

        return other.ClosestPoint(referencePoint);
    }

    /// <summary>
    /// summary: 读取当前命中球在世界坐标系下的中心和半径。
    /// param: center 输出的世界中心
    /// param: radius 输出的世界半径
    /// returns: 成功拿到有效命中球时返回 true
    /// </summary>
    private bool TryGetImpactSphere(out Vector3 center, out float radius)
    {
        center = default;
        radius = 0f;
        if (impactCollider == null || !impactCollider.enabled)
        {
            return false;
        }

        center = impactCollider.transform.TransformPoint(impactCollider.center);
        Vector3 lossyScale = impactCollider.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
        radius = impactCollider.radius * maxScale;
        return radius > 0f;
    }

    /// <summary>
    /// summary: 输出当前发射的关键参数，便于确认子弹实例是否拿到了方向和速度。
    /// param: shotSpawnPosition 当前子弹出生点
    /// param: shotDirection 当前子弹发射方向
    /// param: shotSpeed 当前子弹发射速度
    /// returns: 无
    /// </summary>
    private void LogShotInitialized(Vector3 shotSpawnPosition, Vector3 shotDirection, float shotSpeed)
    {
        GameDebug.LogFormat(
            "[CharBullet] Spawned at {0} dir={1} speed={2} trigger={3} rb={4} moveTarget='{5}'",
            shotSpawnPosition,
            shotDirection.normalized,
            shotSpeed,
            impactCollider != null && impactCollider.isTrigger,
            MovementRigidbody != null,
            MovementTarget != null ? MovementTarget.name : "<null>");
    }

    /// <summary>
    /// summary: 在发射后检测一次出生重叠，帮助定位是否被地面或障碍体积立即吞掉。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void LogSpawnOverlapIfNeeded()
    {
        if (impactCollider == null)
        {
            return;
        }

        Bounds bounds = impactCollider.bounds;
        float overlapRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        if (overlapRadius <= 0f)
        {
            return;
        }

        Collider[] overlaps = Physics.OverlapSphere(bounds.center, overlapRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null ||
                overlap == impactCollider ||
                overlap.isTrigger ||
                IsGroundSurfaceImpact(overlap) ||
                IsOwnedTransform(overlap.transform) ||
                overlap.GetComponentInParent<CharBullet>() != null)
            {
                continue;
            }

            GameDebug.LogWarningFormat(
                "[CharBullet] Spawn overlap detected with target='{0}' collider='{1}' layer={2}. If the bullet disappears immediately, adjust spawn offset, impactMask, or collider size.",
                overlap.transform.root.name,
                overlap.name,
                overlap.gameObject.layer);
            return;
        }
    }

    private void EnsureBaseScaleSnapshot()
    {
        if (hasBaseScaleSnapshot)
        {
            return;
        }

        CaptureCurrentScaleAsBase();
    }

    /// <summary>
    /// summary: 统一应用新的攻击配置，并按需刷新当前子弹的缓存速度与剩余生命。
    /// param: newAttackSpec 需要应用的新攻击配置
    /// param: syncCurrentSpeed 是否同步当前飞行速度
    /// param: syncRemainingLife 是否同步当前剩余生命
    /// returns: 无
    /// </summary>
    private void ApplyAttackSpec(AttackSpec newAttackSpec, bool syncCurrentSpeed, bool syncRemainingLife)
    {
        attackSpec = newAttackSpec.GetSanitized();
        remainingBounceCount = attackSpec.bounceCount;
        if (syncCurrentSpeed)
        {
            speed = attackSpec.projectileSpeed;
        }

        if (syncRemainingLife)
        {
            remainingLife = attackSpec.projectileLife;
        }
    }

    private void ApplyScaleMultiplier()
    {
        if (SizeTarget == null)
        {
            return;
        }

        SizeTarget.localScale = baseLocalScale * scaleMultiplier;
        ApplyImpactColliderScale();
    }

    private void ApplyImpactColliderScale()
    {
        if (impactCollider == null)
        {
            return;
        }

        if (hasFontSizeDrivenImpactRadius)
        {
            impactCollider.radius = Mathf.Max(0f, fontSizeDrivenImpactRadius);
            return;
        }

        impactCollider.radius = baseImpactRadius * scaleMultiplier * impactRadiusMultiplier;
    }

    private void ApplyProjectilePresentation()
    {
        SpellProjectileNode projectile = currentProjectileNode;
        float resolvedScaleMultiplier = projectile != null
            ? Mathf.Max(0f, projectile.ScaleMultiplier)
            : scaleMultiplier;
        float resolvedImpactRadiusMultiplier = projectile != null
            ? Mathf.Max(0f, projectile.ImpactRadiusMultiplier)
            : impactRadiusMultiplier;

        TrySetScaleMultiplier(resolvedScaleMultiplier);
        TrySetImpactRadiusMultiplier(resolvedImpactRadiusMultiplier);

        if (projectile != null)
        {
            if (!string.IsNullOrEmpty(projectile.DisplayText))
            {
                TrySetText(projectile.DisplayText);
            }

            if (projectile.HasTextColorOverride)
            {
                TrySetTextColor(projectile.TextColor);
            }

            if (projectile.HasFontSizeOverride)
            {
                float baseFontSize = GetCurrentGlyphSquareSize();
                TrySetFontSize(projectile.ResolveFontSize(baseFontSize));
            }

            if (visualPresenter != null)
            {
                visualPresenter.ApplyCompiledAppearance(projectile, this);
            }

            return;
        }

        visualPresenter?.RefreshPreview();
    }

    private void NotifyVisualPresenterPreview()
    {
        if (visualPresenter != null)
        {
            visualPresenter.RefreshPreview();
        }
    }

    /// <summary>
    /// summary: 在编辑器校验结束后延迟刷新视觉预览，避免在 OnValidate 中直接改 SpriteRenderer。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void QueueEditorVisualPresenterPreview()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            NotifyVisualPresenterPreview();
            return;
        }

        if (editorPreviewRefreshQueued)
        {
            return;
        }

        editorPreviewRefreshQueued = true;
        EditorApplication.delayCall += FlushQueuedEditorVisualPreview;
#else
        NotifyVisualPresenterPreview();
#endif
    }

#if UNITY_EDITOR
    private void FlushQueuedEditorVisualPreview()
    {
        editorPreviewRefreshQueued = false;
        if (this == null)
        {
            return;
        }

        NotifyVisualPresenterPreview();
    }
#endif

    private bool TryGetGlyphRectTransform(out RectTransform glyphRectTransform)
    {
        glyphRectTransform = null;

        if (GlyphText != null && GlyphText.rectTransform != null)
        {
            glyphRectTransform = GlyphText.rectTransform;
            return true;
        }

        if (SizeTarget is RectTransform sizeRectTransform)
        {
            glyphRectTransform = sizeRectTransform;
            return true;
        }

        return false;
    }

    private float GetCurrentGlyphSquareSize()
    {
        if (!TryGetGlyphRectTransform(out RectTransform glyphRectTransform))
        {
            return 0f;
        }

        Rect rect = glyphRectTransform.rect;
        if (rect.width > 0f)
        {
            return rect.width;
        }

        if (rect.height > 0f)
        {
            return rect.height;
        }

        return 0f;
    }

    private void ApplyImpactColliderRadiusFromFontSize(float fontSize)
    {
        if (impactCollider == null)
        {
            return;
        }

        hasFontSizeDrivenImpactRadius = true;
        fontSizeDrivenImpactRadius = Mathf.Max(0f, fontSize) * 0.5f;
        ApplyImpactColliderScale();
    }

    private Vector3 GetStoredVelocity()
    {
        if (direction.sqrMagnitude <= MinimumVectorSqrMagnitude || speed <= 0f)
        {
            return Vector3.zero;
        }

        return direction.normalized * speed;
    }

    private bool TryConvertDirectionToStorageSpace(Vector3 sourceDirection, Space sourceSpace, out Vector3 storedDirection)
    {
        storedDirection = Vector3.zero;
        if (sourceDirection.sqrMagnitude <= MinimumVectorSqrMagnitude)
        {
            return false;
        }

        Vector3 resolvedDirection = sourceDirection.normalized;
        if (sourceSpace == movementSpace)
        {
            storedDirection = resolvedDirection;
            return true;
        }

        Transform target = MovementTarget;
        if (sourceSpace == Space.World && movementSpace == Space.Self)
        {
            storedDirection = target.InverseTransformDirection(resolvedDirection).normalized;
            return true;
        }

        if (sourceSpace == Space.Self && movementSpace == Space.World)
        {
            storedDirection = target.TransformDirection(resolvedDirection).normalized;
            return true;
        }

        storedDirection = resolvedDirection;
        return true;
    }

    private bool IsImpactLayerIncluded(int layer)
    {
        return (attackSpec.impactMask.value & (1 << layer)) != 0;
    }

    private bool IsOwnedTransform(Transform candidate)
    {
        return ownerRoot != null &&
               candidate != null &&
               (candidate == ownerRoot || candidate.IsChildOf(ownerRoot));
    }

    /// <summary>
    /// summary: 判断当前命中是否来自地图 Ground 表面；地面只用于承载角色，不应让平面射击子弹立刻掉命。
    /// param: other 当前检测到的碰撞体
    /// returns: 命中的碰撞体属于 Ground surface 时返回 true
    /// </summary>
    private static bool IsGroundSurfaceImpact(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null || HasEnemyTag(other.transform) || HasEnemyTag(other.transform.root))
        {
            return false;
        }

        if (other.CompareTag(Kernel.MapGrid.MapGridAuthoring.GroundTagName))
        {
            return true;
        }

        if (other.transform.root != null && other.transform.root.CompareTag(Kernel.MapGrid.MapGridAuthoring.GroundTagName))
        {
            return true;
        }

        CellData cellData = other.GetComponentInParent<CellData>();
        return cellData != null && cellData.SurfaceType == CellData.CellSurfaceType.Ground;
    }

    private bool IsGlyphTextReferenceValid()
    {
        return glyphText != null && IsTransformInsideBullet(glyphText.transform);
    }

    private bool IsMovementTargetReferenceValid()
    {
        return movementTarget != null && IsTransformInsideBullet(movementTarget);
    }

    private bool IsSizeTargetReferenceValid()
    {
        return sizeTarget != null && IsTransformInsideBullet(sizeTarget);
    }

    private bool IsMovementRigidbodyReferenceValid()
    {
        return IsMovementRigidbodyCompatible(movementRigidbody);
    }

    private bool IsMovementRigidbodyCompatible(Rigidbody candidate)
    {
        if (candidate == null ||
            candidate.transform == null ||
            !IsTransformInsideBullet(candidate.transform))
        {
            return false;
        }

        if (IsMovementTargetReferenceValid() &&
            candidate.transform != movementTarget &&
            !movementTarget.IsChildOf(candidate.transform))
        {
            return false;
        }

        if (IsGlyphTextReferenceValid() &&
            candidate.transform != glyphText.transform &&
            !glyphText.transform.IsChildOf(candidate.transform))
        {
            return false;
        }

        return !IsImpactColliderReferenceValid() ||
               candidate.transform == impactCollider.transform ||
               impactCollider.transform.IsChildOf(candidate.transform) ||
               impactCollider.attachedRigidbody == candidate;
    }

    private bool IsImpactColliderReferenceValid()
    {
        return impactCollider != null && IsTransformInsideBullet(impactCollider.transform);
    }

    private TMP_Text FindPreferredGlyphText()
    {
        Transform explicitGlyph = transform.Find("Text/Glyph");
        if (explicitGlyph != null && explicitGlyph.TryGetComponent(out TMP_Text explicitGlyphText))
        {
            return explicitGlyphText;
        }

        TMP_Text selfText = GetComponent<TMP_Text>();
        if (selfText != null)
        {
            return selfText;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private Transform FindPreferredMovementTarget()
    {
        Transform compatibleTarget = ResolveCompatibleMovementTarget();
        if (compatibleTarget != null)
        {
            return compatibleTarget;
        }

        for (int i = 0; i < PreferredMovementChildNames.Length; i++)
        {
            Transform namedChild = transform.Find(PreferredMovementChildNames[i]);
            if (DoesMovementTargetCarryBulletBody(namedChild))
            {
                return namedChild;
            }
        }

        return transform;
    }

    private Transform FindPreferredSizeTarget()
    {
        Transform explicitTextContainer = FindPreferredTextContainer();
        if (explicitTextContainer != null)
        {
            return explicitTextContainer;
        }

        if (IsGlyphTextReferenceValid())
        {
            return ResolvePreferredSizeTargetForGlyph(glyphText);
        }

        if (IsMovementTargetReferenceValid())
        {
            return movementTarget;
        }

        return transform;
    }

    /// <summary>
    /// summary: 查找当前子弹层级下显式命名的文字容器；新 prefab 契约下优先使用 Text 容器承载统一缩放。
    /// param: 无
    /// returns: 找到 Text 容器时返回该节点，否则返回 null
    /// </summary>
    private Transform FindPreferredTextContainer()
    {
        Transform explicitTextContainer = transform.Find("Text");
        if (explicitTextContainer != null && IsTransformInsideBullet(explicitTextContainer))
        {
            return explicitTextContainer;
        }

        return null;
    }

    /// <summary>
    /// summary: 为给定字形解析最合适的缩放目标；Text/Glyph 结构下优先缩放 Text 容器，旧结构回退到字形本体。
    /// param: text 需要参与解析的字形文本组件
    /// returns: 可用于视觉缩放的目标节点
    /// </summary>
    private Transform ResolvePreferredSizeTargetForGlyph(TMP_Text text)
    {
        if (text == null || !IsTransformInsideBullet(text.transform))
        {
            return FindPreferredTextContainer();
        }

        Transform explicitTextContainer = FindPreferredTextContainer();
        if (explicitTextContainer != null &&
            (text.transform == explicitTextContainer || text.transform.IsChildOf(explicitTextContainer)))
        {
            return explicitTextContainer;
        }

        return text.transform;
    }

    private Rigidbody FindPreferredMovementRigidbody(Transform target)
    {
        Transform current = target;
        while (current != null && IsTransformInsideBullet(current))
        {
            if (current.TryGetComponent(out Rigidbody currentRigidbody))
            {
                return currentRigidbody;
            }

            current = current.parent;
        }

        if (impactCollider != null && impactCollider.attachedRigidbody != null && IsTransformInsideBullet(impactCollider.attachedRigidbody.transform))
        {
            return impactCollider.attachedRigidbody;
        }

        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody candidate = rigidbodies[i];
            if (IsMovementRigidbodyCompatible(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// summary: 解析一个能同时驱动文字和命中体的移动根节点；当前 prefab 结构下通常是子弹根节点。
    /// param: 无
    /// returns: 可用的移动根节点
    /// </summary>
    private Transform ResolveCompatibleMovementTarget()
    {
        if (DoesMovementTargetCarryBulletBody(movementTarget))
        {
            return movementTarget;
        }

        Transform sharedRoot = FindSharedMovementRoot();
        if (DoesMovementTargetCarryBulletBody(sharedRoot))
        {
            return sharedRoot;
        }

        return transform;
    }

    /// <summary>
    /// summary: 判断一个节点是否能作为整颗文字子弹的移动根节点。
    /// param: candidate 需要验证的节点
    /// returns: 同时覆盖文字和命中体时返回 true
    /// </summary>
    private bool DoesMovementTargetCarryBulletBody(Transform candidate)
    {
        if (candidate == null || !IsTransformInsideBullet(candidate))
        {
            return false;
        }

        if (IsGlyphTextReferenceValid() &&
            candidate != glyphText.transform &&
            !glyphText.transform.IsChildOf(candidate))
        {
            return false;
        }

        return !IsImpactColliderReferenceValid() ||
               candidate == impactCollider.transform ||
               impactCollider.transform.IsChildOf(candidate);
    }

    /// <summary>
    /// summary: 在文字和命中体之间寻找共享父节点，供 sibling 结构的 prefab 作为默认移动根。
    /// param: 无
    /// returns: 当前子弹层级中的共享父节点
    /// </summary>
    private Transform FindSharedMovementRoot()
    {
        Transform sharedRoot = transform;
        if (IsGlyphTextReferenceValid())
        {
            sharedRoot = FindLowestCommonAncestor(sharedRoot, glyphText.transform);
        }

        if (IsImpactColliderReferenceValid())
        {
            sharedRoot = FindLowestCommonAncestor(sharedRoot, impactCollider.transform);
        }

        return sharedRoot;
    }

    /// <summary>
    /// summary: 在当前子弹层级里寻找两个节点的最低公共父节点。
    /// param: first 第一个节点
    /// param: second 第二个节点
    /// returns: 公共父节点；找不到时回退到子弹根节点
    /// </summary>
    private Transform FindLowestCommonAncestor(Transform first, Transform second)
    {
        Transform current = first;
        while (current != null && IsTransformInsideBullet(current))
        {
            if (second == current || second.IsChildOf(current))
            {
                return current;
            }

            current = current.parent;
        }

        return transform;
    }

    /// <summary>
    /// summary: 当连 Transform 根节点都无法驱动整颗子弹时，才允许最后兜底创建根节点刚体。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void TryCreateFallbackRootRigidbody()
    {
        Rigidbody rootRigidbody = GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            rootRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rootRigidbody.useGravity = false;
        rootRigidbody.isKinematic = false;
        rootRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rootRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rootRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        movementRigidbody = rootRigidbody;
    }

    private bool CanDriveBulletByTransform()
    {
        return DoesMovementTargetCarryBulletBody(MovementTarget);
    }

    private static int CompareRaycastDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }

    private SphereCollider FindPreferredImpactCollider()
    {
        SphereCollider selfCollider = GetComponent<SphereCollider>();
        if (selfCollider != null)
        {
            return selfCollider;
        }

        SphereCollider[] colliders = GetComponentsInChildren<SphereCollider>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            SphereCollider candidate = colliders[i];
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsTransformInsideBullet(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
}
