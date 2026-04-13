/// <summary>
/// 表示玩家生命值首次归零后的死亡结果。
/// </summary>
public readonly struct PlayerDiedEvent
{
    /// <summary>
    /// 触发本次死亡的生命组件。
    /// </summary>
    public readonly PlayerHealth source;

    /// <summary>
    /// 死亡前的生命值。
    /// </summary>
    public readonly float previousHealth;

    /// <summary>
    /// 死亡后的生命值。
    /// </summary>
    public readonly float currentHealth;

    /// <summary>
    /// 当前最大生命值。
    /// </summary>
    public readonly float maxHealth;

    /// <summary>
    /// 创建一次玩家死亡事件。
    /// </summary>
    /// <param name="source">触发本次死亡的生命组件。</param>
    /// <param name="previousHealth">死亡前的生命值。</param>
    /// <param name="currentHealth">死亡后的生命值。</param>
    /// <param name="maxHealth">当前最大生命值。</param>
    public PlayerDiedEvent(PlayerHealth source, float previousHealth, float currentHealth, float maxHealth)
    {
        this.source = source;
        this.previousHealth = previousHealth;
        this.currentHealth = currentHealth;
        this.maxHealth = maxHealth;
    }
}
