using Kernel.Bullet;
using UnityEngine;

/// <summary>
/// 统一管理敌人的持续伤害、减速与眩晕状态，供子弹命中与敌人行为组件共享。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyStatusEffectController : MonoBehaviour
{
    private const float DefaultReactionConsumeRatio = 0.5f;
    private const int StatusSlotCount = (int)SpellStatusSlot.PuppetMark + 1;

    [SerializeField] private Enemy enemyData;

    private readonly float[] statusSlotValues = new float[StatusSlotCount];
    private int fireHitCount;
    private int controlHitCount;
    private float burnDamagePerSecond;
    private float burnRemainingDuration;
    private float slowPercent;
    private float slowRemainingDuration;
    private float stunRemainingDuration;
    private float skillActionLockRemainingDuration;
    private float polymorphRemainingDuration;

    public int FireHitCount => fireHitCount;
    public int ControlHitCount => controlHitCount;
    public bool IsBurning => burnRemainingDuration > 0f && burnDamagePerSecond > 0f;
    public bool IsSlowed => slowRemainingDuration > 0f && slowPercent > 0f;
    public bool IsStunned => stunRemainingDuration > 0f;
    public bool IsSkillActionLocked => skillActionLockRemainingDuration > 0f;
    public bool IsPolymorphed => polymorphRemainingDuration > 0f;
    public SpellElementReactionResult LastReaction { get; private set; }
    public bool CanMove => !IsStunned && !IsPolymorphed;
    public bool CanAct => !IsStunned && !IsSkillActionLocked && !IsPolymorphed;
    public float MovementSpeedMultiplier => IsStunned || IsPolymorphed ? 0f : Mathf.Clamp01(1f - GetActiveSlowPercent());

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
        skillActionLockRemainingDuration = Mathf.Max(0f, skillActionLockRemainingDuration);
        polymorphRemainingDuration = Mathf.Max(0f, polymorphRemainingDuration);
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
        AddStatusValue(SpellStatusSlot.Ignite, 1f);
        if (fireHitCount < triggerCount)
        {
            return false;
        }

        fireHitCount = 0;
        burnDamagePerSecond = Mathf.Max(0f, damagePerSecond);
        burnRemainingDuration = Mathf.Max(0f, duration);
        return true;
    }

    public float GetStatusValue(SpellStatusSlot slot)
    {
        int index = (int)slot;
        return index > 0 && index < statusSlotValues.Length ? statusSlotValues[index] : 0f;
    }

    public bool TryApplyStatusApplication(
        SpellStatusApplication application,
        out SpellElementReactionResult reactionResult)
    {
        reactionResult = default;
        LastReaction = default;
        SpellStatusApplication sanitized = application.GetSanitized();
        if (sanitized.slot == SpellStatusSlot.None || sanitized.amount <= 0f)
        {
            return false;
        }

        AddStatusValue(sanitized.slot, sanitized.amount);
        ApplyStatusThresholdEffect(sanitized);
        reactionResult = ResolveAndApplyReaction(sanitized.slot);
        LastReaction = reactionResult;
        return true;
    }

    public int TryApplyStatusApplications(SpellStatusApplication[] applications)
    {
        if (applications == null || applications.Length <= 0)
        {
            return 0;
        }

        int appliedCount = 0;
        for (int i = 0; i < applications.Length; i++)
        {
            if (TryApplyStatusApplication(applications[i], out _))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    public float ConsumeStatus(SpellStatusSlot slot, float ratio)
    {
        int index = (int)slot;
        if (index <= 0 || index >= statusSlotValues.Length)
        {
            return 0f;
        }

        float clampedRatio = Mathf.Clamp01(ratio);
        float consumedAmount = statusSlotValues[index] * clampedRatio;
        statusSlotValues[index] = Mathf.Max(0f, statusSlotValues[index] - consumedAmount);
        return consumedAmount;
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
        AddStatusValue(SpellStatusSlot.Disable, 1f);
        if (controlHitCount < triggerCount)
        {
            return false;
        }

        controlHitCount = 0;
        stunRemainingDuration = Mathf.Max(0f, duration);
        return true;
    }

    /// <summary>
    /// summary: 施加一次技能动作锁，在持续时间内禁止敌人执行攻击与技能动作。
    /// param: duration 技能动作锁持续时间
    /// returns: 参数合法并成功写入动作锁时返回 true
    /// </summary>
    public bool ApplySkillActionLock(float duration)
    {
        if (duration <= 0f)
        {
            return false;
        }

        skillActionLockRemainingDuration = Mathf.Max(skillActionLockRemainingDuration, duration);
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
        TickSkillActionLock(deltaTime);
        TickPolymorph(deltaTime);
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

    private void TickSkillActionLock(float deltaTime)
    {
        if (!IsSkillActionLocked)
        {
            return;
        }

        skillActionLockRemainingDuration = Mathf.Max(0f, skillActionLockRemainingDuration - deltaTime);
    }

    private void TickPolymorph(float deltaTime)
    {
        if (!IsPolymorphed)
        {
            return;
        }

        polymorphRemainingDuration = Mathf.Max(0f, polymorphRemainingDuration - deltaTime);
    }

    private void AddStatusValue(SpellStatusSlot slot, float amount)
    {
        int index = (int)slot;
        if (index <= 0 || index >= statusSlotValues.Length || amount <= 0f)
        {
            return;
        }

        statusSlotValues[index] = Mathf.Max(0f, statusSlotValues[index] + amount);
    }

    private void ApplyStatusThresholdEffect(SpellStatusApplication application)
    {
        float threshold = Mathf.Max(0f, application.threshold);
        if (threshold <= 0f || GetStatusValue(application.slot) < threshold)
        {
            return;
        }

        switch (application.slot)
        {
            case SpellStatusSlot.Ignite:
                if (application.strength > 0f && application.duration > 0f)
                {
                    burnDamagePerSecond = Mathf.Max(burnDamagePerSecond, application.strength);
                    burnRemainingDuration = Mathf.Max(burnRemainingDuration, application.duration);
                }
                break;
            case SpellStatusSlot.Freeze:
                stunRemainingDuration = Mathf.Max(stunRemainingDuration, application.duration);
                break;
            case SpellStatusSlot.Disable:
                skillActionLockRemainingDuration = Mathf.Max(skillActionLockRemainingDuration, application.duration);
                break;
            case SpellStatusSlot.Bind:
                stunRemainingDuration = Mathf.Max(stunRemainingDuration, application.duration);
                break;
            case SpellStatusSlot.Polymorph:
                if (CanApplyPolymorph(application.duration))
                {
                    polymorphRemainingDuration = Mathf.Max(polymorphRemainingDuration, application.duration);
                }
                break;
        }
    }

    private bool CanApplyPolymorph(float duration)
    {
        if (duration <= 0f || !TryResolveEnemyData() || enemyData == null || enemyData.Equals(null))
        {
            return false;
        }

        return enemyData.DisplacementWeight <= 1f;
    }

    private SpellElementReactionResult ResolveAndApplyReaction(SpellStatusSlot changedSlot)
    {
        SpellElementReactionResult reaction = ResolveReaction(changedSlot);
        if (!reaction.HasReaction)
        {
            return reaction;
        }

        ConsumeStatus(reaction.FirstSlot, DefaultReactionConsumeRatio);
        ConsumeStatus(reaction.SecondSlot, DefaultReactionConsumeRatio);
        return reaction;
    }

    private SpellElementReactionResult ResolveReaction(SpellStatusSlot changedSlot)
    {
        if ((changedSlot == SpellStatusSlot.Ignite && GetStatusValue(SpellStatusSlot.Freeze) > 0f) ||
            (changedSlot == SpellStatusSlot.Freeze && GetStatusValue(SpellStatusSlot.Ignite) > 0f))
        {
            return new SpellElementReactionResult(
                SpellElementReactionType.ThermalCrack,
                SpellStatusSlot.Ignite,
                SpellStatusSlot.Freeze);
        }

        if (changedSlot == SpellStatusSlot.Disable && GetStatusValue(SpellStatusSlot.Wet) > 0f)
        {
            return new SpellElementReactionResult(
                SpellElementReactionType.ElectroCharged,
                SpellStatusSlot.Wet,
                SpellStatusSlot.Disable);
        }

        if (changedSlot == SpellStatusSlot.Disable &&
            (GetStatusValue(SpellStatusSlot.Freeze) > 0f || GetStatusValue(SpellStatusSlot.Bind) > 0f))
        {
            SpellStatusSlot conductor = GetStatusValue(SpellStatusSlot.Freeze) > 0f
                ? SpellStatusSlot.Freeze
                : SpellStatusSlot.Bind;
            return new SpellElementReactionResult(
                SpellElementReactionType.ConductiveThunder,
                conductor,
                SpellStatusSlot.Disable);
        }

        if (changedSlot == SpellStatusSlot.Ignite && GetStatusValue(SpellStatusSlot.Corrosion) > 0f)
        {
            return new SpellElementReactionResult(
                SpellElementReactionType.ToxicBurst,
                SpellStatusSlot.Ignite,
                SpellStatusSlot.Corrosion);
        }

        return default;
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
