using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 把敌人定义资产应用到运行时 prefab 壳上的既有行为与视觉组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDefinitionBinder : MonoBehaviour
{
    private const float DefaultSkillActionLockSeconds = 0.15f;

    [SerializeField] private EnemyDefinition definition;
    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyStatusEffectController statusEffects;
    [SerializeField] private CharEnemyMovement movement;
    [SerializeField] private EnemyMeleeAttacker meleeAttacker;
    [SerializeField] private EnemyRangedTokenAttacker rangedTokenAttacker;
    [SerializeField] private EnemyExplosiveAttacker explosiveAttacker;
    [SerializeField] private EnemyAIController aiController;
    [SerializeField] private EnemySummoner summoner;
    [SerializeField] private CharGlyphPresenter glyphPresenter;
    [SerializeField] private CharEnemyVisualPresenter visualPresenter;
    [SerializeField] private EnemyResultVisualFeedback resultVisualFeedback;

    private readonly List<IEnemySkillCaster> skillCasters = new();
    private float[] nextSkillReadyTimes = Array.Empty<float>();
    private EnemyDefinition skillTimingDefinition;

    public EnemyDefinition Definition => definition;

    private void Awake()
    {
        TryCacheBindings();
        CacheSkillCasters();
        if (definition != null)
        {
            ApplyDefinition(definition);
        }
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
        if (definition != null)
        {
            ApplyDefinition(definition);
        }
    }

    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            return;
        }

        if (aiController != null && aiController.IsProfileActive)
        {
            return;
        }

        TryPerformSkills(Time.time);
    }

    /// <summary>
    /// summary: 解析并缓存当前敌人壳上的运行时组件引用。
    /// param: overwriteExisting 为 true 时强制重新解析当前所有绑定
    /// returns: 成功拿到 Enemy 根数据组件时返回 true
    /// </summary>
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

        if (overwriteExisting || aiController == null || aiController.transform != transform)
        {
            aiController = GetComponent<EnemyAIController>();
        }

        if (overwriteExisting || summoner == null || summoner.transform != transform)
        {
            summoner = GetComponent<EnemySummoner>();
        }

        if (overwriteExisting || glyphPresenter == null || glyphPresenter.transform != transform)
        {
            glyphPresenter = GetComponent<CharGlyphPresenter>();
        }

        if (overwriteExisting || visualPresenter == null || visualPresenter.transform != transform)
        {
            visualPresenter = GetComponent<CharEnemyVisualPresenter>();
        }

        if (overwriteExisting || resultVisualFeedback == null || resultVisualFeedback.transform != transform)
        {
            resultVisualFeedback = GetComponent<EnemyResultVisualFeedback>();
        }

        return enemyData != null;
    }

    /// <summary>
    /// summary: 把给定敌人定义写入当前 prefab 壳，并同步行为开关和视觉表现。
    /// param: nextDefinition 当前实例应绑定的敌人定义
    /// returns: 成功完成定义绑定时返回 true
    /// </summary>
    public bool ApplyDefinition(EnemyDefinition nextDefinition)
    {
        if (nextDefinition == null || !TryCacheBindings())
        {
            return false;
        }

        CacheSkillCasters();
        definition = nextDefinition;
        if (!enemyData.TryBindDefinition(definition))
        {
            return false;
        }

        if (!ApplyMovementKind(definition.MovementKind) ||
            !ApplyAttackKind(definition.AttackKind) ||
            !ApplySkillSlots(definition.SkillSlots))
        {
            return false;
        }

        if (!TryResolveResultVisualFeedback())
        {
            return false;
        }

        ResetSkillRuntimeState(forceReset: true);
        if (!ApplyAIProfile(definition.AIProfile))
        {
            return false;
        }

        ApplyVisuals(definition.Visual);
        return true;
    }

    private bool ApplyAIProfile(EnemyAIProfile aiProfile)
    {
        if (aiProfile == null)
        {
            if (aiController != null)
            {
                aiController.ApplyProfile(null);
            }

            return true;
        }

        if (aiController == null)
        {
            aiController = gameObject.AddComponent<EnemyAIController>();
        }

        aiController.TryCacheBindings(overwriteExisting: true);
        return aiController.ApplyProfile(aiProfile);
    }

    private bool ApplyMovementKind(EnemyMovementKind movementKind)
    {
        bool shouldEnableMovement = movementKind != EnemyMovementKind.None;
        if (movement == null)
        {
            return !shouldEnableMovement;
        }

        movement.enabled = shouldEnableMovement;
        return true;
    }

    private bool ApplyAttackKind(EnemyAttackKind attackKind)
    {
        bool shouldEnableMelee = attackKind == EnemyAttackKind.MeleeContact;
        bool shouldEnableRanged = attackKind == EnemyAttackKind.RangedBulletToken;
        bool shouldEnableExplosive = attackKind == EnemyAttackKind.ProximityExplosion;
        if ((shouldEnableMelee && meleeAttacker == null) ||
            (shouldEnableRanged && rangedTokenAttacker == null) ||
            (shouldEnableExplosive && explosiveAttacker == null))
        {
            return false;
        }

        if (meleeAttacker != null)
        {
            meleeAttacker.enabled = shouldEnableMelee;
        }

        if (rangedTokenAttacker != null)
        {
            rangedTokenAttacker.enabled = shouldEnableRanged;
        }

        if (explosiveAttacker != null)
        {
            explosiveAttacker.enabled = shouldEnableExplosive;
        }

        return true;
    }

    /// <summary>
    /// summary: 按技能槽列表启停当前 prefab 壳上的技能执行器，并校验每个技能槽都有对应执行组件。
    /// param: skillSlots 当前定义声明的技能槽列表
    /// returns: 所有技能槽都能找到对应执行器时返回 true
    /// </summary>
    private bool ApplySkillSlots(IReadOnlyList<EnemyDefinition.EnemySkillSlotDefinition> skillSlots)
    {
        for (int i = 0; i < skillCasters.Count; i++)
        {
            if (skillCasters[i] is not Behaviour skillCasterBehaviour)
            {
                continue;
            }

            skillCasterBehaviour.enabled = ContainsSkillKind(skillSlots, skillCasters[i].SkillKind);
        }

        for (int i = 0; i < skillSlots.Count; i++)
        {
            EnemyDefinition.EnemySkillSlotDefinition skillSlot = skillSlots[i];
            if (skillSlot.skillKind == EnemySkillKind.None)
            {
                continue;
            }

            if (!TryGetSkillCaster(skillSlot.skillKind, requireEnabled: false, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// summary: 按定义里的技能槽顺序调度技能释放，并为每个槽位维护独立冷却。
    /// param: currentTime 当前逻辑时钟
    /// returns: 本帧至少成功释放一个技能槽时返回 true
    /// </summary>
    private bool TryPerformSkills(float currentTime)
    {
        if (definition == null)
        {
            return false;
        }

        IReadOnlyList<EnemyDefinition.EnemySkillSlotDefinition> skillSlots = definition.SkillSlots;
        if (skillSlots.Count <= 0)
        {
            return false;
        }

        EnsureSkillRuntimeState(skillSlots.Count);
        int remainingSkillCasts = definition.SkillCasting.maxSkillCastsPerTick;
        bool hasCastAnySkill = false;
        for (int i = 0; i < skillSlots.Count && remainingSkillCasts > 0; i++)
        {
            EnemyDefinition.EnemySkillSlotDefinition skillSlot = skillSlots[i];
            if (skillSlot.skillKind == EnemySkillKind.None || currentTime < nextSkillReadyTimes[i])
            {
                continue;
            }

            if (!TryGetSkillCaster(skillSlot.skillKind, requireEnabled: true, out IEnemySkillCaster skillCaster))
            {
                continue;
            }

            if (!skillCaster.TryCastSkill(skillSlot))
            {
                continue;
            }

            if (TryResolveStatusEffects())
            {
                statusEffects.ApplySkillActionLock(skillSlot.ResolveActionLockSeconds(DefaultSkillActionLockSeconds));
            }

            nextSkillReadyTimes[i] = currentTime + Mathf.Max(0f, skillSlot.cooldownSeconds);
            remainingSkillCasts--;
            hasCastAnySkill = true;
        }

        return hasCastAnySkill;
    }

    /// <summary>
    /// summary: 重新扫描当前敌人根节点上的技能执行器组件，供多技能调度和启停逻辑复用。
    /// param: 无
    /// returns: 无
    /// </summary>
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

    /// <summary>
    /// summary: 保证技能冷却状态数组与当前绑定定义的技能槽数量一致；定义切换时会重置全部技能冷却。
    /// param: expectedSlotCount 当前定义的技能槽数量
    /// returns: 无
    /// </summary>
    private void EnsureSkillRuntimeState(int expectedSlotCount)
    {
        if (skillTimingDefinition == definition && nextSkillReadyTimes.Length == expectedSlotCount)
        {
            return;
        }

        nextSkillReadyTimes = expectedSlotCount > 0 ? new float[expectedSlotCount] : Array.Empty<float>();
        skillTimingDefinition = definition;
    }

    /// <summary>
    /// summary: 在定义切换或重新绑定时显式重置技能冷却状态，避免旧定义残留冷却延续到新定义。
    /// param: forceReset 为 true 时无条件清空当前技能冷却状态
    /// returns: 无
    /// </summary>
    private void ResetSkillRuntimeState(bool forceReset = false)
    {
        if (forceReset)
        {
            nextSkillReadyTimes = Array.Empty<float>();
            skillTimingDefinition = null;
        }
    }

    /// <summary>
    /// summary: 解析当前技能类型是否出现在定义的技能槽列表中，用于同步技能组件启停状态。
    /// param: skillSlots 当前定义声明的技能槽列表
    /// param: skillKind 需要检查的技能类型
    /// returns: 至少存在一个匹配技能槽时返回 true
    /// </summary>
    private static bool ContainsSkillKind(IReadOnlyList<EnemyDefinition.EnemySkillSlotDefinition> skillSlots, EnemySkillKind skillKind)
    {
        if (skillKind == EnemySkillKind.None)
        {
            return false;
        }

        for (int i = 0; i < skillSlots.Count; i++)
        {
            if (skillSlots[i].skillKind == skillKind)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// summary: 在当前敌人根节点上查找一个能处理指定技能类型的执行器，并可选过滤未启用组件。
    /// param: skillKind 当前要调度的技能类型
    /// param: requireEnabled 为 true 时会跳过未启用的 Behaviour 执行器
    /// param: skillCaster 输出的技能执行器引用
    /// returns: 成功找到匹配执行器时返回 true
    /// </summary>
    private bool TryGetSkillCaster(EnemySkillKind skillKind, bool requireEnabled, out IEnemySkillCaster skillCaster)
    {
        for (int i = 0; i < skillCasters.Count; i++)
        {
            if (skillCasters[i].SkillKind != skillKind)
            {
                continue;
            }

            if (requireEnabled && skillCasters[i] is Behaviour behaviour && !behaviour.enabled)
            {
                continue;
            }

            skillCaster = skillCasters[i];
            return true;
        }

        if (TryEnsureSkillCasterComponent(skillKind, out skillCaster))
        {
            if (requireEnabled && skillCaster is Behaviour behavior && !behavior.enabled)
            {
                return false;
            }

            return true;
        }

        skillCaster = null;
        return false;
    }

    /// <summary>
    /// summary: 按技能类型按需补齐缺失的技能执行器组件，避免定义新增后旧 prefab 壳无法调度。
    /// param: skillKind 当前要调度的技能类型
    /// param: skillCaster 输出的技能执行器引用
    /// returns: 成功拿到可用执行器时返回 true
    /// </summary>
    private bool TryEnsureSkillCasterComponent(EnemySkillKind skillKind, out IEnemySkillCaster skillCaster)
    {
        skillCaster = null;
        if (skillKind == EnemySkillKind.None)
        {
            return false;
        }

        if (skillKind == EnemySkillKind.DelayedGroundBomb)
        {
            if (!TryGetComponent(out EnemyDelayedGroundBombCaster delayedBombCaster) || delayedBombCaster == null)
            {
                delayedBombCaster = gameObject.AddComponent<EnemyDelayedGroundBombCaster>();
            }

            CacheSkillCasters();
            skillCaster = delayedBombCaster;
            return true;
        }

        return false;
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

    private bool TryResolveResultVisualFeedback()
    {
        if (resultVisualFeedback != null && resultVisualFeedback.transform == transform)
        {
            return true;
        }

        if (!TryGetComponent(out resultVisualFeedback) || resultVisualFeedback == null)
        {
            resultVisualFeedback = gameObject.AddComponent<EnemyResultVisualFeedback>();
        }

        if (resultVisualFeedback == null)
        {
            return false;
        }

        resultVisualFeedback.TryCacheBindings();
        return true;
    }

    private void ApplyVisuals(EnemyDefinition.EnemyVisualDefinition visual)
    {
        if (glyphPresenter != null)
        {
            glyphPresenter.SetDisplayText(visual.glyphText);
            TMP_Text glyphText = glyphPresenter.GlyphText;
            if (glyphText != null)
            {
                glyphText.color = visual.glyphColor;
            }
        }

        if (visualPresenter != null)
        {
            visualPresenter.ApplyVisualDefinition(visual);
        }
    }
}
