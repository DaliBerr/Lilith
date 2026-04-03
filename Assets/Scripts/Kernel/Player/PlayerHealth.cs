using UnityEngine;

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

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        remainingHealth = currentHealth;
        isDead = IsDead;
        return true;
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
