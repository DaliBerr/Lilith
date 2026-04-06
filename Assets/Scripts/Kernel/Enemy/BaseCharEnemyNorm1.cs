using UnityEngine;

/// <summary>
/// 当前默认的基础文字敌人类型。
/// </summary>
[DisallowMultipleComponent]
public sealed class BaseCharEnemyNorm1 : Enemy, ILegacyEnemyMovementSettingsReceiver, IEnemyWaveConfigReceiver
{
    private const float DefaultMoveSpeed = 120f;
    private const float DefaultRotationSpeed = 540f;
    private const float DefaultStoppingDistance = 1f;
    private const float DefaultAttackRange = 0f;
    private const float DefaultAttackCooldown = 0f;
    private const float DefaultAttackDamage = 0f;
    private const float DefaultHealth = 1f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField, Min(0f)] private float rotationSpeed = DefaultRotationSpeed;
    [SerializeField, Min(0f)] private float stoppingDistance = DefaultStoppingDistance;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float attackRange = DefaultAttackRange;
    [SerializeField, Min(0f)] private float attackCooldown = DefaultAttackCooldown;
    [SerializeField, Min(0f)] private float attackDamage = DefaultAttackDamage;
    [SerializeField, Min(0f)] private float health = DefaultHealth;

    public override float MoveSpeed => moveSpeed;
    public override float RotationSpeed => rotationSpeed;
    public override float StoppingDistance => stoppingDistance;
    public override float AttackRange => attackRange;
    public override float AttackCooldown => attackCooldown;
    public override float AttackDamage => attackDamage;
    public override float MaxHealth => health;
    public EnemyWaveConfig CurrentWaveConfig => currentWaveConfig;
    public bool HasWaveConfig => hasWaveConfig;
    public override float CurrentHealth
    {
        get
        {
            EnsureHealthInitialized();
            return currentHealth;
        }
    }

    private float currentHealth;
    private bool hasInitializedHealth;
    private EnemyWaveConfig currentWaveConfig;
    private bool hasWaveConfig;

    private void Awake()
    {
        SanitizeConfiguration();
        ResetHealthToFull();
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
        currentHealth = Application.isPlaying ? Mathf.Clamp(currentHealth, 0f, health) : health;
    }

    private void Reset()
    {
        ApplyDefaultConfiguration();
    }

    /// <summary>
    /// summary: 仅当当前敌人仍处于默认运动参数时，才接收旧版移动组件里的 legacy 参数。
    /// param: legacyMoveSpeed 旧版移动组件序列化的移动速度
    /// param: legacyRotationSpeed 旧版移动组件序列化的旋转速度
    /// param: legacyStoppingDistance 旧版移动组件序列化的停止距离
    /// returns: 成功把 legacy 参数写入当前敌人时返回 true
    /// </summary>
    public bool TryApplyLegacyMovementSettingsIfNeeded(float legacyMoveSpeed, float legacyRotationSpeed, float legacyStoppingDistance)
    {
        if (!IsUsingDefaultMovementSettings() || !DoLegacyMovementSettingsDifferFromDefault(legacyMoveSpeed, legacyRotationSpeed, legacyStoppingDistance))
        {
            return false;
        }

        moveSpeed = legacyMoveSpeed;
        rotationSpeed = legacyRotationSpeed;
        stoppingDistance = legacyStoppingDistance;
        SanitizeConfiguration();
        return true;
    }

    /// <summary>
    /// summary: 把当前波次给出的运行时配置覆盖到当前敌人数据，并把生命值重置到新满血。
    /// param: config 当前波次指定的敌人数值配置
    /// returns: 无
    /// </summary>
    public void ApplyWaveConfig(EnemyWaveConfig config)
    {
        currentWaveConfig = config;
        hasWaveConfig = true;
        health = SanitizePositiveValue(config.maxHealth, health);
        moveSpeed = SanitizeValue(config.moveSpeed, moveSpeed);
        attackRange = SanitizeValue(config.attackRange, attackRange);
        attackCooldown = SanitizeValue(config.attackCooldown, attackCooldown);
        attackDamage = SanitizeValue(config.attackDamage, attackDamage);
        stoppingDistance = attackRange;
        SanitizeConfiguration();
        ResetHealthToFull();
    }

    /// <summary>
    /// summary: 尝试对当前敌人扣减生命值；生命归零时销毁当前对象。
    /// param: damage 本次命中造成的伤害数值
    /// param: remainingHealth 本次伤害结算后的剩余生命值
    /// param: isDead 本次伤害结算后是否已经死亡
    /// returns: 成功应用本次伤害时返回 true
    /// </summary>
    public override bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead)
    {
        EnsureHealthInitialized();
        remainingHealth = currentHealth;
        isDead = IsDead;
        if (damage <= 0f || IsDead)
        {
            return false;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        remainingHealth = currentHealth;
        isDead = IsDead;
        if (isDead)
        {
            HandleDeath();
        }

        return true;
    }

    /// <summary>
    /// summary: 把当前敌人的默认配置写回序列化字段，供首次挂载组件时使用。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ApplyDefaultConfiguration()
    {
        hasWaveConfig = false;
        currentWaveConfig = default;
        moveSpeed = DefaultMoveSpeed;
        rotationSpeed = DefaultRotationSpeed;
        stoppingDistance = DefaultStoppingDistance;
        attackRange = DefaultAttackRange;
        attackCooldown = DefaultAttackCooldown;
        attackDamage = DefaultAttackDamage;
        health = DefaultHealth;
        SanitizeConfiguration();
        ResetHealthToFull();
    }

    /// <summary>
    /// summary: 修正当前敌人的序列化数据；仅当数据非法时才回退到该子类的默认值。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        moveSpeed = SanitizeValue(moveSpeed, DefaultMoveSpeed);
        rotationSpeed = SanitizeValue(rotationSpeed, DefaultRotationSpeed);
        stoppingDistance = SanitizeValue(stoppingDistance, DefaultStoppingDistance);
        attackRange = SanitizeValue(attackRange, DefaultAttackRange);
        attackCooldown = SanitizeValue(attackCooldown, DefaultAttackCooldown);
        attackDamage = SanitizeValue(attackDamage, DefaultAttackDamage);
        health = SanitizePositiveValue(health, DefaultHealth);
    }

    /// <summary>
    /// summary: 把运行时生命值重置到当前配置的满血状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResetHealthToFull()
    {
        currentHealth = health;
        hasInitializedHealth = true;
        ResetDeathNotificationState();
    }

    /// <summary>
    /// summary: 当生命归零时销毁当前敌人对象，避免继续参与移动和碰撞。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void HandleDeath()
    {
        TryNotifyDied();
        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }

    /// <summary>
    /// summary: 判断当前运动参数是否仍然等于该敌人类型声明的默认值。
    /// param: 无
    /// returns: 三项运动参数都等于默认值时返回 true
    /// </summary>
    private bool IsUsingDefaultMovementSettings()
    {
        return Mathf.Approximately(moveSpeed, DefaultMoveSpeed) &&
               Mathf.Approximately(rotationSpeed, DefaultRotationSpeed) &&
               Mathf.Approximately(stoppingDistance, DefaultStoppingDistance);
    }

    /// <summary>
    /// summary: 判断旧版移动组件里的参数是否与当前敌人类型默认值存在差异。
    /// param: legacyMoveSpeed 旧版移动速度
    /// param: legacyRotationSpeed 旧版旋转速度
    /// param: legacyStoppingDistance 旧版停止距离
    /// returns: 任一参数与默认值不同则返回 true
    /// </summary>
    private static bool DoLegacyMovementSettingsDifferFromDefault(float legacyMoveSpeed, float legacyRotationSpeed, float legacyStoppingDistance)
    {
        return !Mathf.Approximately(legacyMoveSpeed, DefaultMoveSpeed) ||
               !Mathf.Approximately(legacyRotationSpeed, DefaultRotationSpeed) ||
               !Mathf.Approximately(legacyStoppingDistance, DefaultStoppingDistance);
    }

    /// <summary>
    /// summary: 修正单个数值字段；仅当数值非法时才回退到默认值。
    /// param: value 当前序列化值
    /// param: fallbackValue 当前子类声明的默认值
    /// returns: 合法值原样返回，非法值回退默认
    /// </summary>
    private static float SanitizeValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return fallbackValue;
        }

        return value;
    }

    /// <summary>
    /// summary: 修正必须为正数的字段；零值或非法值会回退到默认值。
    /// param: value 当前序列化值
    /// param: fallbackValue 当前子类声明的默认值
    /// returns: 合法正值原样返回，其余情况回退默认
    /// </summary>
    private static float SanitizePositiveValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return fallbackValue;
        }

        return value;
    }

    /// <summary>
    /// summary: 确保在测试或首次访问生命值时，当前敌人已经有合法的运行时生命状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureHealthInitialized()
    {
        if (hasInitializedHealth)
        {
            return;
        }

        SanitizeConfiguration();
        ResetHealthToFull();
    }
}
