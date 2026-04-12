/// <summary>
/// 表示一次 Boss 遭遇已开始，Boss UI 可以据此开始展示。
/// </summary>
public readonly struct BossEncounterStartedEvent
{
    public readonly Enemy boss;
    public readonly string displayName;
    public readonly float currentHealth;
    public readonly float maxHealth;

    /// <summary>
    /// 创建一次 Boss 遭遇开始事件。
    /// </summary>
    /// <param name="boss">当前进入展示态的 Boss 实例。</param>
    /// <param name="displayName">Boss UI 应展示的名称。</param>
    /// <param name="currentHealth">Boss 当前生命值。</param>
    /// <param name="maxHealth">Boss 当前最大生命值。</param>
    public BossEncounterStartedEvent(Enemy boss, string displayName, float currentHealth, float maxHealth)
    {
        this.boss = boss;
        this.displayName = displayName;
        this.currentHealth = currentHealth;
        this.maxHealth = maxHealth;
    }
}
