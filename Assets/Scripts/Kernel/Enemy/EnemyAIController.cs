using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按 EnemyAIProfile 的 Utility 分数驱动普通敌人的移动、攻击与技能调度。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyAIController : MonoBehaviour
{
    private const float DefaultSkillActionLockSeconds = 0.15f;

    [SerializeField] private EnemyAIProfile profile;
    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private CharEnemyMovement movement;
    [SerializeField] private EnemyMeleeAttacker meleeAttacker;
    [SerializeField] private EnemyRangedTokenAttacker rangedTokenAttacker;
    [SerializeField] private EnemyExplosiveAttacker explosiveAttacker;
    [SerializeField] private Transform targetPlayer;

    private readonly List<IEnemySkillCaster> skillCasters = new();
    private float[] nextActionReadyTimes = Array.Empty<float>();
    private EnemyAIProfile actionTimingProfile;
    private float nextTickTime;
    private bool hasMovementOverride;
    private EnemyMovementKind movementOverride;

    public EnemyAIProfile Profile => profile;
    public bool IsProfileActive => enabled && profile != null;

    private void Awake()
    {
        TryCacheBindings();
        CacheSkillCasters();
    }

    private void Reset()
    {
        TryCacheBindings(overwriteExisting: true);
        CacheSkillCasters();
    }

    private void OnValidate()
    {
        TryCacheBindings();
        CacheSkillCasters();
    }

    private void Update()
    {
        if (!IsProfileActive || EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        float currentTime = Time.time;
        if (currentTime < nextTickTime)
        {
            return;
        }

        nextTickTime = currentTime + profile.TickIntervalSeconds;
        TickAI(currentTime);
    }

    /// <summary>
    /// summary: 应用一个 AI profile；传入 null 时清除 AI 接管并回到旧行为路径。
    /// param: nextProfile 当前敌人定义指定的 AI profile
    /// returns: profile 已成功写入时返回 true
    /// </summary>
    public bool ApplyProfile(EnemyAIProfile nextProfile)
    {
        if (!TryCacheBindings())
        {
            return false;
        }

        profile = nextProfile;
        nextTickTime = 0f;
        actionTimingProfile = null;
        nextActionReadyTimes = Array.Empty<float>();
        hasMovementOverride = false;
        if (profile == null)
        {
            movementOverride = EnemyMovementKind.None;
        }

        return true;
    }

    /// <summary>
    /// summary: 提供给移动组件读取当前 AI 选择出的 movement kind。
    /// param: movementKind 输出的 AI movement override
    /// returns: 当前 profile 已激活且存在 movement override 时返回 true
    /// </summary>
    public bool TryGetMovementOverride(out EnemyMovementKind movementKind)
    {
        movementKind = movementOverride;
        return IsProfileActive && hasMovementOverride;
    }

    /// <summary>
    /// summary: 执行一次 AI 决策 tick，按最高分可执行行动触发行为。
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功执行一个 profile 行动或 fallback 行动时返回 true
    /// </summary>
    public bool TickAI(float currentTime)
    {
        if (!IsProfileActive || !TryCacheBindings())
        {
            hasMovementOverride = false;
            return false;
        }

        CacheSkillCasters();
        EnemyAIContext context = EnemyAIContext.Create(enemyData, statusEffects, ResolveTargetPlayer(), profile.PerceptionRadius);
        EnsureActionRuntimeState(profile.Actions.Count);

        int bestActionIndex = -1;
        EnemyAIActionDefinition bestAction = default;
        float bestScore = 0f;
        for (int i = 0; i < profile.Actions.Count; i++)
        {
            EnemyAIActionDefinition action = profile.GetAction(i);
            if (currentTime < nextActionReadyTimes[i] || !CanExecuteAction(action, context, currentTime))
            {
                continue;
            }

            float score = action.Evaluate(context);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestAction = action;
            bestActionIndex = i;
        }

        if (bestActionIndex >= 0 && TryExecuteAction(bestAction, context, currentTime))
        {
            nextActionReadyTimes[bestActionIndex] = currentTime + bestAction.CooldownSeconds;
            return true;
        }

        EnemyAIActionDefinition fallbackAction = profile.FallbackAction;
        return TryExecuteAction(fallbackAction, context, currentTime);
    }

    private bool CanExecuteAction(EnemyAIActionDefinition action, EnemyAIContext context, float currentTime)
    {
        switch (action.ActionKind)
        {
            case EnemyAIActionKind.Movement:
                return context.CanMove && action.MovementKind != EnemyMovementKind.None;

            case EnemyAIActionKind.MeleeAttack:
                return action.AttackKind == EnemyAttackKind.MeleeContact
                    && context.CanAct
                    && meleeAttacker != null
                    && meleeAttacker.CanExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.RangedAttack:
                return action.AttackKind == EnemyAttackKind.RangedBulletToken
                    && context.CanAct
                    && rangedTokenAttacker != null
                    && rangedTokenAttacker.CanExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.ProximityExplosion:
                return action.AttackKind == EnemyAttackKind.ProximityExplosion
                    && context.CanAct
                    && explosiveAttacker != null
                    && explosiveAttacker.CanExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.CastSkill:
                return context.CanAct && TryResolveSkillSlot(action, out _, out IEnemySkillCaster skillCaster) && skillCaster != null;

            case EnemyAIActionKind.None:
            default:
                return false;
        }
    }

    private bool TryExecuteAction(EnemyAIActionDefinition action, EnemyAIContext context, float currentTime)
    {
        switch (action.ActionKind)
        {
            case EnemyAIActionKind.Movement:
                return TryApplyMovementOverride(action.MovementKind);

            case EnemyAIActionKind.MeleeAttack:
                return meleeAttacker != null && meleeAttacker.TryExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.RangedAttack:
                return rangedTokenAttacker != null && rangedTokenAttacker.TryExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.ProximityExplosion:
                return explosiveAttacker != null && explosiveAttacker.TryExecuteAIAction(context, currentTime);

            case EnemyAIActionKind.CastSkill:
                return TryCastSkillAction(action);

            case EnemyAIActionKind.None:
            default:
                return false;
        }
    }

    private bool TryApplyMovementOverride(EnemyMovementKind movementKind)
    {
        if (movementKind == EnemyMovementKind.None)
        {
            return false;
        }

        movementOverride = movementKind;
        hasMovementOverride = true;
        if (movement != null && !movement.enabled)
        {
            movement.enabled = true;
        }

        return true;
    }

    private bool TryCastSkillAction(EnemyAIActionDefinition action)
    {
        if (!TryResolveSkillSlot(action, out EnemyDefinition.EnemySkillSlotDefinition skillSlot, out IEnemySkillCaster skillCaster))
        {
            return false;
        }

        if (!skillCaster.TryCastSkill(skillSlot))
        {
            return false;
        }

        if (TryResolveStatusEffects())
        {
            float actionLockSeconds = action.ActionLockSeconds > 0f
                ? action.ActionLockSeconds
                : skillSlot.ResolveActionLockSeconds(DefaultSkillActionLockSeconds);
            statusEffects.ApplySkillActionLock(actionLockSeconds);
        }

        return true;
    }

    private bool TryResolveSkillSlot(
        EnemyAIActionDefinition action,
        out EnemyDefinition.EnemySkillSlotDefinition skillSlot,
        out IEnemySkillCaster skillCaster)
    {
        skillSlot = default;
        skillCaster = null;
        if (enemyData == null || enemyData.Definition == null || action.SkillKind == EnemySkillKind.None)
        {
            return false;
        }

        IReadOnlyList<EnemyDefinition.EnemySkillSlotDefinition> skillSlots = enemyData.Definition.SkillSlots;
        int requestedIndex = action.SkillSlotIndex;
        if (requestedIndex >= 0 && requestedIndex < skillSlots.Count && skillSlots[requestedIndex].skillKind == action.SkillKind)
        {
            skillSlot = skillSlots[requestedIndex].GetSanitized();
            return TryGetSkillCaster(action.SkillKind, out skillCaster);
        }

        for (int i = 0; i < skillSlots.Count; i++)
        {
            if (skillSlots[i].skillKind != action.SkillKind)
            {
                continue;
            }

            skillSlot = skillSlots[i].GetSanitized();
            return TryGetSkillCaster(action.SkillKind, out skillCaster);
        }

        return false;
    }

    private bool TryGetSkillCaster(EnemySkillKind skillKind, out IEnemySkillCaster skillCaster)
    {
        for (int i = 0; i < skillCasters.Count; i++)
        {
            if (skillCasters[i].SkillKind == skillKind)
            {
                skillCaster = skillCasters[i];
                return true;
            }
        }

        skillCaster = null;
        return false;
    }

    private void EnsureActionRuntimeState(int expectedActionCount)
    {
        if (actionTimingProfile == profile && nextActionReadyTimes.Length == expectedActionCount)
        {
            return;
        }

        nextActionReadyTimes = expectedActionCount > 0 ? new float[expectedActionCount] : Array.Empty<float>();
        actionTimingProfile = profile;
    }

    private void CacheSkillCasters()
    {
        skillCasters.Clear();
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemySkillCaster skillCaster)
            {
                skillCasters.Add(skillCaster);
            }
        }
    }

    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || enemyData == null || enemyData.transform != transform)
        {
            enemyData = GetComponent<Enemy>();
        }

        if (overwriteExisting || statusEffects == null || statusEffects.transform != transform)
        {
            statusEffects = GetComponent<EnemyStatusEffectController>();
        }

        if (overwriteExisting || movement == null || movement.transform != transform)
        {
            movement = GetComponent<CharEnemyMovement>();
        }

        if (overwriteExisting || meleeAttacker == null || meleeAttacker.transform != transform)
        {
            meleeAttacker = GetComponent<EnemyMeleeAttacker>();
        }

        if (overwriteExisting || rangedTokenAttacker == null || rangedTokenAttacker.transform != transform)
        {
            rangedTokenAttacker = GetComponent<EnemyRangedTokenAttacker>();
        }

        if (overwriteExisting || explosiveAttacker == null || explosiveAttacker.transform != transform)
        {
            explosiveAttacker = GetComponent<EnemyExplosiveAttacker>();
        }

        return enemyData != null;
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

    private Transform ResolveTargetPlayer()
    {
        if (targetPlayer != null && !IsOwnTransform(targetPlayer))
        {
            return targetPlayer;
        }

        PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
        targetPlayer = playerMovement != null ? playerMovement.transform : null;
        return targetPlayer;
    }

    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
