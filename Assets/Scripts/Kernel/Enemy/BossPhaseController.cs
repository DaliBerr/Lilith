using UnityEngine;
using Vocalith.EventSystem;

/// <summary>
/// 监听 Boss 受击事件，并在生命值达到阈值时把 Boss 切到第二阶段定义。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPhaseController : MonoBehaviour
{
    private const float DefaultPhaseTwoTriggerHealthRatio = 0.5f;

    [SerializeField] private Enemy enemyData;
    [SerializeField] private EnemyDefinitionBinder definitionBinder;
    [SerializeField] private EnemyDefinition phaseTwoDefinition;
    [SerializeField, Range(0f, 1f)] private float phaseTwoTriggerHealthRatio = DefaultPhaseTwoTriggerHealthRatio;
    [SerializeField, Min(1)] private int phaseTwoIndex = 2;

    private bool isSubscribedToDamage;
    private bool hasEnteredPhaseTwo;

    private void Awake()
    {
        TryResolveEnemyData();
        TryResolveDefinitionBinder();
        SanitizeConfiguration();
        SyncPhaseStateFromCurrentDefinition();
        EnsureDamageSubscription();
    }

    private void OnEnable()
    {
        EnsureDamageSubscription();
    }

    private void OnDisable()
    {
        RemoveDamageSubscription();
    }

    private void OnDestroy()
    {
        RemoveDamageSubscription();
    }

    private void OnValidate()
    {
        TryResolveEnemyData();
        TryResolveDefinitionBinder();
        SanitizeConfiguration();
        SyncPhaseStateFromCurrentDefinition();
    }

    /// <summary>
    /// summary: 在运行时为当前 Boss 显式注入阶段切换所需依赖。
    /// param: enemy 当前要监听受击事件的 Boss 实例
    /// param: binder 当前 Boss 根节点上的定义绑定器
    /// param: nextPhaseDefinition 进入第二阶段后要切换到的定义
    /// param: triggerHealthRatio 触发第二阶段的生命百分比阈值
    /// returns: 成功完成配置并建立受击订阅时返回 true
    /// </summary>
    public bool TryConfigure(Enemy enemy, EnemyDefinitionBinder binder, EnemyDefinition nextPhaseDefinition, float triggerHealthRatio)
    {
        enemyData = enemy;
        definitionBinder = binder;
        phaseTwoDefinition = nextPhaseDefinition;
        phaseTwoTriggerHealthRatio = triggerHealthRatio;
        SanitizeConfiguration();
        SyncPhaseStateFromCurrentDefinition();

        if (ShouldEnterPhaseTwo())
        {
            return TryEnterPhaseTwo();
        }

        return EnsureDamageSubscription();
    }

    /// <summary>
    /// summary: 当 Boss 受击时检查是否跨过阶段阈值，并在首次满足条件时执行定义切换。
    /// param: damagedEnemy 本次触发受击回调的敌人实例
    /// returns: 无
    /// </summary>
    private void HandleEnemyDamaged(Enemy damagedEnemy)
    {
        if (damagedEnemy == null || damagedEnemy != enemyData)
        {
            return;
        }

        if (!ShouldEnterPhaseTwo())
        {
            return;
        }

        TryEnterPhaseTwo();
    }

    /// <summary>
    /// summary: 判断当前 Boss 是否满足进入第二阶段的条件。
    /// param: 无
    /// returns: 首次达到生命阈值且阶段配置完整时返回 true
    /// </summary>
    private bool ShouldEnterPhaseTwo()
    {
        if (hasEnteredPhaseTwo || phaseTwoDefinition == null || !TryResolveEnemyData())
        {
            return false;
        }

        float maxHealth = enemyData.MaxHealth;
        if (maxHealth <= 0f)
        {
            return false;
        }

        float triggerHealth = maxHealth * phaseTwoTriggerHealthRatio;
        return enemyData.CurrentHealth <= triggerHealth;
    }

    /// <summary>
    /// summary: 把当前 Boss 切换到第二阶段定义，并广播阶段切换事件。
    /// param: 无
    /// returns: 第二阶段切换实际完成时返回 true
    /// </summary>
    private bool TryEnterPhaseTwo()
    {
        if (hasEnteredPhaseTwo || phaseTwoDefinition == null || !TryResolveEnemyData() || !TryResolveDefinitionBinder())
        {
            return false;
        }

        if (!definitionBinder.ApplyDefinition(phaseTwoDefinition))
        {
            return false;
        }

        hasEnteredPhaseTwo = true;
        EventManager.eventBus.Publish(new BossPhaseChangedEvent(
            enemyData,
            phaseTwoIndex,
            phaseTwoDefinition.DisplayName));
        return true;
    }

    /// <summary>
    /// summary: 确保组件已经订阅当前 Boss 的受击事件。
    /// param: 无
    /// returns: 成功建立订阅时返回 true
    /// </summary>
    private bool EnsureDamageSubscription()
    {
        if (isSubscribedToDamage)
        {
            return true;
        }

        if (!TryResolveEnemyData())
        {
            return false;
        }

        enemyData.Damaged += HandleEnemyDamaged;
        isSubscribedToDamage = true;
        return true;
    }

    /// <summary>
    /// summary: 取消当前组件对 Boss 受击事件的订阅，避免停用后残留回调。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void RemoveDamageSubscription()
    {
        if (!isSubscribedToDamage || enemyData == null)
        {
            isSubscribedToDamage = false;
            return;
        }

        enemyData.Damaged -= HandleEnemyDamaged;
        isSubscribedToDamage = false;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定 Enemy 时，自动回退到当前物体上的 Enemy 组件。
    /// param: 无
    /// returns: 成功解析到 Enemy 数据组件时返回 true
    /// </summary>
    private bool TryResolveEnemyData()
    {
        if (enemyData != null && enemyData.transform == transform)
        {
            return true;
        }

        enemyData = null;
        return TryGetComponent(out enemyData);
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定定义绑定器时，自动回退到当前物体上的 EnemyDefinitionBinder。
    /// param: 无
    /// returns: 成功解析到定义绑定器时返回 true
    /// </summary>
    private bool TryResolveDefinitionBinder()
    {
        if (definitionBinder != null && definitionBinder.transform == transform)
        {
            return true;
        }

        definitionBinder = null;
        return TryGetComponent(out definitionBinder);
    }

    /// <summary>
    /// summary: 规范化阶段切换阈值与阶段索引配置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        phaseTwoTriggerHealthRatio = Mathf.Clamp01(phaseTwoTriggerHealthRatio);
        if (phaseTwoTriggerHealthRatio <= 0f)
        {
            phaseTwoTriggerHealthRatio = DefaultPhaseTwoTriggerHealthRatio;
        }

        phaseTwoIndex = Mathf.Max(1, phaseTwoIndex);
    }

    /// <summary>
    /// summary: 根据当前已绑定定义同步阶段状态，避免重复切换到同一阶段。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SyncPhaseStateFromCurrentDefinition()
    {
        hasEnteredPhaseTwo = phaseTwoDefinition != null &&
            TryResolveEnemyData() &&
            enemyData.Definition == phaseTwoDefinition;
    }
}
