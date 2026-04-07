using System;
using System.Collections.Generic;
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
    private const string EnemyTagName = "Enemy_Object";
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

    [Header("Scale")]
    [SerializeField, Min(0f)] private float scaleMultiplier = 1f;
    [SerializeField, Min(0f)] private float impactRadiusMultiplier = 1f;

    private readonly HashSet<int> impactedTargetRoots = new();
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
    private CompiledAttack compiledAttack;

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
    public CompiledAttack CurrentCompiledAttack => compiledAttack;

    private void Awake()
    {
        TryCacheBindings();
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        attackSpec = attackSpec.GetSanitized();
        CaptureCurrentScaleAsBase();
        CaptureImpactColliderBaseRadius();
        ApplyScaleMultiplier();
    }

    /// <summary>
    /// summary: 初始化一次新的子弹发射，并额外注入编译后的高层攻击语义。
    /// param: owner 发射者根节点，用于忽略自碰撞
    /// param: spawnPosition 子弹出生世界坐标
    /// param: shotDirection 本次发射方向
    /// param: shotAttackSpec 本次发射使用的攻击配置
    /// param: shotCompiledAttack 本次发射对应的编译结果
    /// returns: 无
    /// </summary>
    public void InitializeShot(Transform owner, Vector3 spawnPosition, Vector3 shotDirection, AttackSpec shotAttackSpec, CompiledAttack shotCompiledAttack)
    {
        TryCacheBindings(overwriteExisting: true);
        EnsureCompatiblePhysicsBindings(allowFallbackCreation: false);
        EnsureImpactColliderConfiguration();
        AttackSpec resolvedAttackSpec = shotCompiledAttack != null ? shotCompiledAttack.AttackSpec : shotAttackSpec;
        attackSpec = resolvedAttackSpec.GetSanitized();
        compiledAttack = shotCompiledAttack;
        ownerRoot = owner;
        spawnWorldPosition = spawnPosition;
        elapsedLifetime = 0f;
        impactedTargetRoots.Clear();
        hasPreviousImpactCheckCenter = false;
        remainingLife = attackSpec.projectileLife;
        isActiveShot = true;
        autoMove = true;
        movementSpace = Space.World;
        hasFontSizeDrivenImpactRadius = false;
        fontSizeDrivenImpactRadius = 0f;

        EnableImpactCollider(true);
        TryStopMovement();
        TrySetWorldPosition(spawnPosition);
        TrySetDirectionAndSpeed(shotDirection, attackSpec.projectileSpeed, Space.World);
        ApplyFacingDirection(shotDirection);
        IgnoreOwnerCollisions();
        ResetImpactCheckState();
        ApplyCompiledAttackPresentation();

        // LogShotInitialized(spawnPosition, shotDirection, attackSpec.projectileSpeed);
        // LogSpawnOverlapIfNeeded();
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

    private void Update()
    {
        if (MovementRigidbody != null)
        {
            CheckImpactContacts();
            if (!autoMove)
            {
                UpdateLifetime(Time.deltaTime);
            }

            return;
        }

        if (autoMove)
        {
            MoveStep(Time.deltaTime);
        }

        CheckImpactContacts();
        UpdateLifetime(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (MovementRigidbody == null)
        {
            return;
        }

        if (autoMove)
        {
            MoveStep(Time.fixedDeltaTime);
        }

        CheckImpactContacts();
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
                    if (!TryRegisterImpact(hits[i].collider))
                    {
                        continue;
                    }

                    if (!isActiveShot)
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
            if (!TryRegisterImpact(overlaps[i]))
            {
                continue;
            }

            if (!isActiveShot)
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
        int targetRootId = targetRoot.GetInstanceID();
        if (!impactedTargetRoots.Add(targetRootId))
        {
            return false;
        }

        Vector3 impactPoint = GetImpactPoint(other);
        TryApplyDamageToEnemy(other, targetRoot, "Direct");
        TryApplyExplosionDamageAt(impactPoint);
        int nextLife = Mathf.Max(0, remainingLife - Mathf.Max(1, attackSpec.impactLifeCost));
        // GameDebug.LogFormat(
        //     "[CharBullet] Hit target='{0}' via collider='{1}' layer={2} life {3}->{4}",
        //     targetRoot.name,
        //     other.name,
        //     other.gameObject.layer,
        //     remainingLife,
        //     nextLife);
        ApplyLifeCost(attackSpec.impactLifeCost);
        return true;
    }

    /// <summary>
    /// summary: 当命中的对象被标记为敌人时，尝试把当前子弹伤害应用到其 Enemy 组件上。
    /// param: other 本次命中的碰撞体
    /// param: targetRoot 当前命中去重使用的根节点
    /// param: damageSource 本次伤害来源，便于区分直击和爆炸日志
    /// returns: 成功对敌人结算伤害时返回 true
    /// </summary>
    private bool TryApplyDamageToEnemy(Collider other, Transform targetRoot, string damageSource)
    {
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (Damage <= 0f || !IsEnemyImpactTarget(other, targetRoot, enemy))
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

        float previousHealth = enemy.CurrentHealth;
        if (!enemy.TryApplyDamage(Damage, out float remainingHealth, out bool isDead))
        {
            GameDebug.LogFormat(
                "[CharBullet] Enemy target='{0}' ignored {1} damage={2} health={3}/{4}",
                targetRoot.name,
                damageSource,
                Damage,
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
            GameDebug.LogFormat("[CharBullet] Enemy target='{0}' died from {1}.", targetRoot.name, damageSource);
        }

        return true;
    }

    /// <summary>
    /// summary: 若当前编译结果带有爆炸语义，则在命中点附近再做一次 AoE 伤害结算。
    /// param: impactPoint 当前命中的世界位置
    /// returns: 无
    /// </summary>
    private void TryApplyExplosionDamageAt(Vector3 impactPoint)
    {
        if (!ShouldTriggerExplosion())
        {
            return;
        }

        float explosionRadius = GetExplosionRadius();
        if (explosionRadius <= 0f || Damage <= 0f)
        {
            return;
        }

        Collider[] overlaps = Physics.OverlapSphere(impactPoint, explosionRadius, attackSpec.impactMask, QueryTriggerInteraction.Ignore);
        var damagedRoots = new HashSet<int>();
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

            TryApplyDamageToEnemy(overlap, overlapRoot, "Explosion");
        }
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
    /// summary: 判断当前这发子弹是否需要在命中时触发爆炸结算。
    /// param: 无
    /// returns: 结果词为 Explosion 时返回 true
    /// </summary>
    private bool ShouldTriggerExplosion()
    {
        if (compiledAttack != null)
        {
            return compiledAttack.HasExplosion;
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
        if (compiledAttack != null)
        {
            return Mathf.Max(0f, compiledAttack.ExplosionRadius);
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

    private void ApplyCompiledAttackPresentation()
    {
        float resolvedScaleMultiplier = compiledAttack != null ? Mathf.Max(0f, compiledAttack.ScaleMultiplier) : scaleMultiplier;
        float resolvedImpactRadiusMultiplier = compiledAttack != null ? Mathf.Max(0f, compiledAttack.ImpactRadiusMultiplier) : impactRadiusMultiplier;

        TrySetScaleMultiplier(resolvedScaleMultiplier);
        TrySetImpactRadiusMultiplier(resolvedImpactRadiusMultiplier);

        if (compiledAttack == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(compiledAttack.DisplayText))
        {
            TrySetText(compiledAttack.DisplayText);
        }

        if (compiledAttack.HasTextColorOverride)
        {
            TrySetTextColor(compiledAttack.TextColor);
        }

        if (compiledAttack.HasFontSizeOverride)
        {
            float baseFontSize = GetCurrentGlyphSquareSize();
            TrySetFontSize(compiledAttack.ResolveFontSize(baseFontSize));
        }

        if (visualPresenter != null)
        {
            visualPresenter.ApplyCompiledAppearance(compiledAttack, this);
        }
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
