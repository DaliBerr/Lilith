using UnityEngine;

/// <summary>
/// 统一管理敌人的持续伤害、减速与眩晕状态，供子弹命中与敌人行为组件共享。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStatusEffectController : MonoBehaviour
{
    [SerializeField] private Enemy enemyData;

    private int fireHitCount;
    private int controlHitCount;
    private float burnDamagePerSecond;
    private float burnRemainingDuration;
    private float slowPercent;
    private float slowRemainingDuration;
    private float stunRemainingDuration;

    public int FireHitCount => fireHitCount;
    public int ControlHitCount => controlHitCount;
    public bool IsBurning => burnRemainingDuration > 0f && burnDamagePerSecond > 0f;
    public bool IsSlowed => slowRemainingDuration > 0f && slowPercent > 0f;
    public bool IsStunned => stunRemainingDuration > 0f;
    public bool CanMove => !IsStunned;
    public bool CanAct => !IsStunned;
    public float MovementSpeedMultiplier => IsStunned ? 0f : Mathf.Clamp01(1f - GetActiveSlowPercent());

    private void Awake()
    {
        TryResolveEnemyData();
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        burnDamagePerSecond = Mathf.Max(0f, burnDamagePerSecond);
        burnRemainingDuration = Mathf.Max(0f, burnRemainingDuration);
        slowPercent = Mathf.Clamp01(slowPercent);
        slowRemainingDuration = Mathf.Max(0f, slowRemainingDuration);
        stunRemainingDuration = Mathf.Max(0f, stunRemainingDuration);
        fireHitCount = Mathf.Max(0, fireHitCount);
        controlHitCount = Mathf.Max(0, controlHitCount);
    }

    /// <summary>
    /// summary: 记录一次 Fire 核心命中；达到阈值时刷新灼烧并清零该敌人的 Fire 计数。
    /// param: triggerCount 触发灼烧所需累计命中次数
    /// param: damagePerSecond 灼烧每秒伤害
    /// param: duration 灼烧持续时间
    /// returns: 本次命中是否实际触发了灼烧
    /// </summary>
    public bool RegisterFireHit(int triggerCount, float damagePerSecond, float duration)
    {
        if (triggerCount <= 0 || damagePerSecond <= 0f || duration <= 0f)
        {
            return false;
        }

        fireHitCount = Mathf.Max(0, fireHitCount) + 1;
        if (fireHitCount < triggerCount)
        {
            return false;
        }

        fireHitCount = 0;
        burnDamagePerSecond = Mathf.Max(0f, damagePerSecond);
        burnRemainingDuration = Mathf.Max(0f, duration);
        return true;
    }

    /// <summary>
    /// summary: 对当前敌人施加或刷新一次减速；该效果不叠层，只保留最新时长和更高减速值。
    /// param: percent 减速百分比，0.25 表示降低 25% 速度
    /// param: duration 减速持续时间
    /// returns: 参数合法并成功写入减速状态时返回 true
    /// </summary>
    public bool ApplySlow(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f)
        {
            return false;
        }

        slowPercent = Mathf.Max(slowPercent, Mathf.Clamp01(percent));
        slowRemainingDuration = Mathf.Max(0f, duration);
        return true;
    }

    /// <summary>
    /// summary: 记录一次控制计数命中；达到阈值时刷新眩晕并清零该敌人的控制计数。
    /// param: triggerCount 触发控制所需累计命中次数
    /// param: duration 眩晕持续时间
    /// returns: 本次命中是否实际触发了眩晕
    /// </summary>
    public bool RegisterControlHit(int triggerCount, float duration)
    {
        if (triggerCount <= 0 || duration <= 0f)
        {
            return false;
        }

        controlHitCount = Mathf.Max(0, controlHitCount) + 1;
        if (controlHitCount < triggerCount)
        {
            return false;
        }

        controlHitCount = 0;
        stunRemainingDuration = Mathf.Max(0f, duration);
        return true;
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        TickBurn(deltaTime);
        TickSlow(deltaTime);
        TickStun(deltaTime);
    }

    private void TickBurn(float deltaTime)
    {
        if (!IsBurning || !TryResolveEnemyData() || enemyData == null || enemyData.IsDead)
        {
            return;
        }

        float appliedDuration = Mathf.Min(deltaTime, burnRemainingDuration);
        burnRemainingDuration = Mathf.Max(0f, burnRemainingDuration - deltaTime);
        if (appliedDuration <= 0f)
        {
            return;
        }

        enemyData.TryApplyDamage(burnDamagePerSecond * appliedDuration, out _, out _);
        if (burnRemainingDuration <= 0f)
        {
            burnDamagePerSecond = 0f;
        }
    }

    private void TickSlow(float deltaTime)
    {
        if (!IsSlowed)
        {
            return;
        }

        slowRemainingDuration = Mathf.Max(0f, slowRemainingDuration - deltaTime);
        if (slowRemainingDuration <= 0f)
        {
            slowPercent = 0f;
        }
    }

    private void TickStun(float deltaTime)
    {
        if (!IsStunned)
        {
            return;
        }

        stunRemainingDuration = Mathf.Max(0f, stunRemainingDuration - deltaTime);
    }

    private float GetActiveSlowPercent()
    {
        return IsSlowed ? slowPercent : 0f;
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
}
