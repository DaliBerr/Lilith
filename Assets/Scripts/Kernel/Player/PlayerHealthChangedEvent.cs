/// <summary>
/// 表示一次玩家生命值变化后的结果。
/// </summary>
public readonly struct PlayerHealthChangedEvent
{
    /// <summary>
    /// 触发本次变化的生命组件。
    /// </summary>
    public readonly PlayerHealth source;
    public readonly float previousHealth;
    public readonly float currentHealth;
    public readonly float maxHealth;
    public readonly float delta;
    public readonly bool isDead;

    /// <summary>
    /// 创建一次玩家生命值变化事件。
    /// </summary>
    /// <param name="source">触发本次变化的生命组件。</param>
    /// <param name="previousHealth">变化前生命值。</param>
    /// <param name="currentHealth">变化后生命值。</param>
    /// <param name="maxHealth">当前最大生命值。</param>
    /// <param name="delta">本次生命变化量。</param>
    /// <param name="isDead">变化后是否死亡。</param>
    public PlayerHealthChangedEvent(PlayerHealth source, float previousHealth, float currentHealth, float maxHealth, float delta, bool isDead)
    {
        this.source = source;
        this.previousHealth = previousHealth;
        this.currentHealth = currentHealth;
        this.maxHealth = maxHealth;
        this.delta = delta;
        this.isDead = isDead;
    }
}
