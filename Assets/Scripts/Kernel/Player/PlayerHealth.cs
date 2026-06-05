using UnityEngine;
using Vocalith.EventSystem;

/// <summary>
/// 提供玩家最小生命值与受伤结算能力。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    private const float DefaultMaxHealth = 100f;

    [SerializeField, Min(0.01f)] private float maxHealth = DefaultMaxHealth;

    private float currentHealth;
    private bool hasInitializedHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth
    {
        get
        {
            EnsureInitialized();
            return currentHealth;
        }
    }

    public bool IsDead => CurrentHealth <= 0f;

    private void Awake()
    {
        SanitizeConfiguration();
        ResetHealthToFull();
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
        if (!Application.isPlaying)
        {
            currentHealth = maxHealth;
            hasInitializedHealth = true;
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    private void Reset()
    {
        SanitizeConfiguration();
        ResetHealthToFull();
    }

    /// <summary>
    /// summary: 对当前玩家结算一次伤害，并返回最新生命结果。
    /// param: damage 本次伤害值
    /// param: remainingHealth 本次结算后的剩余生命值
    /// param: isDead 本次结算后玩家是否死亡
    /// returns: 成功处理本次伤害时返回 true
    /// </summary>
    public bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead)
    {
        EnsureInitialized();
        remainingHealth = currentHealth;
        isDead = IsDead;
        if (damage <= 0f || IsDead)
        {
            return false;
        }

        float resolvedDamage = damage;
        if (TryGetComponent(out DamageShieldController shield) &&
            shield.TryAbsorbDamage(damage, out float remainingDamage, out _))
        {
            resolvedDamage = remainingDamage;
        }

        if (resolvedDamage <= 0f)
        {
            remainingHealth = currentHealth;
            isDead = IsDead;
            return true;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - resolvedDamage);
        remainingHealth = currentHealth;
        isDead = IsDead;
        PublishHealthChanged(previousHealth);
        PublishDeathIfNeeded(previousHealth);
        return true;
    }

    /// <summary>
    /// summary: 结算一次由施法等系统产生的生命代价；该代价绕过吸收盾。
    /// param: healthCost 本次需要扣除的生命值
    /// param: remainingHealth 本次结算后的剩余生命值
    /// param: isDead 本次结算后玩家是否死亡
    /// returns: 成功处理本次生命代价时返回 true
    /// </summary>
    public bool TryApplyHealthCost(float healthCost, out float remainingHealth, out bool isDead)
    {
        EnsureInitialized();
        remainingHealth = currentHealth;
        isDead = IsDead;
        if (healthCost <= 0f || IsDead)
        {
            return false;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - healthCost);
        remainingHealth = currentHealth;
        isDead = IsDead;
        PublishHealthChanged(previousHealth);
        PublishDeathIfNeeded(previousHealth);
        return true;
    }

    /// <summary>
    /// summary: 对当前玩家结算一次治疗，并返回最新生命结果。
    /// param: healing 本次治疗值
    /// param: resultingHealth 本次结算后的生命值
    /// param: isDead 本次结算后玩家是否死亡
    /// returns: 成功处理本次治疗时返回 true
    /// </summary>
    public bool TryApplyHealing(float healing, out float resultingHealth, out bool isDead)
    {
        EnsureInitialized();
        resultingHealth = currentHealth;
        isDead = IsDead;
        if (healing <= 0f || IsDead || currentHealth >= maxHealth)
        {
            return false;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + healing);
        resultingHealth = currentHealth;
        isDead = IsDead;
        PublishHealthChanged(previousHealth);
        return true;
    }

    /// <summary>
    /// summary: 把当前生命值恢复到满血；可选是否广播一次生命变化事件。
    /// param name="publishChangeEvent": 为 true 时，生命值实际变化后会补发一次 PlayerHealthChangedEvent
    /// returns: 无
    /// </summary>
    public void RestoreFullHealth(bool publishChangeEvent = true)
    {
        EnsureInitialized();
        float previousHealth = currentHealth;
        currentHealth = maxHealth;
        if (publishChangeEvent && !Mathf.Approximately(previousHealth, currentHealth))
        {
            PublishHealthChanged(previousHealth);
        }
    }

    /// <summary>
    /// summary: 确保在编辑器测试或首次访问时，当前生命值已经被初始化为满血状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureInitialized()
    {
        if (hasInitializedHealth)
        {
            return;
        }

        SanitizeConfiguration();
        ResetHealthToFull();
    }

    /// <summary>
    /// summary: 把当前玩家生命值重置到配置的满血状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResetHealthToFull()
    {
        currentHealth = maxHealth;
        hasInitializedHealth = true;
    }

    /// <summary>
    /// summary: 在生命值发生有效变化后，向事件总线广播当前玩家的生命结果。
    /// param: previousHealth 变化前生命值
    /// returns: 无
    /// </summary>
    private void PublishHealthChanged(float previousHealth)
    {
        EventManager.eventBus.Publish(new PlayerHealthChangedEvent(
            this,
            previousHealth,
            currentHealth,
            maxHealth,
            currentHealth - previousHealth,
            IsDead));
    }

    /// <summary>
    /// summary: 当一次扣血把玩家从存活状态推进到死亡状态时，补发独立死亡事件。
    /// param name="previousHealth": 变化前生命值
    /// returns: 无
    /// </summary>
    private void PublishDeathIfNeeded(float previousHealth)
    {
        if (previousHealth <= 0f || currentHealth > 0f)
        {
            return;
        }

        EventManager.eventBus.Publish(new PlayerDiedEvent(this, previousHealth, currentHealth, maxHealth));
    }

    /// <summary>
    /// summary: 修正玩家最大生命值，避免非法值导致受伤逻辑失效。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0f)
        {
            maxHealth = DefaultMaxHealth;
        }
    }
}
